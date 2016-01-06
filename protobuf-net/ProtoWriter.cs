// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;

using System.IO;
using System.Text;
using AqlaSerializer.Meta;
#if MF
using OverflowException = System.ApplicationException;
#endif

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
#endif

namespace AqlaSerializer
{
    /// <summary>
    /// Represents an output stream for writing protobuf data.
    /// 
    /// Why is the API backwards (static methods with writer arguments)?
    /// See: http://marcgravell.blogspot.com/2010/03/last-will-be-first-and-first-will-be.html
    /// </summary>
    public sealed class ProtoWriter : IDisposable
    {
        TypeModel model;
        StreamWrapper dest;
        byte[] _tempBuffer = BufferPool.GetBuffer();

        byte[] GetTempBuffer(int requiredMinSize)
        {
            if (_tempBuffer.Length < requiredMinSize)
                BufferPool.ResizeAndFlushLeft(ref _tempBuffer, requiredMinSize, 0, 0);
            return _tempBuffer;
        }

        /// <summary>
        /// Write an encapsulated sub-object, using the supplied unique key (reprasenting a type).
        /// </summary>
        /// <param name="value">The object to write.</param>
        /// <param name="key">The key that uniquely identifies the type within the model.</param>
        /// <param name="writer">The destination.</param>
        public static void WriteObject(object value, int key, ProtoWriter writer)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (writer == null) throw new ArgumentNullException("writer");
            if (writer.model == null)
            {
                throw new InvalidOperationException("Cannot serialize sub-objects unless a model is provided");
            }

            SubItemToken token = StartSubItem(value, writer);
            if (key >= 0)
            {
                writer.model.Serialize(key, value, writer, false);
            }
            else if (writer.model != null && writer.model.TrySerializeAuxiliaryType(writer, value.GetType(), BinaryDataFormat.Default, Serializer.ListItemTag, value, false, false))
            {
                // all ok
            }
            else
            {
                TypeModel.ThrowUnexpectedType(value.GetType());
            }
            EndSubItem(token, writer);
#endif 
        }
        /// <summary>
        /// Write an encapsulated sub-object, using the supplied unique key (reprasenting a type) - but the
        /// caller is asserting that this relationship is non-recursive; no recursion check will be
        /// performed.
        /// </summary>
        /// <param name="value">The object to write.</param>
        /// <param name="key">The key that uniquely identifies the type within the model.</param>
        /// <param name="writer">The destination.</param>
        public static void WriteRecursionSafeObject(object value, int key, ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            if (writer.model == null)
            {
                throw new InvalidOperationException("Cannot serialize sub-objects unless a model is provided");
            }
            SubItemToken token = StartSubItem(null, writer);
            writer.model.Serialize(key, value, writer, false);
            EndSubItem(token, writer);
        }

        internal static void WriteObject(object value, int key, ProtoWriter writer, PrefixStyle style, int fieldNumber)
        {
            WriteObject(value, key, writer, style, fieldNumber, false);
        }

        internal static void WriteObject(object value, int key, ProtoWriter writer, PrefixStyle style, int fieldNumber, bool isRoot)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (writer.model == null)
            {
                throw new InvalidOperationException("Cannot serialize sub-objects unless a model is provided");
            }
            if (writer.wireType != WireType.None) throw ProtoWriter.CreateException(writer);

            switch (style)
            {
                case PrefixStyle.Base128:
                    writer.wireType = WireType.String;
                    writer.fieldNumber = fieldNumber;
                    if (fieldNumber > 0) WriteHeaderCore(fieldNumber, WireType.String, writer);
                    break;
                case PrefixStyle.Fixed32:
                case PrefixStyle.Fixed32BigEndian:
                    writer.fieldNumber = 0;
                    writer.wireType = WireType.Fixed32;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("style");
            }
            SubItemToken token = StartSubItem(value, writer, true);
            if (key < 0)
            {
                if (!writer.model.TrySerializeAuxiliaryType(writer, value.GetType(), BinaryDataFormat.Default, Serializer.ListItemTag, value, false, isRoot))
                {
                    TypeModel.ThrowUnexpectedType(value.GetType());
                }
            }
            else
            {
                writer.model.Serialize(key, value, writer, isRoot);
            }
            EndSubItem(token, writer, style);
#endif       
        }

        internal int GetTypeKey(ref Type type)
        {
            return model.GetKey(ref type);
        }
        
        private readonly NetObjectCache netCache = new NetObjectCache();
        internal NetObjectCache NetCache
        {
            get { return netCache;}
        }

        private int fieldNumber, flushLock;
        WireType wireType;
        internal WireType WireType { get { return wireType; } }
        /// <summary>
        /// Writes a field-header, indicating the format of the next data we plan to write.
        /// </summary>
        public static void WriteFieldHeader(int fieldNumber, WireType wireType, ProtoWriter writer) {
            if (writer == null) throw new ArgumentNullException("writer");
            if (writer.wireType != WireType.None) throw new InvalidOperationException("Cannot write a " + wireType.ToString()
                + " header until the " + writer.wireType.ToString() + " data has been written");
            if(fieldNumber < 0) throw new ArgumentOutOfRangeException("fieldNumber");
#if DEBUG
            switch (wireType)
            {   // validate requested header-type
                case WireType.Fixed32:
                case WireType.Fixed64:
                case WireType.String:
                case WireType.StartGroup:
                case WireType.SignedVariant:
                case WireType.Variant:
                    break; // fine
                case WireType.None:
                case WireType.EndGroup:
                default:
                    throw new ArgumentException("Invalid wire-type: " + wireType.ToString(), "wireType");                
            }
#endif
            if (writer.packedFieldNumber == 0) {
                writer.fieldNumber = fieldNumber;
                writer.wireType = wireType;
                WriteHeaderCore(fieldNumber, wireType, writer);
            }
            else if (writer.packedFieldNumber == fieldNumber)
            { // we'll set things up, but note we *don't* actually write the header here
                switch (wireType)
                {
                    case WireType.Fixed32:
                    case WireType.Fixed64:
                    case WireType.Variant:
                    case WireType.SignedVariant:
                        break; // fine
                    default:
                        throw new InvalidOperationException("Wire-type cannot be encoded as packed: " + wireType.ToString());
                }
                writer.fieldNumber = fieldNumber;
                writer.wireType = wireType;
            }
            else
            {
                throw new InvalidOperationException("Field mismatch during packed encoding; expected " + writer.packedFieldNumber.ToString() + " but received " + fieldNumber.ToString());
            }
        }
        internal static void WriteHeaderCore(int fieldNumber, WireType wireType, ProtoWriter writer)
        {
            uint header = (((uint)fieldNumber) << 3)
                | (((uint)wireType) & 7);
            WriteUInt32Variant(header, writer);
        }

        /// <summary>
        /// Writes a byte-array to the stream; supported wire-types: String
        /// </summary>
        public static void WriteBytes(byte[] data, ProtoWriter writer)
        {
            if (data == null) throw new ArgumentNullException("data");
            ProtoWriter.WriteBytes(data, 0, data.Length, writer);
        }
        /// <summary>
        /// Writes a byte-array to the stream; supported wire-types: String
        /// </summary>
        public static void WriteBytes(byte[] data, int offset, int length, ProtoWriter writer)
        {
            if (data == null) throw new ArgumentNullException("data");
            if (writer == null) throw new ArgumentNullException("writer");
            switch (writer.wireType)
            {
                case WireType.Fixed32:
                    if (length != 4) throw new ArgumentException("length");
                    break;
                case WireType.Fixed64:
                    if (length != 8) throw new ArgumentException("length");
                    break;
                case WireType.String:
                    WriteUInt32Variant((uint)length, writer);
                    writer.wireType = WireType.None;
                    if (length == 0) return;
                    break;
                default:
                    throw CreateException(writer);
            }
            
            writer.dest.Write(data, offset, length);
            writer.wireType = WireType.None;
        }

        private static void CopyRawFromStream(Stream source, ProtoWriter writer)
        {
            byte[] buffer = writer.GetTempBuffer(32767);
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                writer.dest.Write(buffer, 0, read);
            }
        }

        int depth = 0;
        const int RecursionCheckDepth = 25;
        /// <summary>
        /// Indicates the start of a nested record.
        /// </summary>
        /// <param name="instance">The instance to write.</param>
        /// <param name="writer">The destination.</param>
        /// <returns>A token representing the state of the stream; this token is given to EndSubItem.</returns>
        public static SubItemToken StartSubItem(object instance, ProtoWriter writer)
        {
            return StartSubItem(instance, writer, false);
        }

        MutableList recursionStack;
        private void CheckRecursionStackAndPush(object instance)
        {
            int hitLevel;
            if (recursionStack == null) { recursionStack = new MutableList(); }
            else if (instance != null && (hitLevel = recursionStack.IndexOfReference(instance)) >= 0)
            {
#if DEBUG
                Helpers.DebugWriteLine("Stack:");
                foreach(object obj in recursionStack)
                {
                    Helpers.DebugWriteLine(obj == null ? "<null>" : obj.ToString());
                }
                Helpers.DebugWriteLine(instance == null ? "<null>" : instance.ToString());
#endif
                throw new ProtoException("Possible recursion detected (offset: " + (recursionStack.Count - hitLevel).ToString() + " level(s)): " + instance.ToString());
            }
            recursionStack.Add(instance);
        }
        private void PopRecursionStack() { recursionStack.RemoveLast(); }

        private static SubItemToken StartSubItem(object instance, ProtoWriter writer, bool allowFixed)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            if (++writer.depth > RecursionCheckDepth)
            {
                writer.CheckRecursionStackAndPush(instance);
            }
            if(writer.packedFieldNumber != 0) throw new InvalidOperationException("Cannot begin a sub-item while performing packed encoding");
            switch (writer.wireType)
            {
                case WireType.StartGroup:
                    writer.wireType = WireType.None;
                    return new SubItemToken(-writer.fieldNumber);
                case WireType.String:
#if DEBUG
                    if(writer.model != null && writer.model.ForwardsOnly)
                    {
                        throw new ProtoException("Should not be buffering data");
                    }
#endif
                    writer.wireType = WireType.None;
                    writer.flushLock++;
                    return new SubItemToken(writer.dest.CurPosition++); // leave 1 space (optimistic) for length
                case WireType.Fixed32:
                    {
                        if (!allowFixed) throw CreateException(writer);
                        SubItemToken token = new SubItemToken(writer.dest.CurPosition);
                        writer.dest.CurPosition += 4;
                        writer.wireType = WireType.None;
                        writer.flushLock++;
                        return token;
                    }
                default:
                    throw CreateException(writer);
            }
        }

        /// <summary>
        /// Indicates the end of a nested record.
        /// </summary>
        /// <param name="token">The token obtained from StartubItem.</param>
        /// <param name="writer">The destination.</param>
        public static void EndSubItem(SubItemToken token, ProtoWriter writer)
        {
            EndSubItem(token, writer, PrefixStyle.Base128);
        }
        private static void EndSubItem(SubItemToken token, ProtoWriter writer, PrefixStyle style)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            if (writer.wireType != WireType.None) { throw CreateException(writer); }
            var value = token.value;
            if (writer.depth <= 0) throw CreateException(writer);
            if (writer.depth-- > RecursionCheckDepth)
            {
                writer.PopRecursionStack();
            }
            writer.packedFieldNumber = 0; // ending the sub-item always wipes packed encoding
            if (value < 0)
            {   // group - very simple append
                WriteHeaderCore((int)(-value), WireType.EndGroup, writer);
                writer.wireType = WireType.None;
                return;
            }

            // so we're backfilling the length into an existing sequence
            int len;
            switch(style)
            {
                case PrefixStyle.Fixed32:
                    len = (int)((writer.dest.CurPosition - value) - 4);
                    WriteInt32ToBuffer(len, value, writer);
                    break;
                case PrefixStyle.Fixed32BigEndian:
                    len = (int)((writer.dest.CurPosition - value) - 4);
                    WriteInt32ToBufferBE(len, value, writer);
                    break;
                case PrefixStyle.Base128:
                    // string - complicated because we only reserved one byte;
                    // if the prefix turns out to need more than this then
                    // we need to shuffle the existing data
                    len = (int)((writer.dest.CurPosition - value) - 1);
                    int offset = 0;
                    uint tmp = (uint)len;
                    while ((tmp >>= 7) != 0) offset++;
                    if (offset == 0)
                    {
                        writer.dest[value] = (byte)(len & 0x7F);
                    }
                    else
                    {
                        var p = writer.dest.CurPosition;

                        byte[] buffer = writer._tempBuffer;
                        if (len <= buffer.Length)
                        {
                            // Helpers.BlockCopy(blob, value + 1, blob, value + 1 + offset, len);
                            writer.dest.GetBuffer(value + 1, buffer, 0, len);
                            writer.dest.PutBuffer(value + 1 + offset, buffer, 0, len);
                        }
                        else
                        {
                            const int minBlockSize = 1024 * 1024;
                            if (buffer.Length < minBlockSize)
                                buffer = writer.GetTempBuffer(minBlockSize);

                            writer.dest.CurPosition = value + 1;

                            int pos = len;

                            // need to read from end to start to not overwrite

                            while (pos > 0)
                            {
                                int totalLeft = len - (len - pos);
                                int maxLength = buffer.Length;
                                int blockSize = totalLeft > maxLength ? maxLength : totalLeft;
                                writer.dest.GetBuffer(value + 1 + pos - blockSize, buffer, 0, blockSize);
                                writer.dest.PutBuffer(value + 1 + offset + pos - blockSize, buffer, 0, blockSize);
                                pos -= blockSize;
                            }

                            // Helpers.BlockCopy(blob, value + 1, blob, value + 1 + offset, len);
                        }

                        tmp = (uint)len;
                        
                        writer.dest.CurPosition = value;
                        do
                        {
                            value++;
                            writer.dest.WriteByte((byte)((tmp & 0x7F) | 0x80));
                        } while ((tmp >>= 7) != 0);

                        writer.dest.PreviousByte = (byte)(writer.dest.PreviousByte & ~0x80);

                        writer.dest.CurPosition = p + offset;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("style");
            }

            if (--writer.flushLock == 0)
            {
                Flush(writer, true);
            }
        }

        /// <summary>
        /// Creates a new writer against a stream
        /// </summary>
        /// <param name="dest">The destination stream</param>
        /// <param name="model">The model to use for serialization; this can be null, but this will impair the ability to serialize sub-objects</param>
        /// <param name="context">Additional context about this serialization operation</param>
        public ProtoWriter(Stream dest, TypeModel model, SerializationContext context)
        {
            if (dest == null) throw new ArgumentNullException("dest");
            if (!dest.CanWrite) throw new ArgumentException("Cannot write to stream", "dest");
            //if (model == null) throw new ArgumentNullException("model");
            this.dest = new StreamWrapper(dest, true);
            this.model = model;
            this.wireType = WireType.None;
            if (context == null) { context = SerializationContext.Default; }
            else { context.Freeze(); }
            this.context = context;
            
        }

        private readonly SerializationContext context;
        /// <summary>
        /// Addition information about this serialization operation.
        /// </summary>
        public SerializationContext Context { get { return context; } }
        void IDisposable.Dispose()
        {
            Dispose();
        }
        private void Dispose()
        {   // importantly, this does **not** own the stream, and does not dispose it
            if (dest != null)
            {
                dest.Flush(false);
                dest = null;
            }
            if (_tempBuffer != null)
                BufferPool.ReleaseBufferToPool(ref _tempBuffer);
            model = null;
        }

        // note that this is used by some of the unit tests and should not be removed
        internal static int GetPosition(ProtoWriter writer) { return (int)writer.dest.BytesUsed; }
        
        /// <summary>
        /// Flushes data to the underlying stream, and releases any resources. The underlying stream is *not* disposed
        /// by this operation.
        /// </summary>
        public void Close()
        {
            if (depth != 0 || flushLock != 0) throw new InvalidOperationException("Unable to close stream in an incomplete state");
            Dispose();
        }

        internal void CheckDepthFlushlock()
        {
            if (depth != 0 || flushLock != 0) throw new InvalidOperationException("The writer is in an incomplete state");
        }

        /// <summary>
        /// Get the TypeModel associated with this writer
        /// </summary>
        public TypeModel Model { get { return model; } }
        
        /// <summary>
        /// Writes an unsigned 32-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        private static void WriteUInt32Variant(uint value, ProtoWriter writer)
        {
            int count = 0;
            do {
                writer.dest.WriteByte((byte)((value & 0x7F) | 0x80));
                count++;
            } while ((value >>= 7) != 0);

            writer.dest.PreviousByte &= 0x7F;
        }
 
        static readonly UTF8Encoding encoding = new UTF8Encoding();

        internal static uint Zig(int value)
        {        
            return (uint)((value << 1) ^ (value >> 31));
        }
        internal static ulong Zig(long value)
        {
            return (ulong)((value << 1) ^ (value >> 63));
        }
        private static void WriteUInt64Variant(ulong value, ProtoWriter writer)
        {
            int count = 0;
            do
            {
                writer.dest.WriteByte((byte)((value & 0x7F) | 0x80));
                count++;
            } while ((value >>= 7) != 0);
            writer.dest.PreviousByte &= 0x7F;
        }
        /// <summary>
        /// Writes a string to the stream; supported wire-types: String
        /// </summary>
        public static void WriteString(string value, ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            if (writer.wireType != WireType.String) throw CreateException(writer);
            if (value == null) throw new ArgumentNullException("value"); // written header; now what?
            int len = value.Length;
            if (len == 0)
            {
                WriteUInt32Variant(0, writer);
                writer.wireType = WireType.None;
                return; // just a header
            }
#if MF
            byte[] bytes = encoding.GetBytes(value);
            int actual = bytes.Length;
            writer.WriteUInt32Variant((uint)actual);
            writer.Ensure(actual);
            Helpers.BlockCopy(bytes, 0, writer.ioBuffer, writer.CurPosition, actual);
#else
            int predicted = encoding.GetByteCount(value);
            WriteUInt32Variant((uint)predicted, writer);
            byte[] buffer = writer.GetTempBuffer(predicted);
            int actual = encoding.GetBytes(value, 0, value.Length, buffer, 0);
            writer.dest.Write(buffer, 0, actual);
            Helpers.DebugAssert(predicted == actual);
#endif
            writer.wireType = WireType.None;
        }
        /// <summary>
        /// Writes an unsigned 64-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public static void WriteUInt64(ulong value, ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            switch (writer.wireType)
            {
                case WireType.Fixed64:
                    ProtoWriter.WriteInt64((long)value, writer);
                    return;
                case WireType.Variant:
                    WriteUInt64Variant(value, writer);
                    writer.wireType = WireType.None;
                    return;
                case WireType.Fixed32:
                    checked { ProtoWriter.WriteUInt32((uint)value, writer); }
                    return;
                default:
                    throw CreateException(writer);
            }
        }

        /// <summary>
        /// Writes a signed 64-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public static void WriteInt64(long value, ProtoWriter writer)
        {
            int index;
            if (writer == null) throw new ArgumentNullException("writer");
            var dest = writer.dest;
            byte[] buffer;
            switch (writer.wireType)
            {
                case WireType.Fixed64:

                    buffer = writer.GetTempBuffer(8);
                    buffer[0] = (byte)value;
                    buffer[1] = (byte)(value >> 8);
                    buffer[2] = (byte)(value >> 16);
                    buffer[3] = (byte)(value >> 24);
                    buffer[4] = (byte)(value >> 32);
                    buffer[5] = (byte)(value >> 40);
                    buffer[6] = (byte)(value >> 48);
                    buffer[7] = (byte)(value >> 56);

                    dest.Write(buffer, 0, 8);

                    writer.wireType = WireType.None;
                    return;
                case WireType.SignedVariant:
                    WriteUInt64Variant(Zig(value), writer);
                    writer.wireType = WireType.None;
                    return;
                case WireType.Variant:
                    if (value >= 0)
                    {
                        WriteUInt64Variant((ulong)value, writer);
                        writer.wireType = WireType.None;
                    }
                    else
                    {
                        buffer = writer.GetTempBuffer(10);
                        buffer[0] = (byte)(value | 0x80);
                        buffer[1] = (byte)((int)(value >> 7) | 0x80);
                        buffer[2] = (byte)((int)(value >> 14) | 0x80);
                        buffer[3] = (byte)((int)(value >> 21) | 0x80);
                        buffer[4] = (byte)((int)(value >> 28) | 0x80);
                        buffer[5] = (byte)((int)(value >> 35) | 0x80);
                        buffer[6] = (byte)((int)(value >> 42) | 0x80);
                        buffer[7] = (byte)((int)(value >> 49) | 0x80);
                        buffer[8] = (byte)((int)(value >> 56) | 0x80);
                        buffer[9] = 0x01; // sign bit

                        dest.Write(buffer, 0, 10);

                        writer.wireType = WireType.None;
                    }
                    return;
                case WireType.Fixed32:
                    checked { WriteInt32((int)value, writer); }
                    return;
                default:
                    throw CreateException(writer);
            }
        }

        /// <summary>
        /// Writes an unsigned 16-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public static void WriteUInt32(uint value, ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            switch (writer.wireType)
            {
                case WireType.Fixed32:
                    ProtoWriter.WriteInt32((int)value, writer);
                    return;
                case WireType.Fixed64:
                    ProtoWriter.WriteInt64((int)value, writer);
                    return;
                case WireType.Variant:
                    WriteUInt32Variant(value, writer);
                    writer.wireType = WireType.None;
                    return;
                default:
                    throw CreateException(writer);
            }
        }


        /// <summary>
        /// Writes a signed 16-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public static void WriteInt16(short value, ProtoWriter writer)
        {
            ProtoWriter.WriteInt32(value, writer);
        }
        /// <summary>
        /// Writes an unsigned 16-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public static void WriteUInt16(ushort value, ProtoWriter writer)
        {
            ProtoWriter.WriteUInt32(value, writer);
        }
        /// <summary>
        /// Writes an unsigned 8-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public static void WriteByte(byte value, ProtoWriter writer)
        {
            ProtoWriter.WriteUInt32(value, writer);
        }
        /// <summary>
        /// Writes a signed 8-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public static void WriteSByte(sbyte value, ProtoWriter writer)
        {
            ProtoWriter.WriteInt32(value, writer);
        }
        
        private static void WriteInt32ToBuffer(int value, long position, ProtoWriter writer)
        {
            var p = writer.dest.CurPosition;
            writer.dest.CurPosition = position;
            WriteInt32ToBuffer(value, writer);
            writer.dest.CurPosition = p;
        }

        private static void WriteInt32ToBuffer(int value, ProtoWriter writer)
        {
            var buffer = writer.GetTempBuffer(4);
            buffer[0] = (byte)value;
            buffer[1] = (byte)(value >> 8);
            buffer[2] = (byte)(value >> 16);
            buffer[3] = (byte)(value >> 24);
            writer.dest.Write(buffer, 0, 4);
        }

        private static void WriteInt32ToBufferBE(int value, long position, ProtoWriter writer)
        {
            var buffer = writer.GetTempBuffer(4);
            buffer[3] = (byte)value;
            buffer[2] = (byte)(value >> 8);
            buffer[1] = (byte)(value >> 16);
            buffer[0] = (byte)(value >> 24);
            writer.dest.PutBuffer(position, buffer, 0, 4);
        }

        /// <summary>
        /// Writes a signed 32-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public static void WriteInt32(int value, ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            var dest = writer.dest;
            byte[] buffer;
            switch (writer.wireType)
            {
                case WireType.Fixed32:
                    WriteInt32ToBuffer(value, writer);
                    writer.wireType = WireType.None;
                    return;
                case WireType.Fixed64:
                    buffer = writer.GetTempBuffer(8);

                    buffer[0] = (byte)value;
                    buffer[1] = (byte)(value >> 8);
                    buffer[2] = (byte)(value >> 16);
                    buffer[3] = (byte)(value >> 24);
                    buffer[4] = buffer[5] =
                                buffer[6] = buffer[7] = 0;

                    dest.Write(buffer, 0, 8);

                    writer.wireType = WireType.None;
                    return;
                case WireType.SignedVariant:
                    WriteUInt32Variant(Zig(value), writer);
                    writer.wireType = WireType.None;
                    return;
                case WireType.Variant:
                    if (value >= 0)
                    {
                        WriteUInt32Variant((uint)value, writer);
                        writer.wireType = WireType.None;
                    }
                    else
                    {
                        buffer = writer.GetTempBuffer(10);
                        buffer[0] = (byte)(value | 0x80);
                        buffer[1] = (byte)((value >> 7) | 0x80);
                        buffer[2] = (byte)((value >> 14) | 0x80);
                        buffer[3] = (byte)((value >> 21) | 0x80);
                        buffer[4] = (byte)((value >> 28) | 0x80);
                        buffer[5] = buffer[6] =
                                    buffer[7] = buffer[8] = (byte)0xFF;
                        buffer[9] = (byte)0x01;

                        dest.Write(buffer, 0, 10);
                        writer.wireType = WireType.None;
                    }
                    return;
                default:
                    throw CreateException(writer);
            }
            
        }
        /// <summary>
        /// Writes a double-precision number to the stream; supported wire-types: Fixed32, Fixed64
        /// </summary>
        public
#if !FEAT_SAFE
            unsafe
#endif

                static void WriteDouble(double value, ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            switch (writer.wireType)
            {
                case WireType.Fixed32:
                    float f = (float)value;
                    if (Helpers.IsInfinity(f)
                        && !Helpers.IsInfinity(value))
                    {
                        throw new OverflowException();
                    }
                    ProtoWriter.WriteSingle(f, writer);
                    return;
                case WireType.Fixed64:
#if FEAT_SAFE
                    ProtoWriter.WriteInt64(BitConverter.ToInt64(BitConverter.GetBytes(value), 0), writer);
#else
                    ProtoWriter.WriteInt64(*(long*)&value, writer);
#endif
                    return;
                default:
                    throw CreateException(writer);
            }
        }
        /// <summary>
        /// Writes a single-precision number to the stream; supported wire-types: Fixed32, Fixed64
        /// </summary>
        public 
#if !FEAT_SAFE
            unsafe
#endif
            static void WriteSingle(float value, ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            switch (writer.wireType)
            {
                case WireType.Fixed32:
#if FEAT_SAFE
                    ProtoWriter.WriteInt32(BitConverter.ToInt32(BitConverter.GetBytes(value), 0), writer);
#else
                    ProtoWriter.WriteInt32(*(int*)&value, writer);
#endif
                    return;
                case WireType.Fixed64:
                    ProtoWriter.WriteDouble((double)value, writer);
                    return;
                default:
                    throw CreateException(writer);
            }
        }
        /// <summary>
        /// Throws an exception indicating that the given enum cannot be mapped to a serialized value.
        /// </summary>
        public static void ThrowEnumException(ProtoWriter writer, object enumValue)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            string rhs = enumValue == null ? "<null>" : (enumValue.GetType().FullName + "." + enumValue.ToString());
            throw new ProtoException("No wire-value is mapped to the enum " + rhs + " at position " + writer.dest.BytesUsed.ToString());
        }
        // general purpose serialization exception message
        internal static Exception CreateException(ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            return new ProtoException("Invalid serialization operation with wire-type " + writer.wireType.ToString() + " at position " + writer.dest.BytesUsed.ToString());
        }

        /// <summary>
        /// Writes a boolean to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public static void WriteBoolean(bool value, ProtoWriter writer)
        {
            ProtoWriter.WriteUInt32(value ? (uint)1 : (uint)0, writer);
        }

        /// <summary>
        /// Copies any extension data stored for the instance to the underlying stream
        /// </summary>
        public static void AppendExtensionData(IExtensible instance, ProtoWriter writer)
        {
            if (instance == null) throw new ArgumentNullException("instance");
            if (writer == null) throw new ArgumentNullException("writer");
            // we expect the writer to be raw here; the extension data will have the
            // header detail, so we'll copy it implicitly
            if(writer.wireType != WireType.None) throw CreateException(writer);

            IExtension extn = instance.GetExtensionObject(false);
            if (extn != null)
            {
                // unusually we *don't* want "using" here; the "finally" does that, with
                // the extension object being responsible for disposal etc
                Stream source = extn.BeginQuery();
                try
                {
                    CopyRawFromStream(source, writer);
                }
                finally { extn.EndQuery(source); }
            }
        }


        private int packedFieldNumber;
        /// <summary>
        /// Used for packed encoding; indicates that the next field should be skipped rather than
        /// a field header written. Note that the field number must match, else an exception is thrown
        /// when the attempt is made to write the (incorrect) field. The wire-type is taken from the
        /// subsequent call to WriteFieldHeader. Only primitive types can be packed.
        /// </summary>
        public static void SetPackedField(int fieldNumber, ProtoWriter writer)
        {
            if (fieldNumber <= 0) throw new ArgumentOutOfRangeException("fieldNumber");
            if (writer == null) throw new ArgumentNullException("writer");
            writer.packedFieldNumber = fieldNumber;
        }

        internal string SerializeType(System.Type type)
        {
            return TypeModel.SerializeType(model, type);
        }
        /// <summary>
        /// Specifies a known root object to use during reference-tracked serialization
        /// </summary>
        public void SetRootObject(object value)
        {
            //NetCache.SetKeyedObject(NetObjectCache.Root, value);
        }

        /// <summary>
        /// Writes a Type to the stream, using the model's DynamicTypeFormatting if appropriate; supported wire-types: String
        /// </summary>
        public static void WriteType(System.Type value, ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            WriteString(writer.SerializeType(value), writer);
        }

        public static void Flush(ProtoWriter writer)
        {
            Flush(writer, false);
        }

        public static void Flush(ProtoWriter writer, bool onlyWhenSize)
        {
            if (writer.flushLock == 0)
                writer.dest.Flush(onlyWhenSize);
        }

    }
}
