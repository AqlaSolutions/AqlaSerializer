// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections;
using System.Diagnostics;
#if !NO_GENERICS
using System.Collections.Generic;
#endif
using System.IO;
using System.Text;
using AqlaSerializer.Meta;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
#endif

#if MF
using EndOfStreamException = System.ApplicationException;
using OverflowException = System.ApplicationException;
#endif

namespace AqlaSerializer
{
    /// <summary>
    /// A stateful reader, used to read a protobuf stream. Typical usage would be (sequentially) to call
    /// ReadFieldHeader and (after matching the field) an appropriate Read* method.
    /// </summary>
    public sealed class ProtoReader : IDisposable
    {
        Stream _source;

        internal Stream UnderlyingStream => _source;

        internal int FixedLength { get; private set; }

        byte[] _ioBuffer;
        TypeModel _model;
        int _fieldNumber, _depth, _dataRemaining, _ioIndex, _position, _available, _blockEnd;
        long _underlyingPosition;
        WireType _wireType;
        bool _isFixedLength, _internStrings;
        private NetObjectCache _netCache;

        // this is how many outstanding objects do not currently have
        // values for the purposes of reference tracking; we'll default
        // to just trapping the root object
        // note: objects are trapped (the ref and key mapped) via NoteObject
        uint _trapCount; // uint is so we can use beq/bne more efficiently than bgt

        class ReferenceState
        {
            public readonly LateReferencesCache LateReferencesCache;
            public readonly NetObjectCache NetObjectCache;

            public ReferenceState(NetObjectCache netObjectCache, LateReferencesCache lateReferencesCache)
            {
                LateReferencesCache = lateReferencesCache;
                NetObjectCache = netObjectCache;
            }
        }

        internal object StoreReferenceState()
        {
            return new ReferenceState(_netCache.Clone(), _lateReferences.Clone());
        }

        internal void LoadReferenceState(object state)
        {
            var s = (ReferenceState)state;
            _lateReferences = s.LateReferencesCache.Clone();
            _netCache = s.NetObjectCache.Clone();
        }

        /// <summary>
        /// Gets the number of the field being processed.
        /// </summary>
        public int FieldNumber { get { return _fieldNumber; } }
        /// <summary>
        /// Indicates the underlying proto serialization format on the wire.
        /// </summary>
        public WireType WireType { get { return _wireType; } }

        /// <summary>
        /// Creates a new reader against a stream
        /// </summary>
        /// <param name="source">The source stream</param>
        /// <param name="model">The model to use for serialization; this can be null, but this will impair the ability to deserialize sub-objects</param>
        /// <param name="context">Additional context about this serialization operation</param>
        public ProtoReader(Stream source, TypeModel model, SerializationContext context) 
        {
            Init(this, source, model, context, TO_EOF);
        }
        
        public ProtoReader MakeSubReader(int anyPositionFromRootReaderStart)
        {
            // subreader will have true initial position when initializing
            // we seek before any reading so don't have to care to set it back or about interference with subreader
            // seeking is always required for subreaders so no need to check CanSeek
            _source.Position = InitialUnderlyingStreamPosition;
            var r = new ProtoReader(_source, _model, _context, _isFixedLength ? FixedLength : TO_EOF);
            r.SkipBytes(anyPositionFromRootReaderStart);
            return r;
        }
        
        internal const int TO_EOF = -1;
        
        
        /// <summary>
        /// Gets / sets a flag indicating whether strings should be checked for repetition; if
        /// true, any repeated UTF-8 byte sequence will result in the same String instance, rather
        /// than a second instance of the same string. Enabled by default. Note that this uses
        /// a <i>custom</i> interner - the system-wide string interner is not used.
        /// </summary>
        public bool InternStrings { get { return _internStrings; } set { _internStrings = value; } }

        /// <summary>
        /// Creates a new reader against a stream
        /// </summary>
        /// <param name="source">The source stream</param>
        /// <param name="model">The model to use for serialization; this can be null, but this will impair the ability to deserialize sub-objects</param>
        /// <param name="context">Additional context about this serialization operation</param>
        /// <param name="length">The number of bytes to read, or -1 to read until the end of the stream</param>
        public ProtoReader(Stream source, TypeModel model, SerializationContext context, int length)
        {
            Init(this, source, model, context, length);
        }

        private static void Init(ProtoReader reader, Stream source, TypeModel model, SerializationContext context, int length)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.CanRead) throw new ArgumentException("Cannot read from stream", nameof(source));
            reader._underlyingPosition = reader.InitialUnderlyingStreamPosition = source.Position;
            reader._source = source;
            reader._ioBuffer = BufferPool.GetBuffer();
            reader._model = model;
            bool isFixedLength = length >= 0;
            reader.FixedLength = length;
            reader._isFixedLength = isFixedLength;
            reader._dataRemaining = isFixedLength ? length : 0;

            if (context == null) { context = SerializationContext.Default; }
            else { context.Freeze(); }
            reader._context = context;
            reader._position = reader._available = reader._depth = reader._fieldNumber = reader._ioIndex = 0;
            reader._blockEnd = int.MaxValue;
            reader._internStrings = true;
            reader._wireType = WireType.None;
            reader._trapCount = 0;
            reader._trappedKey = 0;
            reader._trapNoteReserved.Clear();
            if(reader._netCache == null) reader._netCache = new NetObjectCache();
            reader._netCache.ResetRoot();
        }

        private SerializationContext _context;

        /// <summary>
        /// Addition information about this deserialization operation.
        /// </summary>
        public SerializationContext Context { get { return _context; } }
        /// <summary>
        /// Releases resources used by the reader, but importantly <b>does not</b> Dispose the 
        /// underlying stream; in many typical use-cases the stream is used for different
        /// processes, so it is assumed that the consumer will Dispose their stream separately.
        /// </summary>
        public void Dispose()
        {
            // importantly, this does **not** own the stream, and does not dispose it
            _source = null;
            _model = null;
            BufferPool.ReleaseBufferToPool(ref _ioBuffer);
            if(_stringInterner != null) _stringInterner.Clear();
            if(_netCache != null) _netCache.Clear();
            _lateReferences.Reset();
        }
        

        LateReferencesCache _lateReferences = new LateReferencesCache();

        public static void NoteLateReference(int typeKey, object value, ProtoReader reader)
        {
#if DEBUG
            Debug.Assert(value != null);
            Debug.Assert(ReferenceEquals(value, reader._netCache.LastNewValue));
#endif
            reader._lateReferences.AddLateReference(new LateReferencesCache.LateReference(typeKey, value, reader._netCache.LastNewKey));
        }

        public static bool TryGetNextLateReference(out int typeKey, out object value, out int referenceKey, ProtoReader reader)
        {
            var r = reader._lateReferences.TryGetNextLateReference();
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

        internal int TryReadUInt32VariantWithoutMoving(bool trimNegative, out uint value)
        {
            if (_available < 10) Ensure(10, false);
            if (_available == 0)
            {
                value = 0;
                return 0;
            }
            int readPos = _ioIndex;
            value = _ioBuffer[readPos++];
            if ((value & 0x80) == 0) return 1;
            value &= 0x7F;
            if (_available == 1) throw EoF(this);

            uint chunk = _ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 7;
            if ((chunk & 0x80) == 0) return 2;
            if (_available == 2) throw EoF(this);

            chunk = _ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 14;
            if ((chunk & 0x80) == 0) return 3;
            if (_available == 3) throw EoF(this);

            chunk = _ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 21;
            if ((chunk & 0x80) == 0) return 4;
            if (_available == 4) throw EoF(this);

            chunk = _ioBuffer[readPos];
            value |= chunk << 28; // can only use 4 bits from this chunk
            if ((chunk & 0xF0) == 0) return 5;

            if (trimNegative // allow for -ve values
                && (chunk & 0xF0) == 0xF0
                && _available >= 10
                    && _ioBuffer[++readPos] == 0xFF
                    && _ioBuffer[++readPos] == 0xFF
                    && _ioBuffer[++readPos] == 0xFF
                    && _ioBuffer[++readPos] == 0xFF
                    && _ioBuffer[++readPos] == 0x01)
            {
                return 10;
            }
            throw AddErrorData(new OverflowException(), this);
        }
        private uint ReadUInt32Variant(bool trimNegative)
        {
            uint value;
            int read = TryReadUInt32VariantWithoutMoving(trimNegative, out value);
            if (read > 0)
            {
                _ioIndex += read;
                _available -= read;
                _position += read;
                return value;
            }
            throw EoF(this);
        }
        private bool TryReadUInt32Variant(out uint value)
        {
            int read = TryReadUInt32VariantWithoutMoving(false, out value);
            if (read > 0)
            {
                _ioIndex += read;
                _available -= read;
                _position += read;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Reads an unsigned 32-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public uint ReadUInt32()
        {
            switch (_wireType)
            {
                case WireType.Variant:
                    return ReadUInt32Variant(false);
                case WireType.Fixed32:
                    if (_available < 4) Ensure(4, true);
                    _position += 4;
                    _available -= 4;
                    return ((uint)_ioBuffer[_ioIndex++])
                        | (((uint)_ioBuffer[_ioIndex++]) << 8)
                        | (((uint)_ioBuffer[_ioIndex++]) << 16)
                        | (((uint)_ioBuffer[_ioIndex++]) << 24);
                case WireType.Fixed64:
                    ulong val = ReadUInt64();
                    checked { return (uint)val; }
                default:
                    throw CreateWireTypeException();
            }
        }
        
        /// <summary>
        /// Returns the position of the current reader (note that this is not necessarily the same as the position
        /// in the underlying stream, if multiple readers are used on the same stream)
        /// </summary>
        public int Position { get { return _position; } }

        internal long InitialUnderlyingStreamPosition { get; private set; }

        internal void Ensure(int count, bool strict)
        {
            // this is required because position may be changed in subreader:
            if (_source.CanSeek && _source.Position != _underlyingPosition)
                _source.Position = _underlyingPosition;

            Helpers.DebugAssert(_available <= count, "Asking for data without checking first");
            if (count > _ioBuffer.Length)
            {
                BufferPool.ResizeAndFlushLeft(ref _ioBuffer, count, _ioIndex, _available);
                _ioIndex = 0;
            }
            else if (_ioIndex + count >= _ioBuffer.Length)
            {
                // need to shift the buffer data to the left to make space
                Helpers.BlockCopy(_ioBuffer, _ioIndex, _ioBuffer, 0, _available);
                _ioIndex = 0;
            }
            count -= _available;
            int writePos = _ioIndex + _available, bytesRead;
            int canRead = _ioBuffer.Length - writePos;
            if (_isFixedLength)
            {   // throttle it if needed
                if (_dataRemaining < canRead) canRead = _dataRemaining;
            }
            while (count > 0 && canRead > 0 && (bytesRead = _source.Read(_ioBuffer, writePos, canRead)) > 0)
            {
                _underlyingPosition += bytesRead;
                _available += bytesRead;
                count -= bytesRead;
                canRead -= bytesRead;
                writePos += bytesRead;
                if (_isFixedLength) { _dataRemaining -= bytesRead; }
            }
            if (strict && count > 0)
            {
                throw EoF(this);
            }

        }
        /// <summary>
        /// Reads a signed 16-bit integer from the stream: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public short ReadInt16()
        {
            checked { return (short)ReadInt32(); }
        }
        /// <summary>
        /// Reads an unsigned 16-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public ushort ReadUInt16()
        {
            checked { return (ushort)ReadUInt32(); }
        }

        /// <summary>
        /// Reads an unsigned 8-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public byte ReadByte()
        {
            checked { return (byte)ReadUInt32(); }
        }

        /// <summary>
        /// Reads a signed 8-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public sbyte ReadSByte()
        {
            checked { return (sbyte)ReadInt32(); }
        }

        /// <summary>
        /// Reads a signed 32-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public int ReadInt32()
        {
            switch (_wireType)
            {
                case WireType.Variant:
                    return (int)ReadUInt32Variant(true);
                case WireType.Fixed32:
                    if (_available < 4) Ensure(4, true);
                    _position += 4;
                    _available -= 4;
                    return ((int)_ioBuffer[_ioIndex++])
                        | (((int)_ioBuffer[_ioIndex++]) << 8)
                        | (((int)_ioBuffer[_ioIndex++]) << 16)
                        | (((int)_ioBuffer[_ioIndex++]) << 24);
                case WireType.Fixed64:
                    long l = ReadInt64();
                    checked { return (int)l; }
                case WireType.SignedVariant:
                    return Zag(ReadUInt32Variant(true));
                default:
                    throw CreateWireTypeException();
            }
        }
        private const long Int64Msb = ((long)1) << 63;
        private const int Int32Msb = ((int)1) << 31;
        private static int Zag(uint ziggedValue)
        {
            int value = (int)ziggedValue;
            return (-(value & 0x01)) ^ ((value >> 1) & ~ProtoReader.Int32Msb);
        }

        private static long Zag(ulong ziggedValue)
        {
            long value = (long)ziggedValue;
            return (-(value & 0x01L)) ^ ((value >> 1) & ~ProtoReader.Int64Msb);
        }
        /// <summary>
        /// Reads a signed 64-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public long ReadInt64()
        {
            switch (_wireType)
            {
                case WireType.Variant:
                    return (long)ReadUInt64Variant();
                case WireType.Fixed32:
                    return ReadInt32();
                case WireType.Fixed64:
                    if (_available < 8) Ensure(8, true);
                    _position += 8;
                    _available -= 8;

                    return ((long)_ioBuffer[_ioIndex++])
                        | (((long)_ioBuffer[_ioIndex++]) << 8)
                        | (((long)_ioBuffer[_ioIndex++]) << 16)
                        | (((long)_ioBuffer[_ioIndex++]) << 24)
                        | (((long)_ioBuffer[_ioIndex++]) << 32)
                        | (((long)_ioBuffer[_ioIndex++]) << 40)
                        | (((long)_ioBuffer[_ioIndex++]) << 48)
                        | (((long)_ioBuffer[_ioIndex++]) << 56);

                case WireType.SignedVariant:
                    return Zag(ReadUInt64Variant());
                default:
                    throw CreateWireTypeException();
            }
        }

        private int TryReadUInt64VariantWithoutMoving(out ulong value)
        {
            if (_available < 10) Ensure(10, false);
            if (_available == 0)
            {
                value = 0;
                return 0;
            }
            int readPos = _ioIndex;
            value = _ioBuffer[readPos++];
            if ((value & 0x80) == 0) return 1;
            value &= 0x7F;
            if (_available == 1) throw EoF(this);

            ulong chunk = _ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 7;
            if ((chunk & 0x80) == 0) return 2;
            if (_available == 2) throw EoF(this);

            chunk = _ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 14;
            if ((chunk & 0x80) == 0) return 3;
            if (_available == 3) throw EoF(this);

            chunk = _ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 21;
            if ((chunk & 0x80) == 0) return 4;
            if (_available == 4) throw EoF(this);

            chunk = _ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 28;
            if ((chunk & 0x80) == 0) return 5;
            if (_available == 5) throw EoF(this);

            chunk = _ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 35;
            if ((chunk & 0x80) == 0) return 6;
            if (_available == 6) throw EoF(this);

            chunk = _ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 42;
            if ((chunk & 0x80) == 0) return 7;
            if (_available == 7) throw EoF(this);


            chunk = _ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 49;
            if ((chunk & 0x80) == 0) return 8;
            if (_available == 8) throw EoF(this);

            chunk = _ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 56;
            if ((chunk & 0x80) == 0) return 9;
            if (_available == 9) throw EoF(this);

            chunk = _ioBuffer[readPos];
            value |= chunk << 63; // can only use 1 bit from this chunk

            if ((chunk & ~(ulong)0x01) != 0) throw AddErrorData(new OverflowException(), this);
            return 10;
        }

        private ulong ReadUInt64Variant()
        {
            ulong value;
            int read = TryReadUInt64VariantWithoutMoving(out value);
            if (read > 0)
            {
                _ioIndex += read;
                _available -= read;
                _position += read;
                return value;
            }
            throw EoF(this);
        }

#if NO_GENERICS
        private System.Collections.Hashtable stringInterner;
        private string Intern(string value)
        {
            if (value == null) return null;
            if (value.Length == 0) return "";
            if (stringInterner == null)
            {
                stringInterner = new System.Collections.Hashtable();
                stringInterner.Add(value, value);      
            }
            else if (stringInterner.ContainsKey(value))
            {
                value = (string)stringInterner[value];
            }
            else
            {
                stringInterner.Add(value, value);
            }
            return value;
        }
#else
        private System.Collections.Generic.Dictionary<string,string> _stringInterner;
                private string Intern(string value)
        {
            if (value == null) return null;
            if (value.Length == 0) return "";
            string found;
            if (_stringInterner == null)
            {
                _stringInterner = new System.Collections.Generic.Dictionary<string, string>();
                _stringInterner.Add(value, value);        
            }
            else if (_stringInterner.TryGetValue(value, out found))
            {
                value = found;
            }
            else
            {
                _stringInterner.Add(value, value);
            }
            return value;
        }
#endif

        static readonly UTF8Encoding Encoding = new UTF8Encoding();
        /// <summary>
        /// Reads a string from the stream (using UTF8); supported wire-types: String
        /// </summary>
        public string ReadString()
        {
            if (_wireType == WireType.String)
            {
                int bytes = (int)ReadUInt32Variant(false);
                if (bytes == 0) return "";
                if (_available < bytes) Ensure(bytes, true);
#if MF
                byte[] tmp;
                if(ioIndex == 0 && bytes == ioBuffer.Length) {
                    // unlikely, but...
                    tmp = ioBuffer;
                } else {
                    tmp = new byte[bytes];
                    Helpers.BlockCopy(ioBuffer, ioIndex, tmp, 0, bytes);
                }
                string s = new string(encoding.GetChars(tmp));
#else
                string s = Encoding.GetString(_ioBuffer, _ioIndex, bytes);
#endif
                if (_internStrings) { s = Intern(s); }
                _available -= bytes;
                _position += bytes;
                _ioIndex += bytes;
                return s;
            }
            throw CreateWireTypeException();
        }
        /// <summary>
        /// Throws an exception indication that the given value cannot be mapped to an enum.
        /// </summary>
        public void ThrowEnumException(System.Type type, int value)
        {
            string desc = type == null ? "<null>" : type.FullName;
            throw AddErrorData(new ProtoException("No " + desc + " enum is mapped to the wire-value " + value.ToString()), this);
        }
        private Exception CreateWireTypeException()
        {
            return CreateException("Invalid wire-type; this usually means you have over-written a file without truncating or setting the length; see http://stackoverflow.com/q/2152978/23354");
        }
        private Exception CreateException(string message)
        {
            return AddErrorData(new ProtoException(message), this);
        }
        /// <summary>
        /// Reads a double-precision number from the stream; supported wire-types: Fixed32, Fixed64
        /// </summary>
        public
#if !FEAT_SAFE
 unsafe
#endif
 double ReadDouble()
        {
            switch (_wireType)
            {
                case WireType.Fixed32:
                    return ReadSingle();
                case WireType.Fixed64:
                    long value = ReadInt64();
#if FEAT_SAFE
                    return BitConverter.ToDouble(BitConverter.GetBytes(value), 0);
#else
                    return *(double*)&value;
#endif
                default:
                    throw CreateWireTypeException();
            }
        }

        /// <summary>
        /// Reads (merges) a sub-message from the stream, internally calling StartSubItem and EndSubItem, and (in between)
        /// parsing the message in accordance with the model associated with the reader
        /// </summary>
        public static object ReadObject(object value, int key, ProtoReader reader)
        {
            return ReadTypedObject(value, key, reader, null);
        }
        // not used anymore because we don't want aux on members
        public static object ReadTypedObject(object value, int key, ProtoReader reader, Type type)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (reader._model == null)
            {
                throw AddErrorData(new InvalidOperationException("Cannot deserialize sub-objects unless a model is provided"), reader);
            }
            if (key >= 0)
            {
                value = reader._model.Deserialize(key, value, reader, false);
            }
            else if (type != null)
            {
                SubItemToken token = ProtoReader.StartSubItem(reader);
                if (reader._model.TryDeserializeAuxiliaryType(reader, BinaryDataFormat.Default, Serializer.ListItemTag, type, ref value, true, false, true, false, false))
                {
                    // ok
                }
                else
                {
                    TypeModel.ThrowUnexpectedType(type);
                }
                ProtoReader.EndSubItem(token, reader);
            }
            else
            {
                TypeModel.ThrowUnexpectedType(type);
            }
            return value;
#endif
        }

        /// <summary>
        /// Makes the end of consuming a nested message in the stream; the stream must be either at the correct EndGroup
        /// marker, or all fields of the sub-message must have been consumed (in either case, this means ReadFieldHeader
        /// should return zero)
        /// </summary>
        public static void EndSubItem(SubItemToken token, ProtoReader reader)
        {
            EndSubItem(token, false, reader);
        }

        /// <summary>
        /// Makes the end of consuming a nested message in the stream; the stream must be either at the correct EndGroup
        /// marker, or all fields of the sub-message must have been consumed (in either case, this means ReadFieldHeader
        /// should return zero)
        /// </summary>
        public static void EndSubItem(SubItemToken token, bool skipToEnd, ProtoReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            int value = token.Value;
            if (value == int.MinValue)
            {
                // should not overwrite last read result
                // what if we reached outer subitem end?
                //reader.wireType = WireType.None;
                return;
            }
            if (skipToEnd)
                while (reader.ReadFieldHeader() != 0) reader.SkipField(); // skip field will recursively go through nested objects
            
            switch (reader._wireType)
            {
                case WireType.EndGroup:
                    endGroup:
                    if (value >= 0) throw AddErrorData(new ArgumentException("token"), reader);
                    if (-value != reader._fieldNumber) throw reader.CreateException("Wrong group was ended"); // wrong group ended!
                    reader._wireType = WireType.None; // this releases ReadFieldHeader
                    reader._depth--;
                    break;
                // case WireType.None: // TODO reinstate once reads reset the wire-type
                default:
                    if (value < reader._position)
                    {
                        if (value < 0)
                        {
                            if (reader.ReadFieldHeader() != 0 || reader._wireType != WireType.EndGroup) throw reader.CreateException("Group not read entirely or other group end problem");
                            goto endGroup;
                        }
                        throw reader.CreateException("Sub-message not read entirely");
                    }
                    if (reader._blockEnd != reader._position && reader._blockEnd != int.MaxValue)
                        throw reader.CreateException("Sub-message not read correctly");
                    reader._blockEnd = value;
                    reader._depth--;
                    break;
                /*default:
                    throw reader.BorkedIt(); */
            }
        }

        bool _expectRoot;

        /// <summary>
        /// Next StartSubItem call will be ignored unless ReadFieldHeader is called
        /// </summary>
        public static void ExpectRoot(ProtoReader reader)
        {
            reader._expectRoot = true;
        }

        /// <summary>
        /// Begins consuming a nested message in the stream; supported wire-types: StartGroup, String
        /// </summary>
        /// <remarks>The token returned must be help and used when callining EndSubItem</remarks>
        public static SubItemToken StartSubItem(ProtoReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (reader._expectRoot)
            {
                reader._expectRoot = false;
                return new SubItemToken(int.MinValue);
            }
            switch (reader._wireType)
            {
                case WireType.StartGroup:
                    reader._wireType = WireType.None; // to prevent glitches from double-calling
                    reader._depth++;
                    if (reader._depth > (reader._model?.RecursionDepthLimit ?? TypeModel.DefaultRecursionDepthLimit))
                        TypeModel.ThrowRecursionDepthLimitExceeded();
                    return new SubItemToken(-reader._fieldNumber);
                case WireType.String:
                    int len = (int)reader.ReadUInt32Variant(false);
                    if (len < 0) throw AddErrorData(new InvalidOperationException(), reader);
                    int lastEnd = reader._blockEnd;
                    reader._blockEnd = reader._position + len;
                    reader._depth++;
                    if (reader._depth > (reader._model?.RecursionDepthLimit ?? TypeModel.DefaultRecursionDepthLimit))
                        TypeModel.ThrowRecursionDepthLimitExceeded();
                    return new SubItemToken(lastEnd);
                default:
                    throw reader.CreateWireTypeException(); // throws
            }
        }

        /// <summary>
        /// Reads a field header from the stream, setting the wire-type and retuning the field number. If no
        /// more fields are available, then 0 is returned. This methods respects sub-messages.
        /// </summary>
        public int ReadFieldHeader()
        {
            _expectRoot = false;
            // at the end of a group the caller must call EndSubItem to release the
            // reader (which moves the status to Error, since ReadFieldHeader must
            // then be called)
            if (_blockEnd <= _position || _wireType == WireType.EndGroup) { return 0; }
            uint tag;
            if (TryReadUInt32Variant(out tag) && tag != 0)
            {
                _wireType = (WireType)(tag & 7);
                _fieldNumber = (int)(tag >> 3);
                if(_fieldNumber < 1) throw new ProtoException("Invalid field in source data: " + _fieldNumber.ToString());
            }
            else
            {
                _wireType = WireType.None;
                _fieldNumber = 0;
            }
            if (_wireType == AqlaSerializer.WireType.EndGroup)
            {
                if (_depth > 0) return 0; // spoof an end, but note we still set the field-number
                throw new ProtoException("Unexpected end-group in source data; this usually means the source data is corrupt");
            }
            return _fieldNumber;
        }
        /// <summary>
        /// Looks ahead to see whether the next field in the stream is what we expect
        /// (typically; what we've just finished reading - for example ot read successive list items)
        /// </summary>
        public bool TryReadFieldHeader(int field)
        {
            _expectRoot = false;
            // check for virtual end of stream
            if (_blockEnd <= _position || _wireType == WireType.EndGroup) { return false; }
            uint tag;
            int read = TryReadUInt32VariantWithoutMoving(false, out tag);
            WireType tmpWireType; // need to catch this to exclude (early) any "end group" tokens
            if (read > 0 && ((int)tag >> 3) == field
                && (tmpWireType = (WireType)(tag & 7)) != WireType.EndGroup)
            {
                _wireType = tmpWireType;
                _fieldNumber = field;
                _position += read;
                _ioIndex += read;
                _available -= read;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get the TypeModel associated with this reader
        /// </summary>
        public TypeModel Model { get { return _model; } }

        /// <summary>
        /// Compares the streams current wire-type to the hinted wire-type, updating the reader if necessary; for example,
        /// a Variant may be updated to SignedVariant. If the hinted wire-type is unrelated then no change is made.
        /// </summary>
        public void Hint(WireType wireType)
        {
            if (this._wireType == wireType) { }  // fine; everything as we expect
            else if (((int)wireType & 7) == (int)this._wireType)
            {   // the underling type is a match; we're customising it with an extension
                this._wireType = wireType;
            }
            // note no error here; we're OK about using alternative data
        }

        /// <summary>
        /// Verifies that the stream's current wire-type is as expected, or a specialized sub-type (for example,
        /// SignedVariant) - in which case the current wire-type is updated. Otherwise an exception is thrown.
        /// </summary>
        public void Assert(WireType wireType)
        {
            if (this._wireType == wireType) { }  // fine; everything as we expect
            else if (((int)wireType & 7) == (int)this._wireType)
            {   // the underling type is a match; we're customising it with an extension
                this._wireType = wireType;
            }
            else
            {   // nope; that is *not* what we were expecting!
                throw CreateWireTypeException();
            }
        }

        /// <summary>
        /// Discards the data for the current field.
        /// </summary>
        public void SkipField()
        {
            switch (_wireType)
            {
                case WireType.Null:
                    return;
                case WireType.Fixed32:
                    if(_available < 4) Ensure(4, true);
                    _available -= 4;
                    _ioIndex += 4;
                    _position += 4;
                    return;
                case WireType.Fixed64:
                    if (_available < 8) Ensure(8, true);
                    _available -= 8;
                    _ioIndex += 8;
                    _position += 8;
                    return;
                case WireType.String:
                    int len = (int)ReadUInt32Variant(false);
                    if (len <= _available)
                    { // just jump it!
                        _available -= len;
                        _ioIndex += len;
                        _position += len;
                        return;
                    }
                    SkipBytes(len);
                    return;
                case WireType.Variant:
                case WireType.SignedVariant:
                    ReadUInt64Variant(); // and drop it
                    return;
                case WireType.StartGroup:
                    int originalFieldNumber = this._fieldNumber;
                    _depth++; // need to satisfy the sanity-checks in ReadFieldHeader
                    while (ReadFieldHeader() > 0) { SkipField(); }
                    _depth--;
                    if (_wireType == WireType.EndGroup && _fieldNumber == originalFieldNumber)
                    { // we expect to exit in a similar state to how we entered
                        _wireType = AqlaSerializer.WireType.None;
                        return;
                    }
                    throw CreateWireTypeException();
                case WireType.None: // treat as explicit errorr
                case WireType.EndGroup: // treat as explicit error
                default: // treat as implicit error
                    throw CreateWireTypeException();
            }
        }

        public void SkipBytes(int len)
        {
            _position += len; // assumes success, but if it fails we're screwed anyway
            _underlyingPosition += len;
            // for dataRemaining add (in fact no need to subtract available from dataRemaining) anything we've got to-hand
            int lenForRemaining = len - _available;
            
            _ioIndex = _available = 0; // everything remaining in the buffer is garbage
            if (_isFixedLength)
            {
                if (lenForRemaining > _dataRemaining) throw EoF(this);
                // else assume we're going to be OK
                _dataRemaining -= lenForRemaining;
            }
            if (_source.CanSeek) // this is required because position may be changed in subreader
                _source.Position = _underlyingPosition;
            else
                ProtoReader.Seek(_source, len, _ioBuffer);
        }

        public int SeekAndExchangeBlockEnd(int anyPositionFromRootReaderStart, int newBlockEnd = int.MaxValue)
        {
            if (newBlockEnd < anyPositionFromRootReaderStart)
            {
                if (newBlockEnd == -1)
                    newBlockEnd = int.MaxValue;
                else
                    throw new ArgumentOutOfRangeException(nameof(newBlockEnd));
            }
            _position = anyPositionFromRootReaderStart;
            _source.Position = _underlyingPosition = InitialUnderlyingStreamPosition + anyPositionFromRootReaderStart;

            if (_isFixedLength) // for dataRemaining add back anything we've got to-hand
                _dataRemaining = FixedLength - anyPositionFromRootReaderStart;

            _ioIndex = _available = 0; // everything remaining in the buffer is garbage

            int end = _blockEnd;
            _blockEnd = newBlockEnd;
            return end;
        }

        /// <summary>
        /// Reads an unsigned 64-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public ulong ReadUInt64()
        {
            switch (_wireType)
            {
                case WireType.Variant:
                    return ReadUInt64Variant();
                case WireType.Fixed32:
                    return ReadUInt32();
                case WireType.Fixed64:
                    if (_available < 8) Ensure(8, true);
                    _position += 8;
                    _available -= 8;

                    return ((ulong)_ioBuffer[_ioIndex++])
                        | (((ulong)_ioBuffer[_ioIndex++]) << 8)
                        | (((ulong)_ioBuffer[_ioIndex++]) << 16)
                        | (((ulong)_ioBuffer[_ioIndex++]) << 24)
                        | (((ulong)_ioBuffer[_ioIndex++]) << 32)
                        | (((ulong)_ioBuffer[_ioIndex++]) << 40)
                        | (((ulong)_ioBuffer[_ioIndex++]) << 48)
                        | (((ulong)_ioBuffer[_ioIndex++]) << 56);
                default:
                    throw CreateWireTypeException();
            }
        }
        /// <summary>
        /// Reads a single-precision number from the stream; supported wire-types: Fixed32, Fixed64
        /// </summary>
        public
#if !FEAT_SAFE
 unsafe
#endif
 float ReadSingle()
        {
            switch (_wireType)
            {
                case WireType.Fixed32:
                    {
                        int value = ReadInt32();
#if FEAT_SAFE
                        return BitConverter.ToSingle(BitConverter.GetBytes(value), 0);
#else
                        return *(float*)&value;
#endif
                    }
                case WireType.Fixed64:
                    {
                        double value = ReadDouble();
                        float f = (float)value;
                        if (Helpers.IsInfinity(f)
                            && !Helpers.IsInfinity(value))
                        {
                            throw AddErrorData(new OverflowException(), this);
                        }
                        return f;
                    }
                default:
                    throw CreateWireTypeException();
            }
        }

        /// <summary>
        /// Reads a boolean value from the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        /// <returns></returns>
        public bool ReadBoolean()
        {
            switch (ReadUInt32())
            {
                case 0: return false;
                case 1: return true;
                default: throw CreateException("Unexpected boolean value");
            }
        }

        private static readonly byte[] EmptyBlob = new byte[0];
        /// <summary>
        /// Reads a byte-sequence from the stream, appending them to an existing byte-sequence (which can be null); supported wire-types: String
        /// </summary>
        public static byte[] AppendBytes(byte[] value, ProtoReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            switch (reader._wireType)
            {
                case WireType.String:
                    int len = (int)reader.ReadUInt32Variant(false);
                    reader._wireType = WireType.None;
                    if (len == 0) return value == null ? EmptyBlob : value;
                    int offset;
                    if (value == null || value.Length == 0)
                    {
                        offset = 0;
                        value = new byte[len];
                    }
                    else
                    {
                        offset = value.Length;
                        byte[] tmp = new byte[value.Length + len];
                        Helpers.BlockCopy(value, 0, tmp, 0, value.Length);
                        value = tmp;
                    }
                    // value is now sized with the final length, and (if necessary)
                    // contains the old data up to "offset"
                    reader._position += len; // assume success
                    while (len > reader._available)
                    {
                        if (reader._available > 0)
                        {
                            // copy what we *do* have
                            Helpers.BlockCopy(reader._ioBuffer, reader._ioIndex, value, offset, reader._available);
                            len -= reader._available;
                            offset += reader._available;
                            reader._ioIndex = reader._available = 0; // we've drained the buffer
                        }
                        //  now refill the buffer (without overflowing it)
                        int count = len > reader._ioBuffer.Length ? reader._ioBuffer.Length : len;
                        if (count > 0) reader.Ensure(count, true);
                    }
                    // at this point, we know that len <= available
                    if (len > 0)
                    {   // still need data, but we have enough buffered
                        Helpers.BlockCopy(reader._ioBuffer, reader._ioIndex, value, offset, len);
                        reader._ioIndex += len;
                        reader._available -= len;
                    }
                    return value;
                case WireType.Variant:
                    return new byte[0];
                default:
                    throw reader.CreateWireTypeException();
            }
        }

        //static byte[] ReadBytes(Stream stream, int length)
        //{
        //    if (stream == null) throw new ArgumentNullException("stream");
        //    if (length < 0) throw new ArgumentOutOfRangeException("length");
        //    byte[] buffer = new byte[length];
        //    int offset = 0, read;
        //    while (length > 0 && (read = stream.Read(buffer, offset, length)) > 0)
        //    {
        //        length -= read;
        //    }
        //    if (length > 0) throw EoF(null);
        //    return buffer;
        //}
        private static int ReadByteOrThrow(Stream source)
        {
            int val = source.ReadByte();
            if (val < 0) throw EoF(null);
            return val;
        }
        /// <summary>
        /// Reads the length-prefix of a message from a stream without buffering additional data, allowing a fixed-length
        /// reader to be created.
        /// </summary>
        public static int ReadLengthPrefix(Stream source, bool expectHeader, PrefixStyle style, out int fieldNumber)
        {
            int bytesRead;
            return ReadLengthPrefix(source, expectHeader, style, out fieldNumber, out bytesRead);
        }
        /// <summary>
        /// Reads a little-endian encoded integer. An exception is thrown if the data is not all available.
        /// </summary>
        public static int DirectReadLittleEndianInt32(Stream source)
        {
            return ReadByteOrThrow(source)
                | (ReadByteOrThrow(source) << 8)
                | (ReadByteOrThrow(source) << 16)
                | (ReadByteOrThrow(source) << 24);
        }
        /// <summary>
        /// Reads a big-endian encoded integer. An exception is thrown if the data is not all available.
        /// </summary>
        public static int DirectReadBigEndianInt32(Stream source)
        {
            return (ReadByteOrThrow(source) << 24)
                 | (ReadByteOrThrow(source) << 16)
                 | (ReadByteOrThrow(source) << 8)
                 | ReadByteOrThrow(source);
        }
        /// <summary>
        /// Reads a varint encoded integer. An exception is thrown if the data is not all available.
        /// </summary>
        public static int DirectReadVarintInt32(Stream source)
        {
            uint val;
            int bytes = TryReadUInt32Variant(source, out val);
            if (bytes <= 0) throw EoF(null);
            return (int) val;
        }
        /// <summary>
        /// Reads a string (of a given lenth, in bytes) directly from the source into a pre-existing buffer. An exception is thrown if the data is not all available.
        /// </summary>
        public static void DirectReadBytes(Stream source, byte[] buffer, int offset, int count)
        {
            int read;
            if (source == null) throw new ArgumentNullException(nameof(source));
            while(count > 0 && (read = source.Read(buffer, offset, count)) > 0)
            {
                count -= read;
                offset += read;
            }
            if (count > 0) throw EoF(null);
        }
        /// <summary>
        /// Reads a given number of bytes directly from the source. An exception is thrown if the data is not all available.
        /// </summary>
        public static byte[] DirectReadBytes(Stream source, int count)
        {
            byte[] buffer = new byte[count];
            DirectReadBytes(source, buffer, 0, count);
            return buffer;
        }
        /// <summary>
        /// Reads a string (of a given lenth, in bytes) directly from the source. An exception is thrown if the data is not all available.
        /// </summary>
        public static string DirectReadString(Stream source, int length)
        {
            byte[] buffer = new byte[length];
            DirectReadBytes(source, buffer, 0, length);
            return System.Text.Encoding.UTF8.GetString(buffer, 0, length);
        }

        /// <summary>
        /// Reads the length-prefix of a message from a stream without buffering additional data, allowing a fixed-length
        /// reader to be created.
        /// </summary>
        public static int ReadLengthPrefix(Stream source, bool expectHeader, PrefixStyle style, out int fieldNumber, out int bytesRead)
        {
            fieldNumber = 0;
            switch (style)
            {
                case PrefixStyle.None:
                    bytesRead = 0;
                    return int.MaxValue;
                case PrefixStyle.Base128:
                    uint val;
                    int tmpBytesRead;
                    bytesRead = 0;
                    if (expectHeader)
                    {
                        tmpBytesRead = ProtoReader.TryReadUInt32Variant(source, out val);
                        bytesRead += tmpBytesRead;
                        if (tmpBytesRead > 0)
                        {
                            if ((val & 7) != (uint)WireType.String)
                            { // got a header, but it isn't a string
                                throw new InvalidOperationException();
                            }
                            fieldNumber = (int)(val >> 3);
                            tmpBytesRead = ProtoReader.TryReadUInt32Variant(source, out val);
                            bytesRead += tmpBytesRead;
                            if (bytesRead == 0)
                            { // got a header, but no length
                                throw EoF(null);
                            }
                            return (int)val;
                        }
                        else
                        { // no header
                            bytesRead = 0;
                            return -1;
                        }
                    }
                    // check for a length
                    tmpBytesRead = ProtoReader.TryReadUInt32Variant(source, out val);
                    bytesRead += tmpBytesRead;
                    return bytesRead < 0 ? -1 : (int)val;

                case PrefixStyle.Fixed32:
                    {
                        int b = source.ReadByte();
                        if (b < 0)
                        {
                            bytesRead = 0;
                            return -1;
                        }
                        bytesRead = 4;
                        return b
                             | (ReadByteOrThrow(source) << 8)
                             | (ReadByteOrThrow(source) << 16)
                             | (ReadByteOrThrow(source) << 24);
                    }
                case PrefixStyle.Fixed32BigEndian:
                    {
                        int b = source.ReadByte();
                        if (b < 0)
                        {
                            bytesRead = 0;
                            return -1;
                        }
                        bytesRead = 4;
                        return (b << 24)
                            | (ReadByteOrThrow(source) << 16)
                            | (ReadByteOrThrow(source) << 8)
                            | ReadByteOrThrow(source);
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(style));
            }
        }
        /// <returns>The number of bytes consumed; 0 if no data available</returns>
        private static int TryReadUInt32Variant(Stream source, out uint value)
        {
            value = 0;
            int b = source.ReadByte();
            if (b < 0) { return 0; }
            value = (uint)b;
            if ((value & 0x80) == 0) { return 1; }
            value &= 0x7F;

            b = source.ReadByte();
            if (b < 0) throw EoF(null);
            value |= ((uint)b & 0x7F) << 7;
            if ((b & 0x80) == 0) return 2;

            b = source.ReadByte();
            if (b < 0) throw EoF(null);
            value |= ((uint)b & 0x7F) << 14;
            if ((b & 0x80) == 0) return 3;

            b = source.ReadByte();
            if (b < 0) throw EoF(null);
            value |= ((uint)b & 0x7F) << 21;
            if ((b & 0x80) == 0) return 4;

            b = source.ReadByte();
            if (b < 0) throw EoF(null);
            value |= (uint)b << 28; // can only use 4 bits from this chunk
            if ((b & 0xF0) == 0) return 5;

            throw new OverflowException();
        }

        internal static void Seek(Stream source, int count, byte[] buffer)
        {
            if (source.CanSeek)
            {
                source.Seek(count, SeekOrigin.Current);
                count = 0;
            }
            else if (buffer != null)
            {
                int bytesRead;
                while (count > buffer.Length && (bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    count -= bytesRead;
                }
                while (count > 0 && (bytesRead = source.Read(buffer, 0, count)) > 0)
                {
                    count -= bytesRead;
                }
            }
            else // borrow a buffer
            {
                buffer = BufferPool.GetBuffer();
                try
                {
                    int bytesRead;
                    while (count > buffer.Length && (bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        count -= bytesRead;
                    }
                    while (count > 0 && (bytesRead = source.Read(buffer, 0, count)) > 0)
                    {
                        count -= bytesRead;
                    }
                }
                finally
                {
                    BufferPool.ReleaseBufferToPool(ref buffer);
                }
            }
            if (count > 0) throw EoF(null);
        }
        internal static Exception AddErrorData(Exception exception, ProtoReader source)
        {
#if !CF && !FX11 && !PORTABLE
            if (exception != null && source != null && !exception.Data.Contains("protoSource"))
            {
                exception.Data.Add("protoSource", string.Format("tag={0}; wire-type={1}; offset={2}; depth={3}",
                    source._fieldNumber, source._wireType, source._position, source._depth));
            }
#endif
            return exception;

        }
        private static Exception EoF(ProtoReader source)
        {
            return AddErrorData(new EndOfStreamException(), source);
        }

        /// <summary>
        /// Copies the current field into the instance as extension data
        /// </summary>
        public void AppendExtensionData(IExtensible instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            IExtension extn = instance.GetExtensionObject(true);
            bool commit = false;
            // unusually we *don't* want "using" here; the "finally" does that, with
            // the extension object being responsible for disposal etc
            Stream dest = extn.BeginAppend();
            try
            {
                //TODO: replace this with stream-based, buffered raw copying
                using (ProtoWriter writer = new ProtoWriter(dest, _model, null))
                {
                    AppendExtensionField(writer);
                    writer.Close();
                }
                commit = true;
            }
            finally { extn.EndAppend(dest, commit); }
        }
        private void AppendExtensionField(ProtoWriter writer)
        {
            //TODO: replace this with stream-based, buffered raw copying
            ProtoWriter.WriteFieldHeaderAnyType(_fieldNumber, _wireType, writer);
            switch (_wireType)
            {
                case WireType.Fixed32:
                    ProtoWriter.WriteInt32(ReadInt32(), writer);
                    return;
                case WireType.Variant:
                case WireType.SignedVariant:
                case WireType.Fixed64:
                    ProtoWriter.WriteInt64(ReadInt64(), writer);
                    return;
                case WireType.String:
                    ProtoWriter.WriteBytes(AppendBytes(null, this), writer);
                    return;
                case WireType.StartGroup:
                    SubItemToken readerToken = StartSubItem(this),
                                 writerToken = ProtoWriter.StartSubItemWithoutWritingHeader(null, writer);
                    while (ReadFieldHeader() > 0) { AppendExtensionField(writer); }
                    EndSubItem(readerToken, this);
                    ProtoWriter.EndSubItem(writerToken, writer);
                    return;
                case WireType.None: // treat as explicit errorr
                case WireType.EndGroup: // treat as explicit error
                default: // treat as implicit error
                    throw CreateWireTypeException();
            }
        }
        
        /// <summary>
        /// Indicates whether the reader still has data remaining in the current sub-item,
        /// additionally setting the wire-type for the next field if there is more data.
        /// This is used when decoding packed data.
        /// </summary>
        public static bool HasSubValue(AqlaSerializer.WireType wireType, ProtoReader source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            // check for virtual end of stream
            if (source._blockEnd <= source._position || wireType == WireType.EndGroup) { return false; }
            source._wireType = wireType;
            source._fieldNumber = GroupNumberForIgnoredFields;
            return true;
        }

        /// <summary>
        /// Field Number is not written for ignored fields but when group is ended the group number is written (equal to specified field number)
        /// </summary>
        internal const int GroupNumberForIgnoredFields = 1;

        internal int GetTypeKey(ref Type type)
        {
            return _model.GetKey(ref type);
        }

        internal NetObjectCache NetCache
        {
            get { return _netCache; }
        }

        internal System.Type DeserializeType(string value)
        {
            return TypeModel.DeserializeType(_model, value);
        }

        internal void SetRootObject(object value)
        {
            //netCache.SetKeyedObject(NetObjectCache.Root, value);
            //trapCount--;
        }

        /// <summary>
        /// Utility method, not intended for public use; this helps maintain the root object is complex scenarios
        /// </summary>
        public static void NoteObject(object value, ProtoReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if(reader._trapCount != 0)
            {
                reader._netCache.SetKeyedObject(reader._trappedKey, value);
                reader._trapCount--;
            }
        }

        /// <summary>
        /// Utility method, not intended for public use; this helps maintain the root object is complex scenarios
        /// </summary>
        public static void NoteReservedTrappedObject(int trappedKey, object value, ProtoReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (trappedKey == -1) return;
            if (reader._trapCount != 0) throw new InvalidOperationException("NoteReservedTrappedObject called while new not reserved trap present");
            var stack = reader._trapNoteReserved;
            var trueKey = (int) stack.Peek();
            if (trappedKey != trueKey) throw new InvalidOperationException("NoteReservedTrappedObject called for " + trappedKey + " but waiting for " + trueKey);
            stack.Pop();
            reader._netCache.SetKeyedObject(trueKey, value);
        }

        /// <summary>
        /// Reads a Type from the stream, using the model's DynamicTypeFormatting if appropriate; supported wire-types: String
        /// </summary>
        public System.Type ReadType()
        {
            return TypeModel.DeserializeType(_model, ReadString());
        }

        public bool TryReadBuiltinType(ref object value, ProtoTypeCode typecode, bool allowSystemType)
        {
            switch (typecode)
            {
                case ProtoTypeCode.Int16:
                    value = this.ReadInt16();
                    return true;
                case ProtoTypeCode.Int32:
                    value = this.ReadInt32();
                    return true;
                case ProtoTypeCode.Int64:
                    value = this.ReadInt64();
                    return true;
                case ProtoTypeCode.UInt16:
                    value = this.ReadUInt16();
                    return true;
                case ProtoTypeCode.UInt32:
                    value = this.ReadUInt32();
                    return true;
                case ProtoTypeCode.UInt64:
                    value = this.ReadUInt64();
                    return true;
                case ProtoTypeCode.Boolean:
                    value = this.ReadBoolean();
                    return true;
                case ProtoTypeCode.SByte:
                    value = this.ReadSByte();
                    return true;
                case ProtoTypeCode.Byte:
                    value = this.ReadByte();
                    return true;
                case ProtoTypeCode.Char:
                    value = (char)this.ReadUInt16();
                    return true;
                case ProtoTypeCode.Double:
                    value = this.ReadDouble();
                    return true;
                case ProtoTypeCode.Single:
                    value = this.ReadSingle();
                    return true;
                case ProtoTypeCode.DateTime:
                    value = BclHelpers.ReadDateTime(this);
                    return true;
                case ProtoTypeCode.Decimal:
                    value = BclHelpers.ReadDecimal(this);
                    return true;
                case ProtoTypeCode.String:
                    value = this.ReadString();
                    return true;
                case ProtoTypeCode.ByteArray:
                    value = AppendBytes((byte[])value, this);
                    return true;
                case ProtoTypeCode.TimeSpan:
                    value = BclHelpers.ReadTimeSpan(this);
                    return true;
                case ProtoTypeCode.Guid:
                    value = BclHelpers.ReadGuid(this);
                    return true;
                case ProtoTypeCode.Uri:
                    value = new Uri(this.ReadString());
                    return true;
                case ProtoTypeCode.Type:
                    if (!allowSystemType) return false;
                    value = this.ReadType();
                    return true;
            }
            return false;
        }

#if NO_GENERICS
        readonly Stack _trapNoteReserved = new Stack();
#else
        readonly Stack<int> _trapNoteReserved = new Stack<int>();
#endif
        int _trappedKey;

        public static int ReserveNoteObject(ProtoReader reader)
        {
            if (reader._trapCount == 0) return -1;
            reader._trapNoteReserved.Push(reader._trappedKey);
            reader._trapCount--;
            return reader._trappedKey;
        }

        internal void TrapNextObject(int newObjectKey)
        {
            _trapCount++;
            if (_trapCount > 1)
                throw new ProtoException("Trap count > 1, will be mismatched with next NoteObject");
            _netCache.SetKeyedObject(newObjectKey, null); // use null as a temp
            _trappedKey = newObjectKey;
        }

        internal void CheckFullyConsumed()
        {
            if (_isFixedLength)
            {
                if (_dataRemaining != 0) throw new ProtoException("Incorrect number of bytes consumed");
            }
            else
            {
                if (_available != 0) throw new ProtoException("Unconsumed data left in the buffer; this suggests corrupt input");
            }
        }
#region RECYCLER

        internal static ProtoReader Create(Stream source, TypeModel model, SerializationContext context, int len)
        {
            ProtoReader reader = GetRecycled();
            if (reader == null)
            {
                return new ProtoReader(source, model, context, len);
            }
            Init(reader, source, model, context, len);
            return reader;
        }

#if !PLAT_NO_THREADSTATIC
        [ThreadStatic]
        private static ProtoReader _lastReader;

        private static ProtoReader GetRecycled()
        {
            ProtoReader tmp = _lastReader;
            _lastReader = null;
            return tmp;
        }
        internal static void Recycle(ProtoReader reader)
        {
            if(reader != null)
            {
                reader.Dispose();
                _lastReader = reader;
            }
        }
#elif !PLAT_NO_INTERLOCKED
        private static object lastReader;
        private static ProtoReader GetRecycled()
        {
            return (ProtoReader)System.Threading.Interlocked.Exchange(ref lastReader, null);
        }
        internal static void Recycle(ProtoReader reader)
        {
            if(reader != null)
            {
                reader.Dispose();
                System.Threading.Interlocked.Exchange(ref lastReader, reader);
            }
        }
#else
        private static readonly object recycleLock = new object();
        private static ProtoReader lastReader;
        private static ProtoReader GetRecycled()
        {
            lock(recycleLock)
            {
                ProtoReader tmp = lastReader;
                lastReader = null;
                return tmp;
            }            
        }
        internal static void Recycle(ProtoReader reader)
        {
            if(reader != null)
            {
                reader.Dispose();
                lock(recycleLock)
                {
                    lastReader = reader;
                }
            }
        }
#endif

#endregion
    }
}
