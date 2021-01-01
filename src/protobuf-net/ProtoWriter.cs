// Modified by Vladyslav Taranov for AqlaSerializer, 2016
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
    /// <para>Represents an output stream for writing protobuf data.</para>
    /// <para>
    /// Why is the API backwards (static methods with writer arguments)?
    /// See: http://marcgravell.blogspot.com/2010/03/last-will-be-first-and-first-will-be.html
    /// </para>
    /// </summary>
    public abstract partial class ProtoWriter : IDisposable
    {
        internal const string UseStateAPI = ProtoReader.UseStateAPI;
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
        [Obsolete(ProtoWriter.UseStateAPI, false)]
        public static void WriteObject(object value, int key, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteObject(value, key, writer, ref state);
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
        /// <summary>
        /// Write an encapsulated sub-object, using the supplied unique key (reprasenting a type) - but the
        /// caller is asserting that this relationship is non-recursive; no recursion check will be
        /// performed.
        /// </summary>
        /// <param name="value">The object to write.</param>
        /// <param name="key">The key that uniquely identifies the type within the model.</param>
        /// <param name="writer">The destination.</param>
        [Obsolete(UseStateAPI, false)]
        public static void WriteRecursionSafeObject(object value, int key, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteRecursionSafeObject(value, key, writer, ref state);
        }
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
        [Obsolete(UseStateAPI, false)]
        public static void WriteFieldHeader(int fieldNumber, WireType wireType, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteFieldHeader(fieldNumber, wireType, writer, ref state);
        }

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

        /// <summary>
        /// Writes a byte-array to the stream; supported wire-types: String
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteBytes(byte[] data, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteBytes(data, writer, ref state);
        }
        
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
            int bytes = writer.ImplWriteVarint64(ref state, header);
            writer.Advance(bytes);
        }

        /// <summary>
        /// Writes a byte-array to the stream; supported wire-types: String
        /// </summary>
        public static void WriteBytes(byte[] data, ProtoWriter writer, ref State state)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            WriteBytes(data, 0, data.Length, writer, ref state);
        }
        /// <summary>
        /// Writes a byte-array to the stream; supported wire-types: String
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteBytes(byte[] data, int offset, int length, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteBytes(data, offset, length, writer, ref state);
        }

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

        /// <summary>
        /// Indicates the end of a nested record.
        /// </summary>
        /// <param name="token">The token obtained from StartubItem.</param>
        /// <param name="writer">The destination.</param>
        /// <param name="state">Writer state</param>
        public static void EndSubItem(SubItemToken token, ProtoWriter writer, ref State state)
            => writer.EndSubItem(ref state, token, PrefixStyle.Base128);

        /// <summary>
        /// Indicates the end of a nested record.
        /// </summary>
        /// <param name="token">The token obtained from StartubItem.</param>
        /// <param name="writer">The destination.</param>
        [Obsolete(UseStateAPI, false)]
        public static void EndSubItem(SubItemToken token, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            writer.EndSubItem(ref state, token, PrefixStyle.Base128);
        }

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
        public SerializationContext Context { get; }

        private protected virtual void Dispose()
        {
            if (_depth == 0 && _needFlush && ImplDemandFlushOnDispose)
            {
                throw new InvalidOperationException("Writer was diposed without being flushed; data may be lost - you should ensure that Flush (or Abandon) is called");
            }
            _model = null;
            _lateReferences.Reset();
        }

        void IDisposable.Dispose()
        {
            Dispose();
        }

        internal void Abandon()
        {
            if (_depth != 0 || _flushLock != 0 || _fieldStarted) throw new InvalidOperationException("The writer is in an incomplete state");
        }

        private bool _needFlush;
        private long _position64;
        // note that this is used by some of the unit tests and should not be removed
        public static long GetLongPosition(ProtoWriter writer, ref ProtoWriter.State state) { return writer._position64; }
        
        protected private void Advance(long count) => _position64 += count;

        protected private void AdvanceAndReset(int count)
        {
            _position64 += count;
            WireType = WireType.None;
        }

        /// <summary>
        /// Flushes data to the underlying stream, and releases any resources. The underlying stream is *not* disposed
        /// by this operation.
        /// </summary>
        public void Close(ref State state)
        {
            CheckClear(ref state);
            Dispose();
        }

        internal void CheckClear(ref State state)
        {
            if (_depth != 0 || !TryFlush(ref state)) throw new InvalidOperationException("The writer is in an incomplete state");
            _needFlush = false; // because we ^^^ *JUST DID*
        }

        private protected static readonly UTF8Encoding UTF8 = new UTF8Encoding();

        /// <summary>
        /// Get the TypeModel associated with this writer
        /// </summary>
        public TypeModel Model { get { return _model; } }

        static readonly UTF8Encoding Encoding = new UTF8Encoding();

        private static uint Zig(int value)
        {
            return (uint)((value << 1) ^ (value >> 31));
        }

        private static ulong Zig(long value)
        {
            return (ulong)((value << 1) ^ (value >> 63));
        }

        /// <summary>
        /// Writes a string to the stream; supported wire-types: String
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteString(string value, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteString(value, writer, ref state);
        }
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
        protected private abstract int ImplWriteVarint64(ref State state, ulong value);
        protected private abstract void ImplWriteFixed32(ref State state, uint value);
        protected private abstract void ImplWriteFixed64(ref State state, ulong value);
        protected private abstract void ImplWriteBytes(ref State state, byte[] data, int offset, int length);
        protected private abstract void ImplWriteBytes(ref State state, System.Buffers.ReadOnlySequence<byte> data);
        protected private abstract void ImplCopyRawFromStream(ref State state, Stream source);
        private protected abstract SubItemToken ImplStartLengthPrefixedSubItem(ref State state, object instance, PrefixStyle style);
        protected private abstract void ImplEndLengthPrefixedSubItem(ref State state, SubItemToken token, PrefixStyle style);
        protected private abstract bool ImplDemandFlushOnDispose { get; }


        /// <summary>
        /// Writes any uncommitted data to the output
        /// </summary>
        public void Flush(ref State state)
        {
            if (TryFlush(ref state))
            {
                _needFlush = false;
            }
        }

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
        [Obsolete(UseStateAPI, false)]
        public static void WriteUInt64(ulong value, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteUInt64(value, writer, ref state);
        }
        /// <summary>
        /// Writes an unsigned 64-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public static void WriteUInt64(ulong value, ProtoWriter writer, ref State state)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            switch (writer.WireType)
            {
                case WireType.Fixed64:
                    writer.ImplWriteFixed64(ref state, value);
                    writer.AdvanceAndReset(8);
                    return;
                case WireType.Variant:
                    int bytes = writer.ImplWriteVarint64(ref state, value);
                    writer.AdvanceAndReset(bytes);
                    return;
                case WireType.Fixed32:
                    writer.ImplWriteFixed32(ref state, checked((uint)value));
                    writer.AdvanceAndReset(4);
                    return;
                default:
                    ThrowException(writer);
                    break;
            }
        }

        /// <summary>
        /// Writes a signed 64-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteInt64(long value, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteInt64(value, writer, ref state);
        }
        /// <summary>
        /// Writes a signed 64-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public static void WriteInt64(long value, ProtoWriter writer, ref State state)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            switch (writer.WireType)
            {
                case WireType.Fixed64:
                    writer.ImplWriteFixed64(ref state, (ulong)value);
                    writer.AdvanceAndReset(8);
                    return;
                case WireType.Variant:
                    writer.AdvanceAndReset(writer.ImplWriteVarint64(ref state, (ulong)value));
                    return;
                case WireType.SignedVariant:
                    writer.AdvanceAndReset(writer.ImplWriteVarint64(ref state, Zig(value)));
                    return;
                case WireType.Fixed32:
                    writer.ImplWriteFixed32(ref state, checked((uint)(int)value));
                    writer.AdvanceAndReset(4);
                    return;
                default:
                    ThrowException(writer);
                    break;
            }
        }

        /// <summary>
        /// Writes an unsigned 16-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteUInt32(uint value, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteUInt32(value, writer, ref state);
        }

        /// <summary>
        /// Writes an unsigned 16-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public static void WriteUInt32(uint value, ProtoWriter writer, ref State state)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            switch (writer.WireType)
            {
                case WireType.Fixed32:
                    writer.ImplWriteFixed32(ref state, value);
                    writer.AdvanceAndReset(4);
                    return;
                case WireType.Fixed64:
                    writer.ImplWriteFixed64(ref state, value);
                    writer.AdvanceAndReset(8);
                    return;
                case WireType.Variant:
                    int bytes = writer.ImplWriteVarint32(ref state, value);
                    writer.AdvanceAndReset(bytes);
                    return;
                default:
                    ThrowException(writer);
                    break;
            }
        }

        /// <summary>
        /// Writes a signed 16-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteInt16(short value, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteInt32(value, writer, ref state);
        }

        /// <summary>
        /// Writes a signed 16-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public static void WriteInt16(short value, ProtoWriter writer, ref State state)
            => WriteInt32(value, writer, ref state);

        /// <summary>
        /// Writes an unsigned 16-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteUInt16(ushort value, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteUInt32(value, writer, ref state);
        }

        /// <summary>
        /// Writes an unsigned 16-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public static void WriteUInt16(ushort value, ProtoWriter writer, ref State state)
            => WriteUInt32(value, writer, ref state);

        /// <summary>
        /// Writes an unsigned 8-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteByte(byte value, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteUInt32(value, writer, ref state);
        }

        /// <summary>
        /// Writes an unsigned 8-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public static void WriteByte(byte value, ProtoWriter writer, ref State state)
            => WriteUInt32(value, writer, ref state);

        /// <summary>
        /// Writes a signed 8-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteSByte(sbyte value, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteInt32(value, writer, ref state);
        }
        /// <summary>
        /// Writes a signed 8-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public static void WriteSByte(sbyte value, ProtoWriter writer, ref State state)
            => WriteInt32(value, writer, ref state);

        /// <summary>
        /// Writes a signed 32-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteInt32(int value, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteInt32(value, writer, ref state);
        }
        /// <summary>
        /// Writes a signed 32-bit integer to the stream; supported wire-types: Variant, Fixed32, Fixed64, SignedVariant
        /// </summary>
        public static void WriteInt32(int value, ProtoWriter writer, ref State state)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            switch (writer.WireType)
            {
                case WireType.Fixed32:
                    writer.ImplWriteFixed32(ref state, (uint)value);
                    writer.AdvanceAndReset(4);
                    return;
                case WireType.Fixed64:
                    writer.ImplWriteFixed64(ref state, (ulong)(long)value);
                    writer.AdvanceAndReset(8);
                    return;
                case WireType.Variant:
                    if (value >= 0)
                    {
                        writer.AdvanceAndReset(writer.ImplWriteVarint32(ref state, (uint)value));
                    }
                    else
                    {
                        writer.AdvanceAndReset(writer.ImplWriteVarint64(ref state, (ulong)(long)value));
                    }
                    return;
                case WireType.SignedVariant:
                    writer.AdvanceAndReset(writer.ImplWriteVarint32(ref state, Zig(value)));
                    return;
                default:
                    ThrowException(writer);
                    break;
            }
        }

        /// <summary>
        /// Writes a double-precision number to the stream; supported wire-types: Fixed32, Fixed64
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteDouble(double value, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteDouble(value, writer, ref state);
        }

        /// <summary>
        /// Writes a double-precision number to the stream; supported wire-types: Fixed32, Fixed64
        /// </summary>
        public static void WriteDouble(double value, ProtoWriter writer, ref State state)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            switch (writer.WireType)
            {
                case WireType.Fixed32:
                    float f = (float)value;
                    if (float.IsInfinity(f) && !double.IsInfinity(value))
                    {
                        throw new OverflowException();
                    }
                    WriteSingle(f, writer, ref state);
                    return;
                case WireType.Fixed64:
#if FEAT_SAFE
                    writer.ImplWriteFixed64(ref state, (ulong)BitConverter.DoubleToInt64Bits(value));
#else
                    unsafe { writer.ImplWriteFixed64(ref state, *(ulong*)&value); }
#endif
                    writer.AdvanceAndReset(8);
                    return;
                default:
                    ThrowException(writer);
                    return;
            }
        }
        /// <summary>
        /// Writes a single-precision number to the stream; supported wire-types: Fixed32, Fixed64
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteSingle(float value, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteSingle(value, writer, ref state);
        }

        /// <summary>
        /// Writes a single-precision number to the stream; supported wire-types: Fixed32, Fixed64
        /// </summary>
        public static void WriteSingle(float value, ProtoWriter writer, ref State state)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            switch (writer.WireType)
            {
                case WireType.Fixed32:
#if FEAT_SAFE
                    writer.ImplWriteFixed32(ref state, BitConverter.ToUInt32(BitConverter.GetBytes(value), 0));
#else
                    unsafe { writer.ImplWriteFixed32(ref state, *(uint*)&value); }
#endif                    
                    writer.AdvanceAndReset(4);
                    return;
                case WireType.Fixed64:
                    WriteDouble(value, writer, ref state);
                    return;
                default:
                    ThrowException(writer);
                    break;
            }
        }
        
        // general purpose serialization exception message
        internal static Exception CreateException(ProtoWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            string field = writer._fieldStarted
                ? ", waiting for wire type of field number " + writer._fieldNumber
                : (writer.WireType != WireType.None ? (", field number " + writer._fieldNumber) : "");
            return new ProtoException("Invalid serialization operation with wire-type " + writer.WireType.ToString() + field + ", position " + writer._position64.ToString());
        }


        internal static void ThrowException(ProtoWriter writer)
            => throw CreateException(writer);

        /// <summary>
        /// Writes a boolean to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteBoolean(bool value, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteBoolean(value, writer, ref state);
        }

        /// <summary>
        /// Writes a boolean to the stream; supported wire-types: Variant, Fixed32, Fixed64
        /// </summary>
        public static void WriteBoolean(bool value, ProtoWriter writer, ref State state)
        {
            ProtoWriter.WriteUInt32(value ? (uint)1 : (uint)0, writer, ref state);
        }


        /// <summary>
        /// Copies any extension data stored for the instance to the underlying stream
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void AppendExtensionData(IExtensible instance, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            AppendExtensionData(instance, writer, ref state);
        }

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

        /// <summary>
        /// Writes a Type to the stream, using the model's DynamicTypeFormatting if appropriate; supported wire-types: String
        /// </summary>
        [Obsolete(UseStateAPI, false)]
        public static void WriteType(Type value, ProtoWriter writer)
        {
            State state = writer.DefaultState();
            WriteType(value, writer, ref state);
        }
        /// <summary>
        /// Writes a Type to the stream, using the model's DynamicTypeFormatting if appropriate; supported wire-types: String
        /// </summary>
        public static void WriteType(Type value, ProtoWriter writer, ref State state)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            WriteString(writer.SerializeType(value), writer, ref state);
        }
    }
}
