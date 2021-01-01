// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using System.Text;
using System.Xml;
using AltLinq; using System.Linq;
using AqlaSerializer.Meta;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AqlaSerializer.Serializers;
using ProtoBuf.Internal;
using ProtoBuf.Serializers;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
#endif

namespace AqlaSerializer
{
    /// <summary>
    /// <para>Represents an output stream for writing protobuf data.</para>
    /// <para>
    /// Why is the API backwards (static methods with writer arguments)?
    /// See: http://marcgravell.blogspot.com/2010/03/last-will-be-first-and-first-will-be.html
    /// </para>
    /// </summary>
    public abstract partial class ProtoWriter : IDisposable, ISerializationContext
    {
        private const MethodImplOptions HotPath = ProtoReader.HotPath;

        internal const string PreferWriteMessage = "If possible, please use the WriteMessage API; this API may not work correctly with all writers";
        TypeModel _model;
        
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
        /// <param name="writer">The destination.</param>
        /// <param name="state">Writer state</param>
        public static void WriteObject(object value, int key, ProtoWriter writer, ref State state)
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
                SubItemToken token = StartSubItem(value, writer, ref state);
                if (writer._model.TrySerializeAuxiliaryType(writer, value.GetType(), BinaryDataFormat.Default, Serializer.ListItemTag, value, false, false))
                {
                    // all ok
                }
                else
                {
                    TypeModel.ThrowUnexpectedType(value.GetType());
                }
                EndSubItem(token, writer, ref state);
            }
            else
            {
                TypeModel.ThrowUnexpectedType(value.GetType());
            }
#endif 
        }
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

#if FEAT_DYNAMIC_REF
        /// <summary>
        /// Write an encapsulated sub-object, using the supplied unique key (reprasenting a type).
        /// </summary>
        /// <param name="value">The object to write.</param>
        /// <param name="type">The key that uniquely identifies the type within the model.</param>
        /// <param name="writer">The destination.</param>
        [MethodImpl(HotPath)]
        public static void WriteObject(object value, Type type, ProtoWriter writer)
            => writer.DefaultState().WriteObject(value, type);
#endif

        private protected readonly NetObjectCache netCache;
        /// <summary>
        /// Write an encapsulated sub-object, using the supplied unique key (reprasenting a type) - but the
        /// caller is asserting that this relationship is non-recursive; no recursion check will be
        /// performed.
        /// </summary>
        /// <param name="value">The object to write.</param>
        /// <param name="key">The key that uniquely identifies the type within the model.</param>
        /// <param name="writer">The destination.</param>
        public static void WriteRecursionSafeObject(object value, int key, ProtoWriter writer, ref State state)
        {
            // no argument checks here for performance
            writer._model.Serialize(key, value, writer, false);
        }

        //internal static void WriteAuxiliaryObject(int tag, object value, ProtoWriter writer)
        //{
        //    writer._model.TrySerializeAuxiliaryType(writer, value.GetType(), BinaryDataFormat.Default, tag, value, false, false);
        //}

        // not used anymore because we don't want aux on members

        internal static void WriteObject(object value, int key, ProtoWriter writer, PrefixStyle style, int fieldNumber, ref State state)
        {
            WriteObject(value, key, writer, style, fieldNumber, false, ref state);
        }

        internal static void WriteObject(object value, int key, ProtoWriter writer, PrefixStyle style, int fieldNumber, bool isRoot, ref State state)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (writer._model == null)
            {
                throw new InvalidOperationException("Cannot serialize sub-objects unless a model is provided");
            }
            if (writer.WireType != WireType.None || writer._fieldStarted) throw ProtoWriter.CreateException(writer);

            switch (style)
            {
                case PrefixStyle.Base128:
                    writer.WireType = WireType.String;
                    writer._fieldNumber = fieldNumber;
                    if (fieldNumber > 0) WriteHeaderCore(fieldNumber, WireType.String, writer, ref state);
                    break;
                case PrefixStyle.Fixed32:
                case PrefixStyle.Fixed32BigEndian:
                    writer._fieldNumber = 0;
                    writer.WireType = WireType.Fixed32;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(style));
            }

            SubItemToken token = writer.StartSubItem(ref state, value, style);
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
            writer.EndSubItem(ref state, token, style);
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
        public WireType WireType { get; internal set; }
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
            if (writer.WireType != WireType.None)
                throw new InvalidOperationException(
                    "Cannot write a field number " + fieldNumber
                    + " until the " + writer.WireType.ToString() + " data has been written");

            if (writer._fieldStarted) throw new InvalidOperationException("Cannot write a field number until a wire type for field " + writer._fieldNumber + " has been written");
            writer._expectRoot = false;
            writer._fieldNumber = fieldNumber;
            writer._fieldStarted = true;
        }

        /// <summary>
        /// Finished writing a field-header, indicating the format of the next data we plan to write.
        /// </summary>
        public static void WriteFieldHeaderComplete(WireType wireType, ProtoWriter writer, ref State state)
        {
#if DEBUG
            if (wireType == WireType.StartGroup) throw new InvalidOperationException("Should use StartSubItem method for nested items");
#endif
            WriteFieldHeaderCompleteAnyType(wireType, writer, ref state);
        }

        /// <summary>
        /// Cancels writing a field-header, initiated with WriteFieldHeaderBegin
        /// </summary>
        public static void WriteFieldHeaderCancelBegin(ProtoWriter writer, ref State state)
        {
            if (!writer._fieldStarted) throw CreateException(writer);
            writer._fieldNumber = 0;
            writer._fieldStarted = false;
            writer._ignoredFieldStarted = false;
        }

        /// <summary>
        /// Finished writing a field-header, indicating the format of the next data we plan to write. Any type means nested objects are allowed.
        /// </summary>
        public static void WriteFieldHeaderCompleteAnyType(WireType wireType, ProtoWriter writer, ref State state)
        {
            if (!writer._fieldStarted) throw new InvalidOperationException("Cannot write a field wire type " + wireType + " because field number has not been written");
            writer.WireType = wireType;
            writer._fieldStarted = false;
            if (writer._ignoredFieldStarted)
            {
                writer._ignoredFieldStarted = false;
                return;
            }
            WriteFieldHeaderNoCheck(writer._fieldNumber, wireType, writer, ref state);
        }

        /// <summary>
        /// Starts field header without writing it
        /// </summary>
        public static void WriteFieldHeaderIgnored(WireType wireType, ProtoWriter writer, ref State state)
        {
            if (writer._fieldStarted)
                throw new InvalidOperationException("Cannot write a field header until a wire type of the field " + writer._fieldNumber + " has been written");
            if (writer.WireType != WireType.None)
                throw new InvalidOperationException("Cannot write a field header until the " + writer.WireType.ToString() + " data has been written");

            Debug.Assert(wireType != WireType.None);

            writer._expectRoot = false;
            writer.WireType = wireType;
            writer._fieldNumber = ProtoReader.GroupNumberForIgnoredFields;
        }

        /// <summary>
        /// Writes a field-header, indicating the format of the next data we plan to write.
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteFieldHeader(int fieldNumber, WireType wireType, ProtoWriter writer)
            => writer.DefaultState().WriteFieldHeader(fieldNumber, wireType);

        /// <summary>
        /// Writes a field-header, indicating the format of the next data we plan to write.
        /// </summary>
        public static void WriteFieldHeader(int fieldNumber, WireType wireType, ProtoWriter writer, ref State state)
        {
#if DEBUG
            if (wireType == WireType.StartGroup) throw new InvalidOperationException("Should use StartSubItem method for nested items");
#endif
            WriteFieldHeaderAnyType(fieldNumber, wireType, writer, ref state);
        }

        /// <summary>
        /// Writes a field-header, indicating the format of the next data we plan to write. Any type means nested objects are allowed.
        /// </summary>
        public static void WriteFieldHeaderAnyType(int fieldNumber, WireType wireType, ProtoWriter writer, ref State state) {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (writer.WireType != WireType.None) throw new InvalidOperationException("Cannot write a " + wireType.ToString()
                + " header until the " + writer.WireType.ToString() + " data has been written");
            if(fieldNumber < 0) throw new ArgumentOutOfRangeException(nameof(fieldNumber));
            if (writer._fieldStarted) throw new InvalidOperationException("Cannot write a field until a wire type for field " + writer._fieldNumber + " has been written");
            WriteFieldHeaderNoCheck(fieldNumber, wireType, writer, ref state);
        }

        private void PreSubItem(ref State state, object instance)
        {
            if (depth < 0) state.ThrowInvalidSerializationOperation();
            if (++depth > RecursionCheckDepth)
            {
                CheckRecursionStackAndPush(instance);
            }
            if (packedFieldNumber != 0) ThrowHelper.ThrowInvalidOperationException("Cannot begin a sub-item while performing packed encoding");
        }

        /// <summary>
        /// Writes a byte-array to the stream; supported wire-types: String
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteBytes(byte[] data, ProtoWriter writer)
            => writer.DefaultState().WriteBytes(data);
        
        static void WriteFieldHeaderNoCheck(int fieldNumber, WireType wireType, ProtoWriter writer, ref State state) {
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
            writer._needFlush = true;
            writer._expectRoot = false;
            writer._fieldNumber = fieldNumber;
            if (wireType != WireType.Null)
                writer.WireType = wireType;
            else
                writer.WireType = WireType.None;
            WriteHeaderCore(fieldNumber, wireType, writer, ref state);
        }
        internal static void WriteHeaderCore(int fieldNumber, WireType wireType, ProtoWriter writer, ref State state)
        {
            uint header = (((uint)fieldNumber) << 3)
                | (((uint)wireType) & 7);
            int bytes = writer.ImplWriteVarint32(ref state, header);
            writer.Advance(bytes);
        }

        /// <summary>
        /// Writes a byte-array to the stream; supported wire-types: String
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteBytes(byte[] data, int offset, int length, ProtoWriter writer)
            => writer.DefaultState().WriteBytes(new ReadOnlyMemory<byte>(data, offset, length));

        /// <summary>
        /// Writes a byte-array to the stream; supported wire-types: String
        /// </summary>
        public static void WriteBytes(byte[] data, int offset, int length, ProtoWriter writer, ref State state)
        {
            switch (writer.WireType)
            {
                case WireType.Fixed32:
                    if (length != 4) throw new ArgumentException(nameof(length));
                    writer.ImplWriteBytes(ref state, data, offset, 4);
                    writer.AdvanceAndReset(4);
                    return;
                case WireType.Fixed64:
                    if (length != 8) throw new ArgumentException(nameof(length));
                    writer.ImplWriteBytes(ref state, data, offset, 8);
                    writer.AdvanceAndReset(8);
                    return;
                case WireType.String:
                    writer.AdvanceAndReset(writer.ImplWriteVarint64(ref state, (uint)length) + length);
                    if (length == 0) return;
                    writer.ImplWriteBytes(ref state, data, offset, length);
                    break;
                default:
                    ThrowException(writer);
                    break;
            }
        }


        /// <summary>
        /// Writes a byte-array to the stream; supported wire-types: String
        /// </summary>
        public static void WriteBytes(System.Buffers.ReadOnlySequence<byte> data, ProtoWriter writer, ref State state)
        {
            int length = checked((int)data.Length);
            switch (writer.WireType)
            {
                case WireType.Fixed32:
                    if (length != 4) throw new ArgumentException(nameof(length));
                    writer.ImplWriteBytes(ref state, data);
                    writer.AdvanceAndReset(4);
                    return;
                case WireType.Fixed64:
                    if (length != 8) throw new ArgumentException(nameof(length));
                    writer.ImplWriteBytes(ref state, data);
                    writer.AdvanceAndReset(8);
                    return;
                case WireType.String:
                    writer.AdvanceAndReset(writer.ImplWriteVarint64(ref state, (uint)length) + length);
                    if (length == 0) return;
                    writer.ImplWriteBytes(ref state, data);
                    break;
                default:
                    ThrowException(writer);
                    break;
            }
        }
        int _depth = 0;

        private void PostSubItem(ref State state)
        {
            if (WireType != WireType.None) state.ThrowInvalidSerializationOperation();
            if (depth <= 0) state.ThrowInvalidSerializationOperation();
            if (depth-- > RecursionCheckDepth)
            {
                PopRecursionStack();
            }
            packedFieldNumber = 0; // ending the sub-item always wipes packed encoding
        }
        const int RecursionCheckDepth = 25;

        /// <summary>
        /// Indicates the start of a nested record of specified type when fieldNumber has been written.
        /// </summary>
        /// <param name="instance">The instance to write.</param>
        /// <param name="prefixLength">See <see cref="string"/> (for true) and <see cref="WireType.StartGroup"/> (for false)</param>
        /// <param name="writer">The destination.</param>
        /// <returns>A token representing the state of the stream; this token is given to EndSubItem.</returns>
        public static SubItemToken StartSubItem(object instance, bool prefixLength, ProtoWriter writer, ref State state)
        {
            if (writer._expectRoot)
            {
                writer._expectRoot = false;
                return new SubItemToken(int.MinValue);
            }
            // ignored field does affect only field header
            // but subitem information is considered content
            // it should be started properly
            WriteFieldHeaderCompleteAnyType(prefixLength ? WireType.String : WireType.StartGroup, writer, ref state);
            return StartSubItem(instance, writer, ref state);
        }

        /// <summary>
        /// Indicates the start of a nested record of specified type when fieldNumber *AND* wireType has been written.
        /// </summary>
        /// <param name="instance">The instance to write.</param>
        /// <param name="writer">The destination.</param>
        /// <returns>A token representing the state of the stream; this token is given to EndSubItem.</returns>
        public static SubItemToken StartSubItemWithoutWritingHeader(object instance, ProtoWriter writer, ref State state)
        {
            // "ignored" is not checked here because field header is already fully written
            return StartSubItem(instance, writer, ref state);
        }
        
        /// <summary>
        /// Indicates the start of a nested record of specified type when fieldNumber has not been written before.
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <param name="instance">The instance to write.</param>
        /// <param name="prefixLength">See <see cref="string"/> (for true) and <see cref="WireType.StartGroup"/> (for false)</param>
        /// <param name="writer">The destination.</param>
        /// <returns>A token representing the state of the stream; this token is given to EndSubItem.</returns>
        public static SubItemToken StartSubItem(int fieldNumber, object instance, bool prefixLength, ProtoWriter writer, ref State state)
        {
            // "ignored" is not checked here because field header is being written from scratch
            WriteFieldHeaderAnyType(fieldNumber, prefixLength ? WireType.String : WireType.StartGroup, writer, ref state);

            return StartSubItem(instance, writer, ref state);
        }

        protected private ProtoWriter()
            => netCache = new NetObjectCache();
        internal WriteState ResetWriteState()
        {
            var state = new WriteState(_position64, fieldNumber, WireType);
            _position64 = 0;
            fieldNumber = 0;
            WireType = WireType.None;
            return state;
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct WriteState
        {
            internal WriteState(long position, int fieldNumber, WireType wireType)
            {
                Position = position;
                FieldNumber = fieldNumber;
                WireType = wireType;
            }
            internal readonly long Position;
            internal readonly WireType WireType;
            internal readonly int FieldNumber;
        }
        internal void SetWriteState(WriteState state)
        {
            _position64 = state.Position;
            fieldNumber = state.FieldNumber;
            WireType = state.WireType;
        }

        /// <summary>
        /// Creates a new writer against a stream
        /// </summary>
        /// <param name="model">The model to use for serialization; this can be null, but this will impair the ability to serialize sub-objects</param>
        /// <param name="userState">Additional context about this serialization operation</param>
        /// <param name="impactCount">Whether this initialization should impact usage counters (to check for double-usage)</param>
        internal virtual void Init(TypeModel model, object userState, bool impactCount)
        {
            OnInit(impactCount);
            _position64 = 0;
            _needFlush = false;
            this.packedFieldNumber = 0;
            depth = 0;
            fieldNumber = 0;
            this.model = model;
            WireType = WireType.None;
            if (userState is SerializationContext context) context.Freeze();
            UserState = userState;
        }

        MutableList _recursionStack;

        /// <summary>
        /// Addition information about this serialization operation.
        /// </summary>
        public object UserState { get; private set; }

        #if DEBUG || TRACK_USAGE
                int _usageCount;
                partial void OnDispose()
                {
                    int count = System.Threading.Interlocked.Decrement(ref _usageCount);
                    if (count != 0) ThrowHelper.ThrowInvalidOperationException($"Usage count - expected 0, was {count}");
                }
                partial void OnInit(bool impactCount)
                {
                    if (impactCount)
                    {
                        int count = System.Threading.Interlocked.Increment(ref _usageCount);
                        if (count != 1) ThrowHelper.ThrowInvalidOperationException($"Usage count - expected 1, was {count}");
                    }
                    else
                    {
                        _usageCount = 1;
                    }
                }
        #endif

                partial void OnDispose();
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
        partial void OnInit(bool impactCount);


        /// <summary>
        /// Writes a sub-item to the input writer
        /// </summary>
        protected internal virtual void WriteMessage<[DynamicallyAccessedMembers(DynamicAccess.ContractType)] T>(ref State state, T value, ISerializer<T> serializer, PrefixStyle style, bool recursionCheck)
        {
#pragma warning disable CS0618 // StartSubItem/EndSubItem
            var tok = state.StartSubItem(TypeHelper<T>.IsReferenceType & recursionCheck ? (object)value : null, style);
            (serializer ?? TypeModel.GetSerializer<T>(model)).Write(ref state, value);
            state.EndSubItem(tok, style);
#pragma warning restore CS0618
        }

        /// <summary>
        /// Writes a sub-item to the input writer
        /// </summary>
        protected internal virtual void WriteSubType<[DynamicallyAccessedMembers(DynamicAccess.ContractType)] T>(ref State state, T value, ISubTypeSerializer<T> serializer) where T : class
        {
#pragma warning disable CS0618 // StartSubItem/EndSubItem
            var tok = state.StartSubItem(null, PrefixStyle.Base128);
            serializer.WriteSubType(ref state, value);
            state.EndSubItem(tok, PrefixStyle.Base128);
#pragma warning restore CS0618
        }
        private void PopRecursionStack() { _recursionStack.RemoveLast(); }

        protected private virtual void ClearKnownObjects()
            => netCache?.Clear();

        public static bool CheckIsOnHalfToRecursionDepthLimit(ProtoWriter writer)
        {
            return writer._depth > (writer._model?.RecursionDepthLimit ?? TypeModel.DefaultRecursionDepthLimit) / 2;
        }

        /// <summary>
        /// Indicates the start of a nested record.
        /// </summary>
        /// <param name="instance">The instance to write.</param>
        /// <param name="writer">The destination.</param>
        /// <param name="state">Writer state</param>
        /// <returns>A token representing the state of the stream; this token is given to EndSubItem.</returns>
        public static SubItemToken StartSubItem(object instance, ProtoWriter writer, ref State state)
            => writer.StartSubItem(ref state, instance, PrefixStyle.Base128);

        private SubItemToken StartSubItem(ref State state, object instance, PrefixStyle style)
        {
            if (++_depth > RecursionCheckDepth)
            {
                CheckRecursionStackAndPush(instance);
                if (_depth > (_model?.RecursionDepthLimit ?? TypeModel.DefaultRecursionDepthLimit)) TypeModel.ThrowRecursionDepthLimitExceeded(_recursionStack);
            }
            _expectRoot = false;
            switch (WireType)
            {
                case WireType.StartGroup:
                    WireType = WireType.None;
                    return new SubItemToken((long)(-_fieldNumber));
                case WireType.Fixed32:
                    switch (style)
                    {
                        case PrefixStyle.Fixed32:
                        case PrefixStyle.Fixed32BigEndian:
                            break; // OK
                        default:
                            throw CreateException(this);
                    }
                    goto case WireType.String;
                case WireType.String:
#if DEBUG
                    if (_model != null && _model.ForwardsOnly)
                    {
                        throw new ProtoException("Should not be buffering data: " + instance ?? "(null)");
                    }
#endif
                    return ImplStartLengthPrefixedSubItem(ref state, instance, style);
                default:
                    throw CreateException(this);
            }
        }

#pragma warning disable RCS1163, IDE0060 // Remove unused parameter
        internal long GetPosition(ref State state) => _position64;

        /// <summary>
        /// Throws an exception indicating that the given enum cannot be mapped to a serialized value.
        /// </summary>
        [MethodImpl(HotPath)]
        public static void ThrowEnumException(ProtoWriter writer, object enumValue)
            => writer.DefaultState().ThrowEnumException(enumValue);

        /// <summary>
        /// Indicates the end of a nested record.
        /// </summary>
        /// <param name="token">The token obtained from StartubItem.</param>
        /// <param name="writer">The destination.</param>
        [MethodImpl(HotPath)]
        [Obsolete(PreferWriteMessage, false)]
        public static void EndSubItem(SubItemToken token, ProtoWriter writer)
            => writer.DefaultState().EndSubItem(token, PrefixStyle.Base128);

        private void EndSubItem(ref State state, SubItemToken token, PrefixStyle style)
        {
            if (WireType != WireType.None) { throw CreateException(this); }
            if (_fieldStarted) { throw CreateException(this); }
            if (token.Value64 == int.MinValue)
            {
                WireType = WireType.None;
                return;
            }
            if (WireType != WireType.None) { throw CreateException(this); }
            long value = token.Value64;
            if (_depth <= 0) throw CreateException(this);
            if (_depth-- > RecursionCheckDepth)
            {
                PopRecursionStack();
            }
            if (value < 0)

            {   // group - very simple append
                var cancel = token.SeekOnEndOrMakeNullField;
                if (cancel?.PositionShouldBeEqualTo == _position64)
                {
                    if (cancel.Value.ThenTrySeekToPosition != null && TrySeek(cancel.Value.ThenTrySeekToPosition.Value, ref state))
                    {
                        WireType = WireType.None;
                        return;
                    }

                    if (cancel?.NullFieldNumber is int field)
                    {
                        WireType = WireType.None;
                        WriteFieldHeader(field, WireType.Null, this, ref state);
                        return;
                    }
                }
                WriteHeaderCore(-(int)value, WireType.EndGroup, this, ref state);
                WireType = WireType.None;
                return;
            }
            ImplEndLengthPrefixedSubItem(ref state, token, style);
        }

        internal int Depth => depth;

        protected internal virtual bool TrySeek(long position, ref ProtoWriter.State state)
        {
            return false;
        }

        /// <summary>
        /// Creates a new writer against a stream
        /// </summary>
        /// <param name="model">The model to use for serialization; this can be null, but this will impair the ability to serialize sub-objects</param>
        /// <param name="context">Additional context about this serialization operation</param>
        private protected ProtoWriter(TypeModel model, SerializationContext context)
        {
            this._model = model;
            WireType = WireType.None;
            if (context == null) { context = SerializationContext.Default; }
            else { context.Freeze(); }
            Context = context;
        }

        /// <summary>
        /// Addition information about this serialization operation.
        /// </summary>
        [Obsolete("Prefer " + nameof(UserState))]
        public SerializationContext Context => SerializationContext.AsSerializationContext(this);

        private protected virtual void Cleanup()
        {
            if (_depth == 0 && _needFlush && ImplDemandFlushOnDispose)
            {
                throw new InvalidOperationException("Writer was diposed without being flushed; data may be lost - you should ensure that Flush (or Abandon) is called");
            }
            _model = null;
            _lateReferences.Reset();
        }

        #pragma warning disable CA1816 // Dispose methods should call SuppressFinalize - no intention of supporting finalizers here
                void IDisposable.Dispose() => Dispose();

        internal void Abandon()
        {
            if (_depth != 0 || _flushLock != 0 || _fieldStarted) throw new InvalidOperationException("The writer is in an incomplete state");
        }

        private bool _needFlush;
        private long _position64;
        // note that this is used by some of the unit tests and should not be removed
        public static long GetLongPosition(ProtoWriter writer, ref ProtoWriter.State state) { return writer._position64; }
        
        protected private void Advance(long count) => _position64 += count;
        internal void AdvanceAndReset(int count)
        {
            _position64 += count;
            WireType = WireType.None;
        }

        internal void CheckClear(ref State state)
        {
            if (_depth != 0 || !TryFlush(ref state)) throw new InvalidOperationException("The writer is in an incomplete state");
            _needFlush = false; // because we ^^^ *JUST DID*
        }

        /// <summary>
        /// The encoding used by the writer
        /// </summary>
        internal protected static readonly UTF8Encoding UTF8 = new UTF8Encoding();

        /// <summary>
        /// Get the TypeModel associated with this writer
        /// </summary>
        public TypeModel Model { get { return _model; } }

        static readonly UTF8Encoding Encoding = new UTF8Encoding();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint Zig(int value)
        {
            return (uint)((value << 1) ^ (value >> 31));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong Zig(long value)
        {
            return (ulong)((value << 1) ^ (value >> 63));
        }

        /// <summary>
        /// Writes a string to the stream; supported wire-types: String
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteString(string value, ProtoWriter writer) => writer.DefaultState().WriteString(value);
        /// <summary>
        /// Writes a string to the stream; supported wire-types: String
        /// </summary>
        public static void WriteString(string value, ProtoWriter writer, ref State state)
        {
            switch (writer.WireType)
            {
                case WireType.String:
                    if (string.IsNullOrEmpty(value))
                    {
                        writer.AdvanceAndReset(writer.ImplWriteVarint64(ref state, 0));
                    }
                    else
                    {
                        var len = UTF8.GetByteCount(value);
                        writer.AdvanceAndReset(writer.ImplWriteVarint64(ref state, (uint)len) + len);
                        writer.ImplWriteString(ref state, value, len);
                    }
                    break;
                default:
                    ThrowException(writer);
                    break;
            }
        }

        protected private abstract void ImplWriteString(ref State state, string value, int expectedBytes);
        protected private abstract int ImplWriteVarint32(ref State state, uint value);
        internal abstract int ImplWriteVarint64(ref State state, ulong value);
        protected private abstract void ImplWriteFixed32(ref State state, uint value);
        protected private abstract void ImplWriteFixed64(ref State state, ulong value);
        protected private abstract void ImplWriteBytes(ref State state, ReadOnlyMemory<byte> data);
        protected private abstract void ImplWriteBytes(ref State state, ReadOnlySequence<byte> data);
        protected private abstract void ImplCopyRawFromStream(ref State state, Stream source);
        private protected abstract SubItemToken ImplStartLengthPrefixedSubItem(ref State state, object instance, PrefixStyle style);
        protected private abstract void ImplEndLengthPrefixedSubItem(ref State state, SubItemToken token, PrefixStyle style);
        protected private abstract bool ImplDemandFlushOnDispose { get; }

        /// <summary>
        /// Writes any buffered data (if possible) to the underlying stream.
        /// </summary>
        /// <param name="state">Writer state</param>
        /// <remarks>It is not always possible to fully flush, since some sequences
        /// may require values to be back-filled into the byte-stream.</remarks>
        private protected abstract bool TryFlush(ref State state);

        /// <summary>
        /// Writes an unsigned 64-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteUInt64(ulong value, ProtoWriter writer)
            => writer.DefaultState().WriteUInt64(value);

        /// <summary>
        /// Writes a signed 64-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteInt64(long value, ProtoWriter writer)
            => writer.DefaultState().WriteInt64(value);


        /// <summary>
        /// Used for packed encoding; indicates that the next field should be skipped rather than
        /// a field header written. Note that the field number must match, else an exception is thrown
        /// when the attempt is made to write the (incorrect) field. The wire-type is taken from the
        /// subsequent call to WriteFieldHeader. Only primitive types can be packed.
        /// </summary>
        [MethodImpl(HotPath)]
        public static void SetPackedField(int fieldNumber, ProtoWriter writer)
            => writer.DefaultState().SetPackedField(fieldNumber);

        /// <summary>
        /// Used for packed encoding; explicitly reset the packed field marker; this is not required
        /// if using StartSubItem/EndSubItem
        /// </summary>
        [MethodImpl(HotPath)]
        public static void ClearPackedField(int fieldNumber, ProtoWriter writer)
            => writer.DefaultState().ClearPackedField(fieldNumber);

        /// <summary>
        /// Writes an unsigned 16-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteUInt32(uint value, ProtoWriter writer)
            => writer.DefaultState().WriteUInt32(value);

        /// <summary>
        /// Writes a signed 16-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteInt16(short value, ProtoWriter writer)
            => writer.DefaultState().WriteInt16(value);

        /// <summary>
        /// Writes an unsigned 16-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteUInt16(ushort value, ProtoWriter writer)
            => writer.DefaultState().WriteUInt16(value);

        /// <summary>
        /// Writes an unsigned 8-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteByte(byte value, ProtoWriter writer)
            => writer.DefaultState().WriteByte(value);

        /// <summary>
        /// Writes a signed 8-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteSByte(sbyte value, ProtoWriter writer)
            => writer.DefaultState().WriteSByte(value);

        internal static long Measure<T>(NullProtoWriter writer, T value, ISerializer<T> serializer)
        {
            long length;
            object obj = default;
            if (TypeHelper<T>.IsReferenceType)
            {
                obj = value;
                if (obj is null) return 0;
                if (writer.netCache.TryGetKnownLength(obj, null, out length))
                    return length;
            }

            // do the actual work
            var oldState = writer.ResetWriteState();
            var nulState = new State(writer);
            serializer.Write(ref nulState, value);
            length = nulState.GetPosition();
            writer.SetWriteState(oldState); // make sure we leave it how we found it

            // cache it if we can
            if (TypeHelper<T>.IsReferenceType)
            {   // we know it isn't null; we'd have exited above
                writer.netCache.SetKnownLength(obj, null, length);
            }
            return length;
        }

        internal static long Measure<T>(NullProtoWriter writer, T value, ISubTypeSerializer<T> serializer) where T : class
        {
            object obj = value;
            if (obj is null) return 0;
            if (writer.netCache.TryGetKnownLength(obj, typeof(T), out var length))
            {
                return length;
            }

            var oldState = writer.ResetWriteState();
            var nulState = new State(writer);
            serializer.WriteSubType(ref nulState, value);
            length = nulState.GetPosition();
            writer.SetWriteState(oldState); // make sure we leave it how we found it
            writer.netCache.SetKnownLength(obj, typeof(T), length);
            return length;
        }

        /// <summary>
        /// Writes a signed 32-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public static void WriteInt32(int value, ProtoWriter writer)
            => writer.DefaultState().WriteInt32(value);

        /// <summary>
        /// Writes a double-precision number to the stream; supported wire-types: Fixed32, Fixed64
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteDouble(double value, ProtoWriter writer)
            => writer.DefaultState().WriteDouble(value);

        /// <summary>
        /// Writes a single-precision number to the stream; supported wire-types: Fixed32, Fixed64
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteSingle(float value, ProtoWriter writer)
            => writer.DefaultState().WriteSingle(value);
        
        // general purpose serialization exception message
        internal static Exception CreateException(ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            string field = writer._fieldStarted
                ? ", waiting for wire type of field number " + writer._fieldNumber
                : (writer.WireType != WireType.None ? (", field number " + writer._fieldNumber) : "");
            return new ProtoException("Invalid serialization operation with wire-type " + writer.WireType.ToString() + field + ", position " + writer._position64.ToString());
        }

        /// <summary>
        /// Writes a boolean to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        [MethodImpl(HotPath)]
        public static void WriteBoolean(bool value, ProtoWriter writer)
            => writer.DefaultState().WriteBoolean(value);

        /// <summary>
        /// Copies any extension data stored for the instance to the underlying stream
        /// </summary>
        [MethodImpl(HotPath)]
        public static void AppendExtensionData(IExtensible instance, ProtoWriter writer)
            => writer.DefaultState().AppendExtensionData(instance);

        /// <summary>
        /// Copies any extension data stored for the instance to the underlying stream
        /// </summary>
        public static void AppendExtensionData(IExtensible instance, ProtoWriter writer, ref State state)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            // we expect the writer to be raw here; the extension data will have the
            // header detail, so we'll copy it implicitly
            if (writer.WireType != WireType.None || writer._fieldStarted) throw CreateException(writer);

            IExtension extn = instance.GetExtensionObject(false);
            if (extn != null)
            {
                // unusually we *don't* want "using" here; the "finally" does that, with
                // the extension object being responsible for disposal etc
                Stream source = extn.BeginQuery();
                try
                {
                    if (ProtoReader.TryConsumeSegmentRespectingPosition(source, out var data, ProtoReader.TO_EOF))
                    {
                        writer.ImplWriteBytes(ref state, data.Array, data.Offset, data.Count);
                        writer.Advance(data.Count);
                    }
                    else
                    {
                        writer.ImplCopyRawFromStream(ref state, source);
                    }
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
            switch (wireType)
            {
                // use long in case very large arrays are enabled
                case WireType.Fixed32: bytes = ((ulong)elementCount) << 2; break; // x4
                case WireType.Fixed64: bytes = ((ulong)elementCount) << 3; break; // x8
                default:
                    throw new ArgumentOutOfRangeException(nameof(wireType), "Invalid wire-type: " + wireType);
            }

            return bytes;
        }

        public void WriteLengthPrefix(ref ProtoWriter.State state, ulong length)
        {
            if (WireType != WireType.String) throw new InvalidOperationException($"Expected wireType {WireType.String} but was {WireType}");
            WireType = WireType.Variant;
            int prefixLength = ImplWriteVarint64(ref state, length);
            AdvanceAndReset(prefixLength);
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
        internal static void WriteArrayContent<T>(T[] arr, WireType wt, Action<T, ProtoWriter> writer, ProtoWriter dest, ref State state)
        {
            // TODO update
            var t = StartSubItem(null, true, dest, ref state);
            WriteFieldHeader(ListHelpers.FieldLength, WireType.Variant, dest, ref state);
            WriteInt32(arr.Length, dest);
            for (int i = 0; i < arr.Length; i++)
            {
                if (i == 0)
                    WriteFieldHeader(ListHelpers.FieldItem, wt, dest, ref state);
                else
                    WriteFieldHeaderIgnored(wt, dest, ref state);

                writer(arr[i], dest);
            }
            EndSubItem(t, dest);
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

        internal string SerializeType(System.Type type)
        {
            return TypeModel.SerializeType(_model, type);
        }

        #if FEAT_DYNAMIC_REF
                /// <summary>
                /// Specifies a known root object to use during reference-tracked serialization
                /// </summary>
                [MethodImpl(HotPath)]
                public void SetRootObject(object value) => netCache.SetKeyedObject(NetObjectCache.Root, value);

                /// <summary>
                /// Specifies a known root object to use during reference-tracked serialization
                /// </summary>
                [MethodImpl(HotPath)]
                internal int AddObjectKey(object value, out bool existing)
                {
                    AssertTrackedObjects();
                    return netCache.AddObjectKey(value, out existing);
                }

                [MethodImpl(HotPath)]
                internal void AssertTrackedObjects()
                {
                    if (!(this is StreamProtoWriter)) ThrowHelper.ThrowTrackedObjects(this);
                }
        #endif

                /// <summary>
                /// Writes a Type to the stream, using the model's DynamicTypeFormatting if appropriate; supported wire-types: String
                /// </summary>
                [MethodImpl(HotPath)]
                public static void WriteType(Type value, ProtoWriter writer)
                    => writer.DefaultState().WriteType(value);
    }
}
