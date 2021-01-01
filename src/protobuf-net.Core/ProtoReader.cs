
using ProtoBuf.Internal;
// Modified by Vladyslav Taranov for AqlaSerializer, 2021
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq; using AltLinq;
using System.Buffers;
#if !NO_GENERICS
using System.Collections.Generic;
#endif
using System.IO;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using AqlaSerializer.Meta;
using AqlaSerializer.Serializers;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
#endif

namespace AqlaSerializer
{
    /// <summary>
    /// A stateful reader, used to read a protobuf stream. Typical usage would be (sequentially) to call
    /// ReadFieldHeader and (after matching the field) an appropriate Read* method.
    /// </summary>
    public abstract partial class ProtoReader : IDisposable, ISerializationContext
    {
        internal const string PreferStateAPI = "If possible, please use the State API; a transitionary implementation is provided, but this API may be removed in a future version",
            PreferReadMessage = "If possible, please use the ReadMessage API; this API may not work correctly with all readers";

        private protected abstract int ImplTryReadUInt64VarintWithoutMoving(ref State state, out ulong value);

        private protected abstract void ImplSkipBytes(ref State state, long count);

        private protected abstract uint ImplReadUInt32Fixed(ref State state);
        private protected virtual void ImplReadBytes(ref State state, ReadOnlySequence<byte> target)
        {
            if (target.IsSingleSegment)
            {
                ImplReadBytes(ref state, MemoryMarshal.AsMemory(target.First).Span);
            }
            else
            {
                foreach (var segment in target)
                {
                    ImplReadBytes(ref state, MemoryMarshal.AsMemory(segment).Span);
                }
            }
        }
        private protected abstract ulong ImplReadUInt64Fixed(ref State state);
        
        internal NetObjectKeyPositionsList NetCacheKeyPositionsList { get; private set; } = new NetObjectKeyPositionsList();

        private protected abstract string ImplReadString(ref State state, int bytes);
        
        long _blockEnd64;
        TypeModel _model;

        private protected abstract bool IsFullyConsumed(ref State state);

        private protected abstract int ImplTryReadUInt32VarintWithoutMoving(ref State state, Read32VarintMode mode, out uint value);
        int _fieldNumber, _depth;
        private protected abstract void ImplReadBytes(ref State state, Span<byte> target);

        /// <summary>
        /// Gets the number of the field being processed.
        /// </summary>
        public int FieldNumber
        {
            [MethodImpl(HotPath)]
            get => _fieldNumber;
        }

        /// <summary>
        /// Indicates the underlying proto serialization format on the wire.
        /// </summary>
        public WireType WireType
        {
            [MethodImpl(HotPath)]
            get;
            private protected set;
        }

        public virtual bool CanSeek => false;
        public bool AllowReferenceVersioningSeeking => CanSeek && (_model?.AllowReferenceVersioningSeeking ?? true);
        
        internal const int TO_EOF = -1;

        /// <summary>
        /// Gets / sets a flag indicating whether strings should be checked for repetition; if
        /// true, any repeated UTF-8 byte sequence will result in the same String instance, rather
        /// than a second instance of the same string. Enabled by default. Note that this uses
        /// a <i>custom</i> interner - the system-wide string interner is not used.
        /// </summary>
        public bool InternStrings { get; set; }

        private protected ProtoReader() { }

        internal void Init(TypeModel model, SerializationContext context)
        {
            this._model = model;
            if (context == null) { context = SerializationContext.Default; }
            else { context.Freeze(); }
            this._context = context;
            this._blockEnd64 = long.MaxValue;
            this.InternStrings = (model ?? RuntimeTypeModel.Default).InternStrings;
            this.WireType = WireType.None;
            this._trapCount = 0;
            this._trappedKey = 0;
            this._trapNoteReserved.Clear();
            this._expectRoot = false;
            if(this._netCache == null) this._netCache = new NetObjectCache();
            this._netCache.ResetRoot();
        }

        private SerializationContext _context;
        
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
        
        private protected enum Read32VarintMode
        {
            Signed,
            Unsigned,
            FieldHeader,
        }

        /// <summary>
        /// Releases resources used by the reader, but importantly <b>does not</b> Dispose the 
        /// underlying stream; in many typical use-cases the stream is used for different
        /// processes, so it is assumed that the consumer will Dispose their stream separately.
        /// </summary>
        public virtual void OnDispose()
        {
            _model = null;
            if(_stringInterner != null)
            {
                _stringInterner.Clear();
                _stringInterner = null;
            }

            _netCache?.Clear();
            _lateReferences.Reset();
            NetCacheKeyPositionsList.Reset();
        }

        #endregion

        internal abstract void OnInit();

        /// <summary>
        /// Addition information about this deserialization operation.
        /// </summary>
        public object UserState { get; private set; }

        /// <summary>
        /// Addition information about this deserialization operation.
        /// </summary>
        public SerializationContext Context => _context;

        /// <summary>
        /// Releases resources used by the reader, but importantly <b>does not</b> Dispose the 
        /// underlying stream; in many typical use-cases the stream is used for different
        /// processes, so it is assumed that the consumer will Dispose their stream separately.
        /// </summary>
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize - no intention of supporting finalizers here
        public virtual void Dispose()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        {
            OnDispose();
            _model = null;
            if (stringInterner is object)
            {
                stringInterner.Clear();
                stringInterner = null;
            }
            netCache.Clear();
        }
        
        public long BlockEndPosition
        {
            get { return _blockEnd64; }
            internal set { _blockEnd64 = value; }
        }

        /// <summary>
        /// Returns the position of the current reader (note that this is not necessarily the same as the position
        /// in the underlying stream, if multiple readers are used on the same stream)
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public int Position => checked((int)_longPosition);

        long _longPosition;


        /// <summary>
        /// Returns the position of the current reader (note that this is not necessarily the same as the position
        /// in the underlying stream, if multiple readers are used on the same stream)
        /// </summary>
        public long LongPosition
        {
            [MethodImpl(HotPath)]
            get => _longPosition;
        }

        [MethodImpl(HotPath)]
        internal void Advance(long count) => _longPosition += count;

/// <summary>
/// Reads a signed 16-bit integer from the stream: Variant, Fixed32, Fixed64, SignedVariant
/// </summary>
[MethodImpl(HotPath)]
public short ReadInt16() => DefaultState().ReadInt16();


        /// <summary>
        /// Reads an unsigned 16-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        [MethodImpl(HotPath)]
        public ushort ReadUInt16() => DefaultState().ReadUInt16();

        /// <summary>
        /// Reads an unsigned 8-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        [MethodImpl(HotPath)]
        public byte ReadByte() => DefaultState().ReadByte();

        /// <summary>
        /// Reads a signed 8-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        [MethodImpl(HotPath)]
        public sbyte ReadSByte() => DefaultState().ReadSByte();

        /// <summary>
        /// Reads an unsigned 32-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        [MethodImpl(HotPath)]
        public uint ReadUInt32() => DefaultState().ReadUInt32();

        /// <summary>
        /// Reads a signed 32-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        [MethodImpl(HotPath)]
        public int ReadInt32() => DefaultState().ReadInt32();

        /// <summary>
        /// Reads a signed 64-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        [MethodImpl(HotPath)]
        public long ReadInt64() => DefaultState().ReadInt64();

        private Dictionary<string, string> _stringInterner;
        private protected string Intern(string value)
        {
            if (value == null) return null;
            if (value.Length == 0) return "";
            if (_stringInterner == null)
            {
                _stringInterner = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { value, value }
                };
            }
            else if (_stringInterner.TryGetValue(value, out string found))
            {
                value = found;
            }
            else
            {
                _stringInterner.Add(value, value);
            }
            return value;
        }

        public delegate ref State RefStateGetDelegate();
        public delegate void RefStateSetDelegate(ref State state);

        /// <summary>
        /// Behavior same as <see cref="ListHelpers"/> with ProtoCompatibility = off (except max array size). Don't read anything in-between elements!
        /// </summary>
        internal IEnumerable<T> ReadArrayContentIterating<T>(Func<T> reader, RefStateGetDelegate stateGetter, RefStateSetDelegate stateSetter)
        {
            // TODO update
            var t = StartSubItem(this);
            if (ReadFieldHeader() != ListHelpers.FieldLength) throw new ProtoException("Array length expected");
            int count = ReadInt32();

            while (ReadFieldHeader() > 0 && FieldNumber != ListHelpers.FieldItem)
                SkipField();

            var wt = WireType;

            try
            {
                for (int i = 0; i < count; i++)
                {
                    if (i != 0 && !HasSubValue(wt, this)) throw new ProtoException("Expected array item but not found");
                    yield return reader();
                }
            }
            finally
            {
                var state = stateGetter();
                EndSubItem(t, true, this, ref state);
                stateSetter(ref state);
            }
        }
        private protected static readonly UTF8Encoding UTF8 = new UTF8Encoding();

        /// <summary>
        /// Reads a string from the stream (using UTF8); supported wire-types: String
        /// </summary>
        [MethodImpl(HotPath)]
        public string ReadString() => DefaultState().ReadString();

        /// <summary>
        /// Throws an exception indication that the given value cannot be mapped to an enum.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ThrowEnumException(Type type, int value) => DefaultState().ThrowEnumException(type, value);


        /// <summary>
        /// Reads a double-precision number from the stream; supported wire-types: Fixed32, Fixed64
        /// </summary>
        [MethodImpl(HotPath)]
        public double ReadDouble() => DefaultState().ReadDouble();



        /// <summary>
        /// Reads (merges) a sub-message from the stream, internally calling StartSubItem and EndSubItem, and (in between)
        /// parsing the message in accordance with the model associated with the reader
        /// </summary>
        [MethodImpl(HotPath)]
        public static object ReadObject(object value, [DynamicallyAccessedMembers(DynamicAccess.ContractType)] Type type, ProtoReader reader)
            => reader.DefaultState().ReadObject(value, type);

        // not used anymore because we don't want aux on members
        internal static object ReadTypedObject(ProtoReader reader, ref State state, object value, int key, Type type)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (reader._model == null)
            {
                throw AddErrorData(new InvalidOperationException("Cannot deserialize sub-objects unless a model is provided"), reader, ref state);
            }
            SubItemToken token = ProtoReader.StartSubItem(reader, ref state);
            if (key >= 0)
            {
                value = reader._model.DeserializeCore(reader, ref state, key, value);
            }
            else if (type != null && reader._model.TryDeserializeAuxiliaryType(reader, ref state, BinaryDataFormat.Default, Serializer.ListItemTag, type, ref value, true, false, true, false, false))
            {
                // ok
            }
            else
            {
                TypeModel.ThrowUnexpectedType(type);
            }
            ProtoReader.EndSubItem(token, reader, ref state);
            return value;
#endif
        }

        /// <summary>
        /// Makes the end of consuming a nested message in the stream; the stream must be either at the correct EndGroup
        /// marker, or all fields of the sub-message must have been consumed (in either case, this means ReadFieldHeader
        /// should return zero)
        /// </summary>
        [MethodImpl(HotPath)]
        public static void EndSubItem(SubItemToken token, ProtoReader reader)
            => reader.DefaultState().EndSubItem(token);

        /// <summary>
        /// Makes the end of consuming a nested message in the stream; the stream must be either at the correct EndGroup
        /// marker, or all fields of the sub-message must have been consumed (in either case, this means ReadFieldHeader
        /// should return zero)
        /// </summary>
        public static void EndSubItem(SubItemToken token, ProtoReader reader, ref State state)
        {
            EndSubItem(token, false, reader, ref state);
        }

        /// <summary>
        /// Makes the end of consuming a nested message in the stream; the stream must be either at the correct EndGroup
        /// marker, or all fields of the sub-message must have been consumed (in either case, this means ReadFieldHeader
        /// should return zero)
        /// </summary>
        public static void EndSubItem(SubItemToken token, bool skipToEnd, ProtoReader reader, ref State state)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            long value64 = token.Value64;
            if (value64 == int.MinValue)
            {
                // should not overwrite last read result
                // what if we reached outer subitem end?
                //reader.wireType = WireType.None;
                return;
            }
            if (skipToEnd)
                while (reader.ReadFieldHeader() != 0) reader.SkipField(); // skip field will recursively go through nested objects

            switch (reader.WireType)
            {
                case WireType.EndGroup:
                    if (value64 >= 0) throw AddErrorData(new ArgumentException("token"), reader, ref state);
                    if (-(int)value64 != reader._fieldNumber) throw reader.CreateException(ref state, "Wrong group was ended"); // wrong group ended!
                    reader.WireType = WireType.None; // this releases ReadFieldHeader
                    reader._depth--;
                    break;
                // case WireType.None: // TODO reinstate once reads reset the wire-type
                default:
                    long position = reader._longPosition;
                    if (value64 < position) throw reader.CreateException(ref state, $"Sub-message not read entirely; expected {value64}, was {position}");
                    if (reader._blockEnd64 != position && reader._blockEnd64 != long.MaxValue)
                    {
                        throw reader.CreateException(ref state, $"Sub-message not read correctly (end {reader._blockEnd64} vs {position})");
                    }
                    reader._blockEnd64 = value64;
                    reader._depth--;
                    break;
                    /*default:
                        throw reader.BorkedIt(); */
            }
        }

        /// <summary>
        /// Begins consuming a nested message in the stream; supported wire-types: StartGroup, String
        /// </summary>
        /// <remarks>The token returned must be help and used when callining EndSubItem</remarks>
        [MethodImpl(HotPath)]
        public static SubItemToken StartSubItem(ProtoReader reader)
            => reader.DefaultState().StartSubItem();
        /// <summary>
        /// Begins consuming a nested message in the stream; supported wire-types: StartGroup, String
        /// </summary>
        /// <remarks>The token returned must be help and used when callining EndSubItem</remarks>
        public static SubItemToken StartSubItem(ProtoReader reader, ref State state)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (reader._expectRoot)
            {
                reader._expectRoot = false;
                return new SubItemToken(int.MinValue);
            }
            switch (reader.WireType)
            {
                case WireType.StartGroup:
                    reader.WireType = WireType.None; // to prevent glitches from double-calling
                    reader._depth++;
                    if (reader._depth > (reader._model?.RecursionDepthLimit ?? TypeModel.DefaultRecursionDepthLimit))
                        TypeModel.ThrowRecursionDepthLimitExceeded();
                    return new SubItemToken((long)(-reader._fieldNumber));
                case WireType.String:
                    long len = (long)reader.ReadUInt64Varint(ref state);
                    if (len < 0) throw AddErrorData(new InvalidOperationException(), reader, ref state);
                    long lastEnd = reader._blockEnd64;
                    reader._blockEnd64 = reader._longPosition + len;
                    reader._depth++;
                    if (reader._depth > (reader._model?.RecursionDepthLimit ?? TypeModel.DefaultRecursionDepthLimit))
                        TypeModel.ThrowRecursionDepthLimitExceeded();
                    return new SubItemToken(lastEnd);
                default:
                    throw reader.CreateWireTypeException(ref state); // throws
            }
        }

        /// <summary>
        /// Reads a field header from the stream, setting the wire-type and retuning the field number. If no
        /// more fields are available, then 0 is returned. This methods respects sub-messages.
        /// </summary>
        [MethodImpl(HotPath)]
        public int ReadFieldHeader() => DefaultState().ReadFieldHeader();

        /// <summary>
        /// Reads a field header from the stream, setting the wire-type and retuning the field number. If no
        /// more fields are available, then 0 is returned. This methods respects sub-messages.
        /// </summary>
        public int ReadFieldHeader(ref State state)
        {
            _expectRoot = false;
            // at the end of a group the caller must call EndSubItem to release the
            // reader (which moves the status to Error, since ReadFieldHeader must
            // then be called)
            if (_blockEnd64 <= _longPosition || WireType == WireType.EndGroup) { return 0; }

            if (state.RemainingInCurrent >= 5)
            {
                var read = state.ReadVarintUInt32(out var tag);
                Advance(read);
                return SetTag(tag);
            }
            return ReadFieldHeaderFallback(ref state);
        }

        [MethodImpl(HotPath)]
        private int SetTag(uint tag)
        {
            if ((_fieldNumber = (int)(tag >> 3)) < 1) ThrowInvalidField(_fieldNumber);
            if ((WireType = (WireType)(tag & 7)) == WireType.EndGroup)
            {
                if (_depth > 0) return 0; // spoof an end, but note we still set the field-number
                ThrowUnexpectedEndGroup();
            }
            return _fieldNumber;
        }
        private static void ThrowInvalidField(int fieldNumber)
            => ThrowHelper.ThrowProtoException("Invalid field in source data: " + fieldNumber.ToString());
        private static void ThrowUnexpectedEndGroup()
            => ThrowHelper.ThrowProtoException("Unexpected end-group in source data; this usually means the source data is corrupt");

        /// <summary>
        /// Looks ahead to see whether the next field in the stream is what we expect
        /// (typically; what we've just finished reading - for example ot read successive list items)
        /// </summary>
        [MethodImpl(HotPath)]
        public bool TryReadFieldHeader(int field) => DefaultState().TryReadFieldHeader(field);

        /// <summary>
        /// Looks ahead to see whether the next field in the stream is what we expect
        /// (typically; what we've just finished reading - for example ot read successive list items)
        /// </summary>
        public bool TryReadFieldHeader(ref State state, int field)
        {
            _expectRoot = false; // TODO move inside true branch?
            // check for virtual end of stream
            if (_blockEnd64 <= _longPosition || WireType == WireType.EndGroup) { return false; }

            int read = ImplTryReadUInt32VarintWithoutMoving(ref state, Read32VarintMode.FieldHeader, out uint tag);
            WireType tmpWireType; // need to catch this to exclude (early) any "end group" tokens
            if (read > 0 && ((int)tag >> 3) == field
                && (tmpWireType = (WireType)(tag & 7)) != WireType.EndGroup)
            {
                WireType = tmpWireType;
                _fieldNumber = field;
                ImplSkipBytes(ref state, read);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Looks ahead to see the next field in the stream
        /// </summary>
        public int? TryPeekFieldHeader(ref State state)
        {
            _expectRoot = false; // TODO move inside true branch?
            // check for virtual end of stream
            if (_blockEnd64 <= _longPosition || WireType == WireType.EndGroup) { return 0; }

            int read = ImplTryReadUInt32VarintWithoutMoving(ref state, Read32VarintMode.FieldHeader, out uint tag);
            if (read > 0)
            {
                if (((WireType) (tag & 7)) == WireType.EndGroup) return 0;
                return ((int)tag >> 3);
            }
            return null;
        }

        public bool IsExpectingRoot => _expectRoot;

        bool _expectRoot;

        /// <summary>
        /// Next StartSubItem call will be ignored unless ReadFieldHeader is called
        /// </summary>
        public static void ExpectRoot(ProtoReader reader)
        {
            reader._expectRoot = true;
        }

        /// <summary>
        /// Get the TypeModel associated with this reader
        /// </summary>
        public TypeModel Model
        {
            get => _model;
            internal set => _model = value;
        }

        /// <summary>
        /// Compares the streams current wire-type to the hinted wire-type, updating the reader if necessary; for example,
        /// a Variant may be updated to SignedVariant. If the hinted wire-type is unrelated then no change is made.
        /// </summary>
        [MethodImpl(HotPath)]
        public void Hint(WireType wireType)
        {
            if (WireType == wireType) { }  // fine; everything as we expect
            else if (((int)wireType & 7) == (int)this.WireType)
            {   // the underling type is a match; we're customising it with an extension
                WireType = wireType;
            }
            // note no error here; we're OK about using alternative data
        }

        /// <summary>
        /// Verifies that the stream's current wire-type is as expected, or a specialized sub-type (for example,
        /// SignedVariant) - in which case the current wire-type is updated. Otherwise an exception is thrown.
        /// </summary>
        [MethodImpl(HotPath)]
        public void Assert(WireType wireType) => DefaultState().Assert(wireType);

        /// <summary>
        /// Discards the data for the current field.
        /// </summary>
        [MethodImpl(HotPath)]
        public void SkipField() => DefaultState().SkipField();

        /// <summary>
        /// Discards the data for the current field.
        /// </summary>
        public void SkipField(ref State state)
        {
            switch (WireType)
            {
                case WireType.Null:
                    return;
                case WireType.Fixed32:
                    ImplSkipBytes(ref state, 4);
                    return;
                case WireType.Fixed64:
                    ImplSkipBytes(ref state, 8);
                    return;
                case WireType.String:
                    long len = (long)ReadUInt64Varint(ref state);
                    ImplSkipBytes(ref state, len);
                    return;
                case WireType.Variant:
                case WireType.SignedVariant:
                    ReadUInt64Varint(ref state); // and drop it
                    return;
                case WireType.StartGroup:
                    int originalFieldNumber = this._fieldNumber;
                    _depth++; // need to satisfy the sanity-checks in ReadFieldHeader
                    while (ReadFieldHeader(ref state) > 0) { SkipField(ref state); }
                    _depth--;
                    if (WireType == WireType.EndGroup && _fieldNumber == originalFieldNumber)
                    { // we expect to exit in a similar state to how we entered
                        WireType = WireType.None;
                        return;
                    }
                    throw CreateWireTypeException(ref state);
                case WireType.None: // treat as explicit errorr
                case WireType.EndGroup: // treat as explicit error
                default: // treat as implicit error
                    throw CreateWireTypeException(ref state);
            }
        }

        /// <summary>
        /// Reads an unsigned 64-bit integer from the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        [MethodImpl(HotPath)]
        public ulong ReadUInt64() => DefaultState().ReadUInt64();

        /// <summary>
        /// Reads a single-precision number from the stream; supported wire-types: Fixed32, Fixed64
        /// </summary>
        [MethodImpl(HotPath)]
        public float ReadSingle() => DefaultState().ReadSingle();

        /// <summary>
        /// Reads a boolean value from the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        [MethodImpl(HotPath)]
        public bool ReadBoolean() => DefaultState().ReadBoolean();
        
        public abstract long ImplSeekAndExchangeBlockEnd(ref State state, long anyPositionFromRootReaderStart, long newBlockEnd = long.MaxValue);
        
        private static readonly byte[] EmptyBlob = new byte[0];

        /// <summary>
        /// Reads a byte-sequence from the stream, appending them to an existing byte-sequence (which can be null); supported wire-types: String
        /// </summary>
        [MethodImpl(HotPath)]
        public static byte[] AppendBytes(byte[] value, ProtoReader reader)
            => reader.DefaultState().AppendBytes(value);

        //static byte[] ReadBytes(Stream stream, int length)
        //{
        //    if (stream is null) ThrowHelper.ThrowArgumentNullException("stream");
        //    if (length < 0) ThrowHelper.ThrowArgumentOutOfRangeException("length");
        //    byte[] buffer = new byte[length];
        //    int offset = 0, read;
        //    while (length > 0 && (read = stream.Read(buffer, offset, length)) > 0)
        //    {
        //        length -= read;
        //    }
        //    if (length > 0) ThrowEoF();
        //    return buffer;
        //}

        private static int ReadByteOrThrow(Stream source)
        {
            int val = source.ReadByte();
            if (val < 0) ThrowEoF();
            return val;
        }

        /// <summary>
        /// Reads the length-prefix of a message from a stream without buffering additional data, allowing a fixed-length
        /// reader to be created.
        /// </summary>
        public static int ReadLengthPrefix(Stream source, bool expectHeader, PrefixStyle style, out int fieldNumber)
            => ReadLengthPrefix(source, expectHeader, style, out fieldNumber, out int _);

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
            int bytes = TryReadUInt64Varint(source, out ulong val);
            if (bytes <= 0) ThrowEoF();
            return checked((int)val);
        }

        /// <summary>
        /// Reads a string (of a given lenth, in bytes) directly from the source into a pre-existing buffer. An exception is thrown if the data is not all available.
        /// </summary>
        public static void DirectReadBytes(Stream source, byte[] buffer, int offset, int count)
        {
            int read;
            if (source is null) ThrowHelper.ThrowArgumentNullException(nameof(source));
            while (count > 0 && (read = source.Read(buffer, offset, count)) > 0)
            {
                count -= read;
                offset += read;
            }
            if (count > 0) ThrowEoF();
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
            return ProtoWriter.UTF8.GetString(buffer, 0, length);
        }

        /// <summary>
        /// Reads the length-prefix of a message from a stream without buffering additional data, allowing a fixed-length
        /// reader to be created.
        /// </summary>
        public static int ReadLengthPrefix(Stream source, bool expectHeader, PrefixStyle style, out int fieldNumber, out int bytesRead)
        {
            if (style == PrefixStyle.None)
            {
                bytesRead = fieldNumber = 0;
                return int.MaxValue; // avoid the long.maxvalue causing overflow
            }
            long len64 = ReadLongLengthPrefix(source, expectHeader, style, out fieldNumber, out bytesRead);
            return checked((int)len64);
        }

        /// <summary>
        /// Reads the length-prefix of a message from a stream without buffering additional data, allowing a fixed-length
        /// reader to be created.
        /// </summary>
        public static long ReadLongLengthPrefix(Stream source, bool expectHeader, PrefixStyle style, out int fieldNumber, out int bytesRead)
        {
            fieldNumber = 0;
            switch (style)
            {
                case PrefixStyle.None:
                    bytesRead = 0;
                    return long.MaxValue;
                case PrefixStyle.Base128:
                    ulong val;
                    int tmpBytesRead;
                    bytesRead = 0;
                    if (expectHeader)
                    {
                        tmpBytesRead = ProtoReader.TryReadUInt64Varint(source, out val);
                        bytesRead += tmpBytesRead;
                        if (tmpBytesRead > 0)
                        {
                            if ((val & 7) != (uint)WireType.String)
                            { // got a header, but it isn't a string
                                ThrowHelper.ThrowInvalidOperationException($"Unexpected wire-type: {(WireType)(val & 7)}, expected {WireType.String})");
                            }
                            fieldNumber = (int)(val >> 3);
                            tmpBytesRead = ProtoReader.TryReadUInt64Varint(source, out val);
                            bytesRead += tmpBytesRead;
                            if (bytesRead == 0) ThrowEoF(); // got a header, but no length
                            return (long)val;
                        }
                        else
                        { // no header
                            bytesRead = 0;
                            return -1;
                        }
                    }
                    // check for a length
                    tmpBytesRead = ProtoReader.TryReadUInt64Varint(source, out val);
                    bytesRead += tmpBytesRead;
                    return bytesRead < 0 ? -1 : (long)val;

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
                    ThrowHelper.ThrowArgumentOutOfRangeException(nameof(style));
                    bytesRead = default;
                    return default;
            }
        }

        /// <summary>Read a varint if possible</summary>
        /// <returns>The number of bytes consumed; 0 if no data available</returns>
        private static int TryReadUInt64Varint(Stream source, out ulong value)
        {
            value = 0;
            int b = source.ReadByte();
            if (b < 0) { return 0; }
            value = (uint)b;
            if ((value & 0x80) == 0) { return 1; }
            value &= 0x7F;
            int bytesRead = 1, shift = 7;
            while (bytesRead < 9)
            {
                b = source.ReadByte();
                if (b < 0) ThrowEoF();
                value |= ((ulong)b & 0x7F) << shift;
                shift += 7;
                bytesRead++;

                if ((b & 0x80) == 0) return bytesRead;
            }
            b = source.ReadByte();
            if (b < 0) ThrowEoF();
            if ((b & 1) == 0) // only use 1 bit from the last byte
            {
                value |= ((ulong)b & 0x7F) << shift;
                return ++bytesRead;
            }
            ThrowHelper.ThrowOverflowException();
            return default;
        }

        internal static void Seek(Stream source, long count, byte[] buffer)
        {
            if (source.CanSeek)
            {
                source.Seek(count, SeekOrigin.Current);
                count = 0;
            }
            else if (buffer is object)
            {
                int bytesRead;
                while (count > buffer.Length && (bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    count -= bytesRead;
                }
                while (count > 0 && (bytesRead = source.Read(buffer, 0, (int)count)) > 0)
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
                    while (count > 0 && (bytesRead = source.Read(buffer, 0, (int)count)) > 0)
                    {
                        count -= bytesRead;
                    }
                }
                finally
                {
                    BufferPool.ReleaseBufferToPool(ref buffer);
                }
            }
            if (count > 0) ThrowEoF();
        }

        /// <summary>
        /// Copies the current field into the instance as extension data
        /// </summary>
        [MethodImpl(HotPath)]
        public void AppendExtensionData(IExtensible instance) => DefaultState().AppendExtensionData(instance);

        /// <summary>
        /// Indicates whether the reader still has data remaining in the current sub-item,
        /// additionally setting the wire-type for the next field if there is more data.
        /// This is used when decoding packed data.
        /// </summary>
        public static bool HasSubValue(WireType wireType, ProtoReader source)
        {
            if (source is null) ThrowHelper.ThrowArgumentNullException(nameof(source));
            // check for virtual end of stream
            if (source._blockEnd64 <= source._longPosition || wireType == WireType.EndGroup) { return false; }
            source.WireType = wireType;
            return true;
        }

        /// <summary>
        /// Field Number is not written for ignored fields but when group is ended the group number is written (equal to specified field number)
        /// </summary>
        internal const int GroupNumberForIgnoredFields = 1;
        
        /// <summary>
        /// Reads a Type from the stream, using the model's DynamicTypeFormatting if appropriate; supported wire-types: String
        /// </summary>
        public System.Type ReadType()
        {
            return TypeModel.DeserializeType(_model, ReadString());
        }

        #region Reference tracking

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
            public readonly NetObjectKeyPositionsList NetObjectKeyPositionsList;

            public ReferenceState(NetObjectCache netObjectCache, LateReferencesCache lateReferencesCache, NetObjectKeyPositionsList netObjectKeyPositionsList)
            {
                LateReferencesCache = lateReferencesCache;
                NetObjectKeyPositionsList = netObjectKeyPositionsList;
                NetObjectCache = netObjectCache;
            }
        }

        internal object StoreReferenceState()
        {
            return new ReferenceState(_netCache.Clone(), _lateReferences.Clone(), NetCacheKeyPositionsList.Clone());
        }

        internal void LoadReferenceState(object state)
        {
            var s = (ReferenceState)state;
            _lateReferences = s.LateReferencesCache.Clone();
            _netCache = s.NetObjectCache.Clone();
            NetCacheKeyPositionsList = s.NetObjectKeyPositionsList.Clone();
        }

        /// <summary>
        /// Utility method, not intended for public use; this helps maintain the root object is complex scenarios
        /// </summary>
        public static void NoteObject(object value, ProtoReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (reader._trapCount != 0)
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
            var trueKey = (int)stack.Peek();
            if (trappedKey != trueKey) throw new InvalidOperationException("NoteReservedTrappedObject called for " + trappedKey + " but waiting for " + trueKey);
            stack.Pop();
            reader._netCache.SetKeyedObject(trueKey, value);
        }

        readonly Stack<int> _trapNoteReserved = new Stack<int>();
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowEoF() => default(State).ThrowEoF();
    }
}

