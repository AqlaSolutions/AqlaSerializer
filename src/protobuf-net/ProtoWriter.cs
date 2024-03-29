﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using AltLinq; using System.Linq;
using AqlaSerializer.Meta;
using AqlaSerializer.Serializers;

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
        internal Stream UnderlyingStream => _dest;

        private Stream _dest;
        TypeModel _model;

        byte[] _tempBuffer = BufferPool.GetBuffer();

        byte[] GetTempBuffer(int requiredMinSize)
        {
            if (_tempBuffer.Length < requiredMinSize)
                BufferPool.ResizeAndFlushLeft(ref _tempBuffer, requiredMinSize, 0, 0);
            return _tempBuffer;
        }

        LateReferencesCache _lateReferences = new LateReferencesCache();

        public static void NoteLateReference(int typeKey, object value, ProtoWriter writer)
        {
#if DEBUG
            Debug.Assert(value != null);
            Debug.Assert(ReferenceEquals(value, writer._netCache.LastNewValue));
#endif
            writer._lateReferences.AddLateReference(new LateReferencesCache.LateReference(typeKey, value, writer._netCache.LastNewKey));
        }

        public static bool TryGetNextLateReference(out int typeKey, out object value, out int referenceKey, ProtoWriter writer)
        {
            var r = writer._lateReferences.TryGetNextLateReference();
            if (r == null)
            {
                typeKey = 0;
                value = null;
                referenceKey = 0;
                return false;
            }
            typeKey = r.Value.TypeKey;
            value = r.Value.Value;
            referenceKey = r.Value.ReferenceKey;
            return true;
        }

        /// <summary>
        /// Write an encapsulated sub-object, using the supplied unique key (reprasenting a type).
        /// </summary>
        /// <param name="value">The object to write.</param>
        /// <param name="key">The key that uniquely identifies the type within the model.</param>
        /// <param name="prefixLength">See <see cref="string"/> (for true) and <see cref="WireType.StartGroup"/> (for false)</param>
        /// <param name="writer">The destination.</param>
        public static void WriteObject(object value, int key, bool prefixLength, ProtoWriter writer)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (writer._model == null)
            {
                throw new InvalidOperationException("Cannot serialize sub-objects unless a model is provided");
            }

            if (key >= 0)
            {
                writer._model.Serialize(key, value, writer, false);
            }
            else if (writer._model != null)
            {
                SubItemToken token = StartSubItem(value, prefixLength, writer);
                if (writer._model.TrySerializeAuxiliaryType(writer, value.GetType(), BinaryDataFormat.Default, Serializer.ListItemTag, value, false, false))
                {
                    // all ok
                }
                else
                {
                    TypeModel.ThrowUnexpectedType(value.GetType());
                }
                EndSubItem(token, writer);
            }
            else
            {
                TypeModel.ThrowUnexpectedType(value.GetType());
            }
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
            // no argument checks here for performance
            writer._model.Serialize(key, value, writer, false);
        }

        //internal static void WriteAuxiliaryObject(int tag, object value, ProtoWriter writer)
        //{
        //    writer._model.TrySerializeAuxiliaryType(writer, value.GetType(), BinaryDataFormat.Default, tag, value, false, false);
        //}

        // not used anymore because we don't want aux on members

        internal static void WriteObject(object value, int key, ProtoWriter writer, PrefixStyle style, int fieldNumber)
        {
            WriteObject(value, key, writer, style, fieldNumber, false);
        }

        internal static void WriteObject(object value, int key, ProtoWriter writer, PrefixStyle style, int fieldNumber, bool isRoot)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (writer._model == null)
            {
                throw new InvalidOperationException("Cannot serialize sub-objects unless a model is provided");
            }
            if (writer._wireType != WireType.None || writer._fieldStarted) throw ProtoWriter.CreateException(writer);

            switch (style)
            {
                case PrefixStyle.Base128:
                    writer._wireType = WireType.String;
                    writer._fieldNumber = fieldNumber;
                    if (fieldNumber > 0) WriteHeaderCore(fieldNumber, WireType.String, writer);
                    break;
                case PrefixStyle.Fixed32:
                case PrefixStyle.Fixed32BigEndian:
                    writer._fieldNumber = 0;
                    writer._wireType = WireType.Fixed32;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(style));
            }

            SubItemToken token = StartSubItem(value, writer, true);
            if (key < 0)
            {
                if (!writer._model.TrySerializeAuxiliaryType(writer, value.GetType(), BinaryDataFormat.Default, Serializer.ListItemTag, value, false, isRoot))
                {
                    TypeModel.ThrowUnexpectedType(value.GetType());
                }
            }
            else
            {
                writer._model.Serialize(key, value, writer, isRoot);
            }
            EndSubItem(token, writer, style);
#endif
        }

        internal int GetTypeKey(ref Type type)
        {
            return _model.GetKey(ref type);
        }

        private NetObjectCache _netCache = new NetObjectCache();
        internal NetObjectCache NetCache
        {
            get { return _netCache;}
        }

        internal NetObjectKeyPositionsList NetCacheKeyPositionsList { get; } = new NetObjectKeyPositionsList();

        private int _fieldNumber, _flushLock;
        WireType _wireType;
        public WireType WireType => _wireType;
        public int FieldNumber => _fieldNumber;

        public bool IsExpectingRoot => _expectRoot;

        bool _expectRoot;
        bool _expectRootType;

        public bool TakeIsExpectingRootType()
        {
            var r = _expectRootType;
            _expectRootType = false;
            return r;
        }

        /// <summary>
        /// Next StartSubItem call will be ignored unless WriteFieldHeader is called
        /// </summary>
        public static void ExpectRoot(ProtoWriter writer)
        {
            writer._expectRoot = true;
        }

        /// <summary>
        /// Root type should write length-prefixed data
        /// </summary>
        /// <param name="writer"></param>
        public static void ExpectRootType(ProtoWriter writer)
        {
            writer._expectRootType = true;
        }

        public bool HasIncompleteField => _fieldStarted;

        bool _fieldStarted;
        bool _ignoredFieldStarted;

        // TODO compiler optimization to merge two consequence calls start-complete

        /// <summary>
        /// Indicates that the next WriteFieldHeaderComplete call should be ignored
        /// </summary>
        /// <param name="writer"></param>
        public static void WriteFieldHeaderBeginIgnored(ProtoWriter writer)
        {
            WriteFieldHeaderBegin(ProtoReader.GroupNumberForIgnoredFields, writer);
            writer._ignoredFieldStarted = true;
        }

        /// <summary>
        /// Starts writing a field-header
        /// </summary>
        public static void WriteFieldHeaderBegin(int fieldNumber, ProtoWriter writer)
        {
            if (writer._wireType != WireType.None)
                throw new InvalidOperationException(
                    "Cannot write a field number " + fieldNumber
                    + " until the " + writer._wireType.ToString() + " data has been written");

            if (writer._fieldStarted) throw new InvalidOperationException("Cannot write a field number until a wire type for field " + writer._fieldNumber + " has been written");
            writer._expectRoot = false;
            writer._fieldNumber = fieldNumber;
            writer._fieldStarted = true;
        }

        /// <summary>
        /// Finished writing a field-header, indicating the format of the next data we plan to write.
        /// </summary>
        public static void WriteFieldHeaderComplete(WireType wireType, ProtoWriter writer)
        {
#if DEBUG
            if (wireType == WireType.StartGroup) throw new InvalidOperationException("Should use StartSubItem method for nested items");
#endif
            WriteFieldHeaderCompleteAnyType(wireType, writer);
        }

        /// <summary>
        /// Cancels writing a field-header, initiated with WriteFieldHeaderBegin
        /// </summary>
        public static void WriteFieldHeaderCancelBegin(ProtoWriter writer)
        {
            if (!writer._fieldStarted) throw CreateException(writer);
            writer._fieldNumber = 0;
            writer._fieldStarted = false;
            writer._ignoredFieldStarted = false;
        }

        /// <summary>
        /// Finished writing a field-header, indicating the format of the next data we plan to write. Any type means nested objects are allowed.
        /// </summary>
        public static void WriteFieldHeaderCompleteAnyType(WireType wireType, ProtoWriter writer)
        {
            if (!writer._fieldStarted) throw new InvalidOperationException("Cannot write a field wire type " + wireType + " because field number has not been written");
            writer._wireType = wireType;
            writer._fieldStarted = false;
            if (writer._ignoredFieldStarted)
            {
                writer._ignoredFieldStarted = false;
                return;
            }
            WriteFieldHeaderNoCheck(writer._fieldNumber, wireType, writer);
        }

        /// <summary>
        /// Starts field header without writing it
        /// </summary>
        public static void WriteFieldHeaderIgnored(WireType wireType, ProtoWriter writer)
        {
            if (writer._fieldStarted)
                throw new InvalidOperationException("Cannot write a field header until a wire type of the field " + writer._fieldNumber + " has been written");
            if (writer._wireType != WireType.None)
                throw new InvalidOperationException("Cannot write a field header until the " + writer._wireType.ToString() + " data has been written");

            Debug.Assert(wireType != WireType.None);

            writer._expectRoot = false;
            writer._wireType = wireType;
            writer._fieldNumber = ProtoReader.GroupNumberForIgnoredFields;
        }

        /// <summary>
        /// Writes a field-header, indicating the format of the next data we plan to write.
        /// </summary>
        public static void WriteFieldHeader(int fieldNumber, WireType wireType, ProtoWriter writer)
        {
#if DEBUG
            if (wireType == WireType.StartGroup) throw new InvalidOperationException("Should use StartSubItem method for nested items");
#endif
            WriteFieldHeaderAnyType(fieldNumber, wireType, writer);
        }

        /// <summary>
        /// Writes a field-header, indicating the format of the next data we plan to write. Any type means nested objects are allowed.
        /// </summary>
        public static void WriteFieldHeaderAnyType(int fieldNumber, WireType wireType, ProtoWriter writer) {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (writer._wireType != WireType.None) throw new InvalidOperationException("Cannot write a " + wireType.ToString()
                + " header until the " + writer._wireType.ToString() + " data has been written");
            if(fieldNumber < 0) throw new ArgumentOutOfRangeException(nameof(fieldNumber));
            if (writer._fieldStarted) throw new InvalidOperationException("Cannot write a field until a wire type for field " + writer._fieldNumber + " has been written");
            WriteFieldHeaderNoCheck(fieldNumber, wireType, writer);
        }

        static void WriteFieldHeaderNoCheck(int fieldNumber, WireType wireType, ProtoWriter writer) {
#if DEBUG
            switch (wireType)
            {   // validate requested header-type
                case WireType.Null:
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
                    throw new ArgumentException("Invalid wire-type: " + wireType.ToString(), nameof(wireType));
            }
#endif
            writer._expectRoot = false;
            writer._fieldNumber = fieldNumber;
            if (wireType != WireType.Null)
                writer._wireType = wireType;
            else
                writer._wireType = WireType.None;
            WriteHeaderCore(fieldNumber, wireType, writer);
        }
        internal static void WriteHeaderCore(int fieldNumber, WireType wireType, ProtoWriter writer)
        {
            uint header = (((uint)fieldNumber) << 3)
                | (((uint)wireType) & 7);
            WriteUInt64Variant(header, writer);
        }

        /// <summary>
        /// Writes a byte-array to the stream; supported wire-types: String
        /// </summary>
        public static void WriteBytes(byte[] data, ProtoWriter writer)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            ProtoWriter.WriteBytes(data, 0, data.Length, writer);
        }
        /// <summary>
        /// Writes a byte-array to the stream; supported wire-types: String
        /// </summary>
        public static void WriteBytes(byte[] data, int offset, int length, ProtoWriter writer)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            switch (writer._wireType)
            {
                case WireType.Fixed32:
                    if (length != 4) throw new ArgumentException("length");
                    goto CopyFixedLength;  // ugly but effective
                case WireType.Fixed64:
                    if (length != 8) throw new ArgumentException("length");
                    goto CopyFixedLength;  // ugly but effective
                case WireType.String:
                    WriteUInt32Variant((uint)length, writer);
                    writer._wireType = WireType.None;
                    if (length == 0) return;
                    if (length <= writer._ioBuffer.Length || !writer.IsFlushAdvised(writer._ioIndex + length)) // write to the buffer
                    {
                        goto CopyFixedLength; // ugly but effective
                    }
                    // writing data that is bigger than the buffer (and the buffer
                    // isn't currently locked due to a sub-object needing the size backfilled)
                    Flush(writer); // commit any existing data from the buffer
                    // now just write directly to the underlying stream
                    writer._dest.Write(data, offset, length);
                    writer._position64 += length; // since we've flushed offset etc is 0, and remains
                                        // zero since we're writing directly to the stream
                    return;
            }
            throw CreateException(writer);
        CopyFixedLength: // no point duplicating this lots of times, and don't really want another stackframe
            DemandSpace(length, writer);
            Helpers.BlockCopy(data, offset, writer._ioBuffer, writer._ioIndex, length);
            IncrementedAndReset(length, writer);
        }
        private static void CopyRawFromStream(Stream source, ProtoWriter writer)
        {
            byte[] buffer = writer._ioBuffer;
            int space = buffer.Length - writer._ioIndex, bytesRead = 1; // 1 here to spoof case where already full

            // try filling the buffer first
            while (space > 0 && (bytesRead = source.Read(buffer, writer._ioIndex, space)) > 0)
            {
                writer._ioIndex += bytesRead;
                writer._position64 += bytesRead;
                space -= bytesRead;
            }
            if (bytesRead <= 0) return; // all done using just the buffer; stream exhausted

            // at this point the stream still has data, but buffer is full;
            if (writer._streamAsBufferAllowed || writer._flushLock == 0)
            {
                // flush the buffer and write to the underlying stream instead
                Flush(writer);
                while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    writer._dest.Write(buffer, 0, bytesRead);
                    writer._position64 += bytesRead;
                }
            }
            else
            {
                do
                {
                    // need more space; resize (double) as necessary,
                    // requesting a reasonable minimum chunk each time
                    // (128 is the minimum; there may actually be much
                    // more space than this in the buffer)
                    DemandSpace(128, writer);
                    if((bytesRead = source.Read(writer._ioBuffer, writer._ioIndex,
                        writer._ioBuffer.Length - writer._ioIndex)) <= 0) break;
                    writer._position64 += bytesRead;
                    writer._ioIndex += bytesRead;
                } while (true);
            }

        }
        private static void IncrementedAndReset(int length, ProtoWriter writer)
        {
            Helpers.DebugAssert(length >= 0);
            writer._ioIndex += length;
            writer._position64 += length;
            writer._wireType = WireType.None;
        }
        int _depth = 0;
        const int RecursionCheckDepth = 25;

        /// <summary>
        /// Indicates the start of a nested record of specified type when fieldNumber has been written.
        /// </summary>
        /// <param name="instance">The instance to write.</param>
        /// <param name="prefixLength">See <see cref="string"/> (for true) and <see cref="WireType.StartGroup"/> (for false)</param>
        /// <param name="writer">The destination.</param>
        /// <returns>A token representing the state of the stream; this token is given to EndSubItem.</returns>
        public static SubItemToken StartSubItem(object instance, bool prefixLength, ProtoWriter writer)
        {
            if (writer._expectRoot)
            {
                writer._expectRoot = false;
                return new SubItemToken(int.MinValue);
            }
            // ignored field does affect only field header
            // but subitem information is considered content
            // it should be started properly
            WriteFieldHeaderCompleteAnyType(prefixLength ? WireType.String : WireType.StartGroup, writer);
            return StartSubItem(instance, writer, false);
        }

        /// <summary>
        /// Indicates the start of a nested record of specified type when fieldNumber *AND* wireType has been written.
        /// </summary>
        /// <param name="instance">The instance to write.</param>
        /// <param name="writer">The destination.</param>
        /// <returns>A token representing the state of the stream; this token is given to EndSubItem.</returns>
        public static SubItemToken StartSubItemWithoutWritingHeader(object instance, ProtoWriter writer)
        {
            // "ignored" is not checked here because field header is already fully written
            return StartSubItem(instance, writer, false);
        }

        /// <summary>
        /// Indicates the start of a nested record of specified type when fieldNumber has not been written before.
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <param name="instance">The instance to write.</param>
        /// <param name="prefixLength">See <see cref="string"/> (for true) and <see cref="WireType.StartGroup"/> (for false)</param>
        /// <param name="writer">The destination.</param>
        /// <returns>A token representing the state of the stream; this token is given to EndSubItem.</returns>
        public static SubItemToken StartSubItem(int fieldNumber, object instance, bool prefixLength, ProtoWriter writer)
        {
            // "ignored" is not checked here because field header is being written from scratch
            WriteFieldHeaderAnyType(fieldNumber, prefixLength ? WireType.String : WireType.StartGroup, writer);

            return StartSubItem(instance, writer, false);
        }

        MutableList _recursionStack;
        private void CheckRecursionStackAndPush(object instance)
        {
            int hitLevel;
            if (_recursionStack == null) { _recursionStack = new MutableList(); }
            else if (instance != null)
            {
                if (_recursionStack.HasReferences(instance, 5))
                {
                    hitLevel = _recursionStack.IndexOfReference(instance);
#if DEBUG
                    Helpers.DebugWriteLine("Stack:");
                    foreach(object obj in _recursionStack)
                    {
                        Helpers.DebugWriteLine(obj == null ? "<null>" : obj.ToString());
                    }
                    Helpers.DebugWriteLine(instance == null ? "<null>" : instance.ToString());
#endif
                    throw new ProtoException("Possible recursion detected (offset: " + (_recursionStack.Count - hitLevel).ToString() + " level(s)): " + instance.ToString());
                }
            }
            _recursionStack.Add(instance);
        }
        private void PopRecursionStack() { _recursionStack.RemoveLast(); }

        public static bool CheckIsOnHalfToRecursionDepthLimit(ProtoWriter writer)
        {
            return writer._depth > (writer._model?.RecursionDepthLimit ?? TypeModel.DefaultRecursionDepthLimit) / 2;
        }

        private static SubItemToken StartSubItem(object instance, ProtoWriter writer, bool allowFixed)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (++writer._depth > RecursionCheckDepth)
            {
                writer.CheckRecursionStackAndPush(instance);
                if (writer._depth > (writer._model?.RecursionDepthLimit ?? TypeModel.DefaultRecursionDepthLimit)) TypeModel.ThrowRecursionDepthLimitExceeded(writer._recursionStack);
            }
            writer._expectRoot = false;
            switch (writer._wireType)
            {
                case WireType.StartGroup:
                    writer._wireType = WireType.None;
                    return new SubItemToken((long)(-writer._fieldNumber));
                case WireType.String:
#if DEBUG
                    if(writer._model != null && writer._model.ForwardsOnly)
                    {
                        throw new ProtoException("Should not be buffering data: " + instance ?? "(null)");
                    }
#endif
                    writer._wireType = WireType.None;
                    DemandSpace(32, writer); // make some space in anticipation...
                    writer._flushLock++;
                    writer._ioIndex++;
                    return new SubItemToken(writer._position64++); // leave 1 space (optimistic) for length
                case WireType.Fixed32:
                    {
                        if (!allowFixed) throw CreateException(writer);
                        DemandSpace(32, writer); // make some space in anticipation...
                        writer._flushLock++;
                        SubItemToken token = new SubItemToken(writer._position64);
                        ProtoWriter.IncrementedAndReset(4, writer); // leave 4 space (rigid) for length
                        return token;
                    }
                default:
                    throw CreateException(writer);
            }
        }

        void DebugFlushFile()
        {
            bool prev = _streamAsBufferAllowed;
            _streamAsBufferAllowed = true;
            Flush(this);
            _streamAsBufferAllowed = prev;
            _dest.Flush();
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
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (writer._fieldStarted) { throw CreateException(writer); }
            if (token.Value64 == int.MinValue)
            {
                writer._wireType = WireType.None;
                return;
            }
            if (writer._wireType != WireType.None) { throw CreateException(writer); }
            long value = token.Value64;
            if (writer._depth <= 0) throw CreateException(writer);
            if (writer._depth-- > RecursionCheckDepth)
            {
                writer.PopRecursionStack();
            }
            if (value < 0)
            {   // group - very simple append
                var cancel = token.SeekOnEndOrMakeNullField;
                if (cancel?.PositionShouldBeEqualTo == writer._position64)
                {
                    if (cancel.Value.ThenTrySeekToPosition != null && writer.TrySeek(cancel.Value.ThenTrySeekToPosition.Value))
                    {
                        writer._wireType = WireType.None;
                        return;
                    }

                    if (cancel?.NullFieldNumber is int field)
                    {
                        writer._wireType = WireType.None;
                        WriteFieldHeader(field, WireType.Null, writer);
                        return;
                    }
                }
                WriteHeaderCore(-(int)value, WireType.EndGroup, writer);
                writer._wireType = WireType.None;
                return;
            }

            long flushed = writer._position64 - writer._ioIndex;
            Debug.Assert(flushed >= 0, "Position should be always bigger or equal to ioIndex (it's total written length including flushed)");

            long inBufferPos = value - flushed;

            byte[] buffer;

            // so we're backfilling the length into an existing sequence
            // should operate on buffer?
            if (inBufferPos >= 0)
            {
                int positionDiff = checked((int)(writer._position64 - value));
                int len;
                switch (style)
                {
                case PrefixStyle.Fixed32:
                        len = (int)(positionDiff - 4);
                        ProtoWriter.WriteInt32ToBuffer(len, writer._ioBuffer, (int) inBufferPos);
                    break;
                case PrefixStyle.Fixed32BigEndian:
                        len = (int)(positionDiff - 4);
                        buffer = writer._ioBuffer;
                        ProtoWriter.WriteInt32ToBufferBE(len, buffer, (int) inBufferPos);
                    break;
                case PrefixStyle.Base128:
                    // string - complicated because we only reserved one byte;
                    // if the prefix turns out to need more than this then
                    // we need to shuffle the existing data
                        len = (int)(positionDiff - 1);
                        int offset = 0;
                        uint tmp = (uint)len;
                        while ((tmp >>= 7) != 0) offset++;
                        if (offset == 0)
                        {
                            writer._ioBuffer[inBufferPos] = (byte)(len & 0x7F);
                        }
                        else
                        {
                            DemandSpace(offset, writer);
                            byte[] blob = writer._ioBuffer;
                            Helpers.BlockCopy(blob, (int) (inBufferPos + 1), blob, (int) (inBufferPos + 1 + offset), len);
                            tmp = (uint)len;
                            do
                            {
                                blob[inBufferPos++] = (byte)((tmp & 0x7F) | 0x80);
                            } while ((tmp >>= 7) != 0);
                            blob[inBufferPos - 1] = (byte)(blob[inBufferPos - 1] & ~0x80);
                            writer._position64 += offset;
                            writer._ioIndex += offset;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(style));
                }
            }
            else // do the same thing but for stream
            {
                long positionDiff = writer._position64 - value;

                byte[] temp = null;
                int writeFromTemp = 0;
                var dest = writer._dest;
                var prevPos = dest.Position;

                switch (style)
                {
                    case PrefixStyle.Fixed32:
                    {
                        writeFromTemp = 4;
                        int len = checked((int)(positionDiff - writeFromTemp));
                        temp = writer.GetTempBuffer(writeFromTemp);
                        ProtoWriter.WriteInt32ToBuffer(len, temp, 0);
                    }
                    break;
                    case PrefixStyle.Fixed32BigEndian:
                    {
                        writeFromTemp = 4;
                        int len = checked((int)((int)(positionDiff - writeFromTemp)));
                        temp = writer.GetTempBuffer(writeFromTemp);
                        ProtoWriter.WriteInt32ToBufferBE(len, temp, 0);
                    }
                    break;
                    case PrefixStyle.Base128:
                    {
                        // string - complicated because we only reserved one byte;
                        // if the prefix turns out to need more than this then
                        // we need to shuffle the existing data

                        // hack: len up to long
                        long len = positionDiff - 1;
                        int offset = 0;
                        ulong tmp = (ulong)len;
                        while ((tmp >>= 7) != 0) offset++;
                        if (offset == 0)
                        {
                            dest.Position += inBufferPos; // negative
                            dest.WriteByte((byte)(len & 0x7F));
                        }
                        else
                        {
                            DemandSpace(offset, writer);
                            buffer = writer._ioBuffer;
                            if (inBufferPos == -1)
                                Helpers.BlockCopy(buffer, 0, buffer, offset, checked((int)len)); // must be ok if pos is just -1
                            else
                            {
                                // move data from inBufferPos + 1 to inBufferPos + 1 + offset
                                long left = len;
                                long blockInBufferPos = inBufferPos + 1; // negative
                                // first - in memory
                                long inMemoryWas = len + blockInBufferPos;
                                if (inMemoryWas > 0)
                                {
                                    Helpers.BlockCopy(buffer, 0, buffer, offset, (int)inMemoryWas); // in memory should not be more than int.MaxValue
                                    left -= inMemoryWas;
                                }

                                int inMemoryDataStart = offset;

                                // second - from stream to stream (and memory)
                                int maxBlockSize = (int)Math.Min(left, 1024 * 1024);
                                temp = writer.GetTempBuffer(maxBlockSize);
                                long streamPosition = dest.Position;
                                long streamEndPosition = streamPosition;
                                while (left > 0)
                                {
                                    int thisBlockSize = (int)Math.Min(left, maxBlockSize);
                                    streamPosition -= thisBlockSize;
                                    dest.Position = streamPosition;
                                    int actual = dest.Read(temp, 0, thisBlockSize);
                                    Debug.Assert(thisBlockSize == actual);
                                    int overhead = Math.Min(inMemoryDataStart, thisBlockSize);
                                    if (overhead > 0)
                                    {
                                        // we have more bytes than we can put to the stream
                                        // so they go to memory

                                        Helpers.BlockCopy(temp, thisBlockSize - overhead, buffer, inMemoryDataStart - overhead, overhead);

                                        inMemoryDataStart -= overhead;
                                        thisBlockSize -= overhead;
                                        left -= overhead;
                                    }

                                    if (thisBlockSize > 0)
                                    {
                                        dest.Position = streamPosition + offset;
                                        Debug.Assert(dest.Position + thisBlockSize <= streamEndPosition);
                                        dest.Write(temp, 0, thisBlockSize);
                                    }

                                    left -= thisBlockSize;
                                }
                                dest.Position = streamEndPosition;
                            }

                            // from inBufferPos
                            temp = writer.GetTempBuffer(10);
                            int count = 0;
                            tmp = (ulong)len;
                            do
                            {
                                temp[count++] = (byte)((tmp & 0x7F) | 0x80);
                            } while ((tmp >>= 7) != 0);
                            temp[count - 1] = (byte)(temp[count - 1] & ~0x80);

                            writeFromTemp = count;

                            writer._position64 += offset;
                            writer._ioIndex += offset;

                        }
                    }
                    break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(style));
                }

                if (writeFromTemp != 0)
                {
                    Debug.Assert(temp != null, "temp != null");

                    int lengthInStream = (int)Math.Min(-inBufferPos, writeFromTemp);
                    dest.Position += inBufferPos;
                    dest.Write(temp, 0, lengthInStream);

                    if (lengthInStream != writeFromTemp)
                        Helpers.BlockCopy(temp, lengthInStream, writer._ioBuffer, 0, writeFromTemp - lengthInStream);
                }

                dest.Position = prevPos;
            }
            // and this object is no longer a blockage - also flush if sensible

            writer._flushLock--;

            if (writer.IsFlushAdvised())
            {
                ProtoWriter.Flush(writer);
            }

        }

        bool IsFlushAdvised()
        {
            return IsFlushAdvised(_ioIndex);
        }

        bool IsFlushAdvised(int whenSize)
        {
            const int ADVISORY_FLUSH_SIZE = 1024;
            const int STREAM_AS_BUFFER_ADVISORY_FLUSH_SIZE = 1024 * 1024 * 10; // do not backread stream unless necessary
            bool streamRead = _streamAsBufferAllowed;

            return !streamRead
                       ? _flushLock == 0 && whenSize >= ADVISORY_FLUSH_SIZE
                       : whenSize >= STREAM_AS_BUFFER_ADVISORY_FLUSH_SIZE;
        }

        /// <summary>
        /// Creates a new writer against a stream
        /// </summary>
        /// <param name="dest">The destination stream</param>
        /// <param name="model">The model to use for serialization; this can be null, but this will impair the ability to serialize sub-objects</param>
        /// <param name="context">Additional context about this serialization operation</param>
        public ProtoWriter(Stream dest, TypeModel model, SerializationContext context)
        {
            if (dest == null) throw new ArgumentNullException(nameof(dest));
            if (!dest.CanWrite) throw new ArgumentException("Cannot write to stream", nameof(dest));
            if ((model != null && model.AllowStreamRewriting) && dest.CanSeek && dest.CanRead)
                _streamAsBufferAllowed = true;
            //if (model == null) throw new ArgumentNullException("model");
            this._dest = dest;
            this._ioBuffer = BufferPool.GetBuffer();
            this._model = model;
            this._wireType = WireType.None;
            if (context == null) { context = SerializationContext.Default; }
            else { context.Freeze(); }
            this._context = context;
        }

        private readonly SerializationContext _context;
        /// <summary>
        /// Addition information about this serialization operation.
        /// </summary>
        public SerializationContext Context { get { return _context; } }
        void IDisposable.Dispose()
        {
            Dispose();
        }
        private void Dispose()
        {   // importantly, this does **not** own the stream, and does not dispose it
            if (_dest != null)
            {
                Flush(this);
                _dest = null;
            }
            if (_tempBuffer != null)
                BufferPool.ReleaseBufferToPool(ref _tempBuffer);
            _model = null;
            BufferPool.ReleaseBufferToPool(ref _ioBuffer);
            _lateReferences.Reset();
        }

        private byte[] _ioBuffer;

        private int _ioIndex;

        // note that this is used by some of the unit tests and should not be removed
        public static long GetLongPosition(ProtoWriter writer) { return writer._position64; }
        // note that this is used by some of the unit tests and should not be removed
        public static int GetPosition(ProtoWriter writer) { return checked((int) writer._position64); }
        private long _position64;
        private static void DemandSpace(int required, ProtoWriter writer)
        {
            // check for enough space
            if ((writer._ioBuffer.Length - writer._ioIndex) < required)
            {
                if (writer.IsFlushAdvised(writer._ioIndex + required))
                {
                    Flush(writer); // try emptying the buffer
                    if ((writer._ioBuffer.Length - writer._ioIndex) >= required) return;
                }
                // either can't empty the buffer, or that didn't help; need more space
                BufferPool.ResizeAndFlushLeft(ref writer._ioBuffer, required + writer._ioIndex, 0, writer._ioIndex);
            }
        }
        /// <summary>
        /// Flushes data to the underlying stream, and releases any resources. The underlying stream is *not* disposed
        /// by this operation.
        /// </summary>
        public void Close()
        {
            CheckDepthFlushlock();
            Dispose();
        }

        internal void CheckDepthFlushlock()
        {
            if (_depth != 0 || _flushLock != 0 || _fieldStarted) throw new InvalidOperationException("The writer is in an incomplete state");
        }

        /// <summary>
        /// Get the TypeModel associated with this writer
        /// </summary>
        public TypeModel Model { get { return _model; } }

        bool _streamAsBufferAllowed;

        public bool AllowStreamRewriting => _streamAsBufferAllowed;

        /// <summary>
        /// Writes any buffered data (if possible) to the underlying stream.
        /// </summary>
        /// <param name="writer">The writer to flush</param>
        /// <remarks>It is not always possible to fully flush, since some sequences
        /// may require values to be back-filled into the byte-stream.</remarks>
        internal static void Flush(ProtoWriter writer)
        {
            if ((writer._streamAsBufferAllowed || writer._flushLock == 0) && writer._ioIndex != 0)
            {
                writer._dest.Write(writer._ioBuffer, 0, writer._ioIndex);
                writer._ioIndex = 0;

            }
        }

        /// <summary>
        /// Works only if position is in buffer and not flushed yet
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        internal bool TrySeek(long position)
        {
            var flushed = _position64 - _ioIndex;
            if (flushed > 0) position -= flushed;
            if (position < 0 || position > _ioBuffer.Length)
                return false;
            var diff = position - _ioIndex;
            _ioIndex = (int)position;
            _position64 += diff;
            return true;
        }

        /// <summary>
        /// Writes an unsigned 32-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        private static void WriteUInt32Variant(uint value, ProtoWriter writer)
        {
            DemandSpace(5, writer);
            int count = 0;
            do {
                writer._ioBuffer[writer._ioIndex++] = (byte)((value & 0x7F) | 0x80);
                count++;
            } while ((value >>= 7) != 0);
            writer._ioBuffer[writer._ioIndex - 1] &= 0x7F;
            writer._position64 += count;
        }

        static readonly UTF8Encoding Encoding = new UTF8Encoding();

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
            DemandSpace(10, writer);
            int count = 0;
            do
            {
                writer._ioBuffer[writer._ioIndex++] = (byte)((value & 0x7F) | 0x80);
                count++;
            } while ((value >>= 7) != 0);
            writer._ioBuffer[writer._ioIndex - 1] &= 0x7F;
            writer._position64 += count;
        }
        /// <summary>
        /// Writes a string to the stream; supported wire-types: String
        /// </summary>
        public static void WriteString(string value, ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (writer._wireType != WireType.String) throw CreateException(writer);
            if (value == null) throw new ArgumentNullException(nameof(value)); // written header; now what?
            int len = value.Length;
            if (len == 0)
            {
                WriteUInt32Variant(0, writer);
                writer._wireType = WireType.None;
                return; // just a header
            }

            int predicted = Encoding.GetByteCount(value);
            WriteUInt32Variant((uint)predicted, writer);
            DemandSpace(predicted, writer);
            int actual = Encoding.GetBytes(value, 0, value.Length, writer._ioBuffer, writer._ioIndex);
            Helpers.DebugAssert(predicted == actual);
            IncrementedAndReset(actual, writer);
        }
        /// <summary>
        /// Writes an unsigned 64-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public static void WriteUInt64(ulong value, ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            switch (writer._wireType)
            {
                case WireType.Fixed64:
                    ProtoWriter.WriteInt64((long)value, writer);
                    return;
                case WireType.Variant:
                    WriteUInt64Variant(value, writer);
                    writer._wireType = WireType.None;
                    return;
                case WireType.Fixed32:
                    checked { ProtoWriter.WriteUInt32((uint)value, writer); }
                    return;
                default:
                    throw CreateException(writer);
            }
        }

        public void WriteLengthPrefix(ulong length)
        {
            if (_wireType != WireType.String) throw new InvalidOperationException($"Expected wireType {WireType.String} but was {_wireType}");
            WriteUInt64Variant(length, this);
            _wireType = WireType.None;
        }

        /// <summary>
        /// Writes a signed 64-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public static void WriteInt64(long value, ProtoWriter writer)
        {
            byte[] buffer;
            int index;
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            switch (writer._wireType)
            {
                case WireType.Fixed64:
                    DemandSpace(8, writer);
                    buffer = writer._ioBuffer;
                    index = writer._ioIndex;
                    buffer[index] = (byte)value;
                    buffer[index + 1] = (byte)(value >> 8);
                    buffer[index + 2] = (byte)(value >> 16);
                    buffer[index + 3] = (byte)(value >> 24);
                    buffer[index + 4] = (byte)(value >> 32);
                    buffer[index + 5] = (byte)(value >> 40);
                    buffer[index + 6] = (byte)(value >> 48);
                    buffer[index + 7] = (byte)(value >> 56);
                    IncrementedAndReset(8, writer);
                    return;
                case WireType.SignedVariant:
                    WriteUInt64Variant(Zig(value), writer);
                    writer._wireType = WireType.None;
                    return;
                case WireType.Variant:
                    if (value >= 0)
                    {
                        WriteUInt64Variant((ulong)value, writer);
                        writer._wireType = WireType.None;
                    }
                    else
                    {
                        DemandSpace(10, writer);
                        buffer = writer._ioBuffer;
                        index = writer._ioIndex;
                        buffer[index] = (byte)(value | 0x80);
                        buffer[index + 1] = (byte)((int)(value >> 7) | 0x80);
                        buffer[index + 2] = (byte)((int)(value >> 14) | 0x80);
                        buffer[index + 3] = (byte)((int)(value >> 21) | 0x80);
                        buffer[index + 4] = (byte)((int)(value >> 28) | 0x80);
                        buffer[index + 5] = (byte)((int)(value >> 35) | 0x80);
                        buffer[index + 6] = (byte)((int)(value >> 42) | 0x80);
                        buffer[index + 7] = (byte)((int)(value >> 49) | 0x80);
                        buffer[index + 8] = (byte)((int)(value >> 56) | 0x80);
                        buffer[index + 9] = 0x01; // sign bit
                        IncrementedAndReset(10, writer);
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
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            switch (writer._wireType)
            {
                case WireType.Fixed32:
                    ProtoWriter.WriteInt32((int)value, writer);
                    return;
                case WireType.Fixed64:
                    ProtoWriter.WriteInt64((int)value, writer);
                    return;
                case WireType.Variant:
                    WriteUInt32Variant(value, writer);
                    writer._wireType = WireType.None;
                    return;
                default:
                    throw CreateException(writer);
            }
        }

        public static bool TryWriteBuiltinTypeValue(object value, ProtoTypeCode typecode, bool allowSystemType, ProtoWriter writer)
        {
            switch (typecode)
            {
                case ProtoTypeCode.Int16:
                    ProtoWriter.WriteInt16((short)value, writer);
                    return true;
                case ProtoTypeCode.Int32:
                    ProtoWriter.WriteInt32((int)value, writer);
                    return true;
                case ProtoTypeCode.Int64:
                    ProtoWriter.WriteInt64((long)value, writer);
                    return true;
                case ProtoTypeCode.UInt16:
                    ProtoWriter.WriteUInt16((ushort)value, writer);
                    return true;
                case ProtoTypeCode.UInt32:
                    ProtoWriter.WriteUInt32((uint)value, writer);
                    return true;
                case ProtoTypeCode.UInt64:
                    ProtoWriter.WriteUInt64((ulong)value, writer);
                    return true;
                case ProtoTypeCode.Boolean:
                    ProtoWriter.WriteBoolean((bool)value, writer);
                    return true;
                case ProtoTypeCode.SByte:
                    ProtoWriter.WriteSByte((sbyte)value, writer);
                    return true;
                case ProtoTypeCode.Byte:
                    ProtoWriter.WriteByte((byte)value, writer);
                    return true;
                case ProtoTypeCode.Char:
                    ProtoWriter.WriteUInt16((ushort)(char)value, writer);
                    return true;
                case ProtoTypeCode.Double:
                    ProtoWriter.WriteDouble((double)value, writer);
                    return true;
                case ProtoTypeCode.Single:
                    ProtoWriter.WriteSingle((float)value, writer);
                    return true;
                case ProtoTypeCode.DateTime:
                    if (writer.Model != null && writer.Model.SerializeDateTimeKind())
                        BclHelpers.WriteDateTimeWithKind((DateTime)value, writer);
                    else
                        BclHelpers.WriteDateTime((DateTime)value, writer);
                    return true;
                case ProtoTypeCode.Decimal:
                    BclHelpers.WriteDecimal((decimal)value, writer);
                    return true;
                case ProtoTypeCode.String:
                    ProtoWriter.WriteString((string)value, writer);
                    return true;
                case ProtoTypeCode.ByteArray:
                    ProtoWriter.WriteBytes((byte[])value, writer);
                    return true;
                case ProtoTypeCode.TimeSpan:
                    BclHelpers.WriteTimeSpan((TimeSpan)value, writer);
                    return true;
                case ProtoTypeCode.Guid:
                    BclHelpers.WriteGuid((Guid)value, writer);
                    return true;
                case ProtoTypeCode.Uri:
                    ProtoWriter.WriteString(((Uri)value).AbsoluteUri, writer);
                    return true;
                case ProtoTypeCode.Type:
                    if (!allowSystemType) break;
                    ProtoWriter.WriteType((System.Type)value, writer);
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Behavior same as <see cref="ListHelpers"/> with ProtoCompatibility = off
        /// </summary>
        internal static void WriteArrayContent<T>(T[] arr, WireType wt, Action<T, ProtoWriter> writer, ProtoWriter dest)
        {
            var t = StartSubItem(null, true, dest);
            WriteFieldHeader(ListHelpers.FieldLength, WireType.Variant, dest);
            WriteInt32(arr.Length, dest);
            for (int i = 0; i < arr.Length; i++)
            {
                if (i == 0)
                    WriteFieldHeader(ListHelpers.FieldItem, wt, dest);
                else
                    WriteFieldHeaderIgnored(wt, dest);

                writer(arr[i], dest);
            }
            EndSubItem(t, dest);
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
        private static void WriteInt32ToBuffer(int value, byte[] buffer, int index)
        {
            buffer[index] = (byte)value;
            buffer[index + 1] = (byte)(value >> 8);
            buffer[index + 2] = (byte)(value >> 16);
            buffer[index + 3] = (byte)(value >> 24);
        }

        private static void WriteInt32ToBufferBE(int value, byte[] buffer, int index)
        {
            buffer[index + 3] = (byte)value;
            buffer[index + 2] = (byte)(value >> 8);
            buffer[index + 1] = (byte)(value >> 16);
            buffer[index] = (byte)(value >> 24);
        }

        /// <summary>
        /// Writes a signed 32-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public static void WriteInt32(int value, ProtoWriter writer)
        {
            byte[] buffer;
            int index;
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            switch (writer._wireType)
            {
                case WireType.Fixed32:
                    DemandSpace(4, writer);
                    WriteInt32ToBuffer(value, writer._ioBuffer, writer._ioIndex);
                    IncrementedAndReset(4, writer);
                    return;
                case WireType.Fixed64:
                    DemandSpace(8, writer);
                    buffer = writer._ioBuffer;
                    index = writer._ioIndex;
                    buffer[index] = (byte)value;
                    buffer[index + 1] = (byte)(value >> 8);
                    buffer[index + 2] = (byte)(value >> 16);
                    buffer[index + 3] = (byte)(value >> 24);
                    buffer[index + 4] = buffer[index + 5] =
                        buffer[index + 6] = buffer[index + 7] = 0;
                    IncrementedAndReset(8, writer);
                    return;
                case WireType.SignedVariant:
                    WriteUInt32Variant(Zig(value), writer);
                    writer._wireType = WireType.None;
                    return;
                case WireType.Variant:
                    if (value >= 0)
                    {
                        WriteUInt32Variant((uint)value, writer);
                        writer._wireType = WireType.None;
                    }
                    else
                    {
                        DemandSpace(10, writer);
                        buffer = writer._ioBuffer;
                        index = writer._ioIndex;
                        buffer[index] = (byte)(value | 0x80);
                        buffer[index + 1] = (byte)((value >> 7) | 0x80);
                        buffer[index + 2] = (byte)((value >> 14) | 0x80);
                        buffer[index + 3] = (byte)((value >> 21) | 0x80);
                        buffer[index + 4] = (byte)((value >> 28) | 0x80);
                        buffer[index + 5] = buffer[index + 6] =
                            buffer[index + 7] = buffer[index + 8] = (byte)0xFF;
                        buffer[index + 9] = (byte)0x01;
                        IncrementedAndReset(10, writer);
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
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            switch (writer._wireType)
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
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            switch (writer._wireType)
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
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            string rhs = enumValue == null ? "<null>" : (enumValue.GetType().FullName + "." + enumValue.ToString());
            throw new ProtoException("No wire-value is mapped to the enum " + rhs + " at position " + writer._position64.ToString());
        }
        // general purpose serialization exception message
        internal static Exception CreateException(ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            string field = writer._fieldStarted
                                         ? ", waiting for wire type of field number " + writer._fieldNumber
                                         : (writer._wireType != WireType.None ? (", field number " + writer._fieldNumber) : "");
            return new ProtoException("Invalid serialization operation with wire-type " + writer._wireType.ToString() + field + ", position " + writer._position64.ToString());
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
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            // we expect the writer to be raw here; the extension data will have the
            // header detail, so we'll copy it implicitly
            if(writer._wireType != WireType.None || writer._fieldStarted) throw CreateException(writer);

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
        /// <summary>
        /// Used for packed encoding; writes the length prefix using fixed sizes rather than using
        /// buffering. Only valid for fixed-32 and fixed-64 encoding.
        /// </summary>
        public static ulong? MakePackedPrefix(int elementCount, WireType wireType)
        {
            if (elementCount < 0) throw new ArgumentOutOfRangeException(nameof(elementCount));
            ulong bytes;
            switch(wireType)
            {
                // use long in case very large arrays are enabled
                case WireType.Fixed32: bytes = ((ulong)elementCount) << 2; break; // x4
                case WireType.Fixed64: bytes = ((ulong)elementCount) << 3; break; // x8
                default:
                    throw new ArgumentOutOfRangeException(nameof(wireType), "Invalid wire-type: " + wireType);
            }

            return bytes;
        }

        internal string SerializeType(System.Type type)
        {
            return TypeModel.SerializeType(_model, type);
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
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            WriteString(writer.SerializeType(value), writer);
        }
    }
}
