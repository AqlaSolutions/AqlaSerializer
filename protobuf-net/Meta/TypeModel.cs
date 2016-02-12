// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.IO;

using System.Collections;
using AqlaSerializer.Internal;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

namespace AqlaSerializer.Meta
{
    /// <summary>
    /// Provides protobuf serialization support for a number of types
    /// </summary>
    public abstract partial class TypeModel
    {
        internal ExtensibleUtil ExtensibleUtil { get; }
#if WINRT
        internal TypeInfo MapType(TypeInfo type)
        {
            return type;
        }
#endif

        /// <summary>
        /// When you pass stream which CanSeek and CanRead the serializer may use it as a buffer when its own buffer grows too big. Default: true.
        /// </summary>
        public bool AllowStreamRewriting { get; set; } = true;

        internal const int DefaultRecursionDepthLimit = 500;

        public int RecursionDepthLimit { get; set; } = DefaultRecursionDepthLimit;

        internal static void ThrowRecursionDepthLimitExceeded()
        {
            throw new ProtoException("Recursion depth exceeded safe limit. See TypeModel.RecursionDepthLimit");
        }

        protected TypeModel()
        {
            AllowStreamRewriting = true;
            ExtensibleUtil = new ExtensibleUtil(this);
        }

        /// <summary>
        /// Should the <c>Kind</c> be included on date/time values?
        /// </summary>
        protected internal virtual bool SerializeDateTimeKind() { return false; }

        /// <summary>
        /// Resolve a System.Type to the compiler-specific type
        /// </summary>
        protected internal Type MapType(System.Type type)
        {
            return MapType(type, true);
        }
        /// <summary>
        /// Resolve a System.Type to the compiler-specific type
        /// </summary>
        protected internal virtual Type MapType(System.Type type, bool demand)
        {
#if FEAT_IKVM
            throw new NotImplementedException(); // this should come from RuntimeTypeModel!
#else
            return type;
#endif
        }
        
        internal WireType GetWireType(ProtoTypeCode code, BinaryDataFormat format, ref Type type, out int modelKey)
        {
            modelKey = -1;
            if (Helpers.IsEnum(type))
            {
                modelKey = GetKey(ref type);
                return WireType.Variant;
            }

            WireType wireType = HelpersInternal.GetWireType(code, format);
            if (wireType != WireType.None) return wireType;

            if ((modelKey = GetKey(ref type)) >= 0)
            {
                return WireType.String;
            }
            return WireType.None;
        }
        
#if !FEAT_IKVM
        /// <summary>
        /// This is the more "complete" version of Serialize, which handles single instances of mapped types.
        /// The value is written as a complete field, including field-header and (for sub-objects) a
        /// length-prefix
        /// In addition to that, this provides support for:
        ///  - basic values; individual int / string / Guid / etc
        ///  - IEnumerable sequences of any type handled by TrySerializeAuxiliaryType
        ///  
        /// </summary>
        internal bool TrySerializeAuxiliaryType(ProtoWriter writer, Type type, BinaryDataFormat format, int tag, object value, bool isInsideList, bool isRoot)
        {
            if (type == null) { type = value.GetType(); }

            ProtoTypeCode typecode = Helpers.GetTypeCode(type);
            int modelKey;
            // note the "ref type" here normalizes against proxies
            WireType wireType = GetWireType(typecode, format, ref type, out modelKey);


            if (modelKey >= 0)
            {   // write the header, but defer to the model
                if (Helpers.IsEnum(type))
                { // no header
                    if (isRoot)
                        ProtoWriter.WriteFieldHeaderBegin(EnumRootTag, writer);
                    Serialize(modelKey, value, writer, false);
                    return true;
                }
                else
                {
                    ProtoWriter.WriteFieldHeaderBegin(tag, writer);
                    switch (wireType)
                    {
                        case WireType.None:
                            throw ProtoWriter.CreateException(writer);
                        case WireType.StartGroup:
                        case WireType.String:
                            // needs a wrapping length etc
                            SubItemToken token = ProtoWriter.StartSubItem(value, wireType == WireType.String, writer);
                            Serialize(modelKey, value, writer, isRoot);
                            ProtoWriter.EndSubItem(token, writer);
                            return true;
                        default:
                            Serialize(modelKey, value, writer, isRoot);
                            return true;
                    }
                }
            }

            if(wireType != WireType.None) {
                ProtoWriter.WriteFieldHeader(tag, wireType, writer);
            }
            if (ProtoWriter.TryWriteBuiltinTypeValue(value, typecode, false, writer)) return true;

            if (typecode == ProtoTypeCode.Type && isRoot)
            {
                ProtoWriter.WriteType((Type)value, writer);
                return true;
            }

            // by now, we should have covered all the simple cases; if we wrote a field-header, we have
            // forgotten something!
            Helpers.DebugAssert(wireType == WireType.None);

            // now attempt to handle sequences (including arrays and lists)
            IEnumerable sequence = value as IEnumerable;
            if (sequence != null)
            {
                if (isInsideList) throw new ProtoException("TrySerializeAuxiliaryType should not be called for nested lists, instead they should be registered in model");
                foreach (object item in sequence) {
                    if (item == null) { throw new NullReferenceException(); }
                    if (!TrySerializeAuxiliaryType(writer, null, format, tag, item, true, isRoot))
                    {
                        ThrowUnexpectedType(item.GetType());
                    }
                }
                return true;
            }
            return false;
        }

        private void SerializeCore(ProtoWriter writer, object value, bool isRoot)
        {
            if (value == null) throw new ArgumentNullException("value");
            Type type = value.GetType();
            int key = GetKey(ref type);
            if (key >= 0)
            {
                Serialize(key, value, writer, isRoot);
            }
            else if (!TrySerializeAuxiliaryType(writer, type, BinaryDataFormat.Default, Serializer.ListItemTag, value, false, isRoot))
            {
                ThrowUnexpectedType(type);
            }
        }
#endif
        /// <summary>
        /// Writes a protocol-buffer representation of the given instance to the supplied stream.
        /// </summary>
        /// <param name="value">The existing instance to be serialized (cannot be null).</param>
        /// <param name="dest">The destination stream to write to.</param>
        public void Serialize(Stream dest, object value)
        {
            Serialize(dest, value, null);
        }
        /// <summary>
        /// Writes a protocol-buffer representation of the given instance to the supplied stream.
        /// </summary>
        /// <param name="value">The existing instance to be serialized (cannot be null).</param>
        /// <param name="dest">The destination stream to write to.</param>
        /// <param name="context">Additional information about this serialization operation.</param>
        public void Serialize(Stream dest, object value, SerializationContext context)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            using (ProtoWriter writer = new ProtoWriter(dest, this, context))
            {
                writer.SetRootObject(value);
                SerializeCore(writer, value, true);
                writer.Close();
            }
#endif
        }
        /// <summary>
        /// Writes a protocol-buffer representation of the given instance to the supplied writer.
        /// </summary>
        /// <param name="value">The existing instance to be serialized (cannot be null).</param>
        /// <param name="dest">The destination writer to write to.</param>
        public void Serialize(ProtoWriter dest, object value)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (dest == null) throw new ArgumentNullException("dest");
            dest.CheckDepthFlushlock();
            dest.SetRootObject(value);
            SerializeCore(dest, value, true);
            dest.CheckDepthFlushlock();
            ProtoWriter.Flush(dest);
#endif
        }

        /// <summary>
        /// Applies a protocol-buffer stream to an existing instance (or null), using length-prefixed
        /// data - useful with network IO.
        /// </summary>
        /// <param name="type">The type being merged.</param>
        /// <param name="value">The existing instance to be modified (can be null).</param>
        /// <param name="source">The binary stream to apply to the instance (cannot be null).</param>
        /// <param name="style">How to encode the length prefix.</param>
        /// <param name="fieldNumber">The tag used as a prefix to each record (only used with base-128 style prefixes).</param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        public T DeserializeWithLengthPrefix<T>(Stream source, T value, PrefixStyle style, int fieldNumber)
        {
            return (T)DeserializeWithLengthPrefix(source, value, MapType(typeof(T)), style, fieldNumber);
        }

        /// <summary>
        /// Applies a protocol-buffer stream to an existing instance (or null), using length-prefixed
        /// data - useful with network IO.
        /// </summary>
        /// <param name="type">The type being merged.</param>
        /// <param name="value">The existing instance to be modified (can be null).</param>
        /// <param name="source">The binary stream to apply to the instance (cannot be null).</param>
        /// <param name="style">How to encode the length prefix.</param>
        /// <param name="fieldNumber">The tag used as a prefix to each record (only used with base-128 style prefixes).</param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        public object DeserializeWithLengthPrefix(Stream source, object value, Type type, PrefixStyle style, int fieldNumber)
        {
            int bytesRead;
            return DeserializeWithLengthPrefix(source, value, type, style, fieldNumber, null, out bytesRead);
        }


        /// <summary>
        /// Applies a protocol-buffer stream to an existing instance (or null), using length-prefixed
        /// data - useful with network IO.
        /// </summary>
        /// <param name="type">The type being merged.</param>
        /// <param name="value">The existing instance to be modified (can be null).</param>
        /// <param name="source">The binary stream to apply to the instance (cannot be null).</param>
        /// <param name="style">How to encode the length prefix.</param>
        /// <param name="expectedField">The tag used as a prefix to each record (only used with base-128 style prefixes).</param>
        /// <param name="resolver">Used to resolve types on a per-field basis.</param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        public object DeserializeWithLengthPrefix(Stream source, object value, Type type, PrefixStyle style, int expectedField, Serializer.TypeResolver resolver)
        {
            int bytesRead;
            return DeserializeWithLengthPrefix(source, value, type, style, expectedField, resolver, out bytesRead);
        }

        /// <summary>
        /// Applies a protocol-buffer stream to an existing instance (or null), using length-prefixed
        /// data - useful with network IO.
        /// </summary>
        /// <param name="type">The type being merged.</param>
        /// <param name="value">The existing instance to be modified (can be null).</param>
        /// <param name="source">The binary stream to apply to the instance (cannot be null).</param>
        /// <param name="style">How to encode the length prefix.</param>
        /// <param name="expectedField">The tag used as a prefix to each record (only used with base-128 style prefixes).</param>
        /// <param name="resolver">Used to resolve types on a per-field basis.</param>
        /// <param name="bytesRead">Returns the number of bytes consumed by this operation (includes length-prefix overheads and any skipped data).</param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        public object DeserializeWithLengthPrefix(Stream source, object value, Type type, PrefixStyle style, int expectedField, Serializer.TypeResolver resolver, out int bytesRead)
        {
            bool haveObject;
            return DeserializeWithLengthPrefix(source, value, type, style, expectedField, resolver, out bytesRead, out haveObject, null);
        }

        private object DeserializeWithLengthPrefix(Stream source, object value, Type type, PrefixStyle style, int expectedField, Serializer.TypeResolver resolver, out int bytesRead, out bool haveObject, SerializationContext context)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            haveObject = false;
            bool skip;
            int len;
            int tmpBytesRead;
            bytesRead = 0;
            if (type == null && (style != PrefixStyle.Base128 || resolver == null))
            {
                throw new InvalidOperationException("A type must be provided unless base-128 prefixing is being used in combination with a resolver");
            }
            int actualField;
            do
            {

                bool expectPrefix = expectedField > 0 || resolver != null;
                len = ProtoReader.ReadLengthPrefix(source, expectPrefix, style, out actualField, out tmpBytesRead);
                if (tmpBytesRead == 0) return value;
                bytesRead += tmpBytesRead;
                if (len < 0) return value;

                switch (style)
                {
                    case PrefixStyle.Base128:
                        if (expectPrefix && expectedField == 0 && type == null && resolver != null)
                        {
                            type = resolver(actualField);
                            skip = type == null;
                        }
                        else { skip = expectedField != actualField; }
                        break;
                    default:
                        skip = false;
                        break;
                }

                if (skip)
                {
                    if (len == int.MaxValue) throw new InvalidOperationException();
                    ProtoReader.Seek(source, len, null);
                    bytesRead += len;
                }
            } while (skip);

            ProtoReader reader = null;
            try
            {
                reader = ProtoReader.Create(source, this, context, len);
                int key = GetKey(ref type);
                if (key >= 0)// && !Helpers.IsEnum(type))
                {
                    value = Deserialize(key, value, reader, true);
                }
                else
                {
                    if (!(TryDeserializeAuxiliaryType(reader, BinaryDataFormat.Default, Serializer.ListItemTag, type, ref value, true, false, true, false, true) || len == 0))
                    {
                        TypeModel.ThrowUnexpectedType(type); // throws
                    }
                }
                bytesRead += reader.Position;
                haveObject = true;
                return value;
            }
            finally
            {
                ProtoReader.Recycle(reader);
            }
#endif
        }
        /// <summary>
        /// Reads a sequence of consecutive length-prefixed items from a stream, using
        /// either base-128 or fixed-length prefixes. Base-128 prefixes with a tag
        /// are directly comparable to serializing multiple items in succession
        /// (use the <see cref="Serializer.ListItemTag"/> tag to emulate the implicit behavior
        /// when serializing a list/array). When a tag is
        /// specified, any records with different tags are silently omitted. The
        /// tag is ignored. The tag is ignores for fixed-length prefixes.
        /// </summary>
        /// <param name="source">The binary stream containing the serialized records.</param>
        /// <param name="style">The prefix style used in the data.</param>
        /// <param name="expectedField">The tag of records to return (if non-positive, then no tag is
        /// expected and all records are returned).</param>
        /// <param name="resolver">On a field-by-field basis, the type of object to deserialize (can be null if "type" is specified). </param>
        /// <param name="type">The type of object to deserialize (can be null if "resolver" is specified).</param>
        /// <returns>The sequence of deserialized objects.</returns>
        public System.Collections.IEnumerable DeserializeItems(System.IO.Stream source, Type type, PrefixStyle style, int expectedField, Serializer.TypeResolver resolver)
        {
            return DeserializeItems(source, type, style, expectedField, resolver, null);
        }
        /// <summary>
        /// Reads a sequence of consecutive length-prefixed items from a stream, using
        /// either base-128 or fixed-length prefixes. Base-128 prefixes with a tag
        /// are directly comparable to serializing multiple items in succession
        /// (use the <see cref="Serializer.ListItemTag"/> tag to emulate the implicit behavior
        /// when serializing a list/array). When a tag is
        /// specified, any records with different tags are silently omitted. The
        /// tag is ignored. The tag is ignores for fixed-length prefixes.
        /// </summary>
        /// <param name="source">The binary stream containing the serialized records.</param>
        /// <param name="style">The prefix style used in the data.</param>
        /// <param name="expectedField">The tag of records to return (if non-positive, then no tag is
        /// expected and all records are returned).</param>
        /// <param name="resolver">On a field-by-field basis, the type of object to deserialize (can be null if "type" is specified). </param>
        /// <param name="type">The type of object to deserialize (can be null if "resolver" is specified).</param>
        /// <returns>The sequence of deserialized objects.</returns>
        /// <param name="context">Additional information about this serialization operation.</param>
        public System.Collections.IEnumerable DeserializeItems(System.IO.Stream source, Type type, PrefixStyle style, int expectedField, Serializer.TypeResolver resolver, SerializationContext context)
        {
            return new DeserializeItemsIterator(this, source, type, style, expectedField, resolver, context);
        }

#if !NO_GENERICS
        /// <summary>
        /// Reads a sequence of consecutive length-prefixed items from a stream, using
        /// either base-128 or fixed-length prefixes. Base-128 prefixes with a tag
        /// are directly comparable to serializing multiple items in succession
        /// (use the <see cref="Serializer.ListItemTag"/> tag to emulate the implicit behavior
        /// when serializing a list/array). When a tag is
        /// specified, any records with different tags are silently omitted. The
        /// tag is ignored. The tag is ignores for fixed-length prefixes.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize.</typeparam>
        /// <param name="source">The binary stream containing the serialized records.</param>
        /// <param name="style">The prefix style used in the data.</param>
        /// <param name="expectedField">The tag of records to return (if non-positive, then no tag is
        /// expected and all records are returned).</param>
        /// <returns>The sequence of deserialized objects.</returns>
        public System.Collections.Generic.IEnumerable<T> DeserializeItems<T>(Stream source, PrefixStyle style, int expectedField)
        {
            return DeserializeItems<T>(source, style, expectedField, null);
        }
        /// <summary>
        /// Reads a sequence of consecutive length-prefixed items from a stream, using
        /// either base-128 or fixed-length prefixes. Base-128 prefixes with a tag
        /// are directly comparable to serializing multiple items in succession
        /// (use the <see cref="Serializer.ListItemTag"/> tag to emulate the implicit behavior
        /// when serializing a list/array). When a tag is
        /// specified, any records with different tags are silently omitted. The
        /// tag is ignored. The tag is ignores for fixed-length prefixes.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize.</typeparam>
        /// <param name="source">The binary stream containing the serialized records.</param>
        /// <param name="style">The prefix style used in the data.</param>
        /// <param name="expectedField">The tag of records to return (if non-positive, then no tag is
        /// expected and all records are returned).</param>
        /// <returns>The sequence of deserialized objects.</returns>
        /// <param name="context">Additional information about this serialization operation.</param>
        public System.Collections.Generic.IEnumerable<T> DeserializeItems<T>(Stream source, PrefixStyle style, int expectedField, SerializationContext context)
        {
            return new DeserializeItemsIterator<T>(this, source, style, expectedField, context);
        }

        private sealed class DeserializeItemsIterator<T> : DeserializeItemsIterator,
            System.Collections.Generic.IEnumerator<T>,
            System.Collections.Generic.IEnumerable<T>
        {
            System.Collections.Generic.IEnumerator<T> System.Collections.Generic.IEnumerable<T>.GetEnumerator() { return this; }
            public new T Current { get { return (T)base.Current; } }
            void IDisposable.Dispose() { }
            public DeserializeItemsIterator(TypeModel model, Stream source, PrefixStyle style, int expectedField, SerializationContext context)
                : base(model, source, model.MapType(typeof(T)), style, expectedField, null, context) { }
        }
#endif
        private class DeserializeItemsIterator : IEnumerator, IEnumerable
        {
            IEnumerator IEnumerable.GetEnumerator() { return this; }
            private bool haveObject;
            private object current;
            public bool MoveNext()
            {
                if (haveObject)
                {
                    int bytesRead;
                    current = model.DeserializeWithLengthPrefix(source, null, type, style, expectedField, resolver, out bytesRead, out haveObject, context);
                }
                return haveObject;
            }
            void IEnumerator.Reset() { throw new NotSupportedException(); }
            public object Current { get { return current; } }
            private readonly Stream source;
            private readonly Type type;
            private readonly PrefixStyle style;
            private readonly int expectedField;
            private readonly Serializer.TypeResolver resolver;
            private readonly TypeModel model;
            private readonly SerializationContext context;
            public DeserializeItemsIterator(TypeModel model, Stream source, Type type, PrefixStyle style, int expectedField, Serializer.TypeResolver resolver, SerializationContext context)
            {
                haveObject = true;
                this.source = source;
                this.type = type;
                this.style = style;
                this.expectedField = expectedField;
                this.resolver = resolver;
                this.model = model;
                this.context = context;
            }
        }

        /// <summary>
        /// Writes a protocol-buffer representation of the given instance to the supplied stream,
        /// with a length-prefix. This is useful for socket programming,
        /// as DeserializeWithLengthPrefix can be used to read the single object back
        /// from an ongoing stream.
        /// </summary>
        /// <param name="type">The type being serialized.</param>
        /// <param name="value">The existing instance to be serialized (cannot be null).</param>
        /// <param name="style">How to encode the length prefix.</param>
        /// <param name="dest">The destination stream to write to.</param>
        /// <param name="fieldNumber">The tag used as a prefix to each record (only used with base-128 style prefixes).</param>
        public void SerializeWithLengthPrefix<T>(Stream dest, T value, PrefixStyle style, int fieldNumber)
        {
            SerializeWithLengthPrefix(dest, value, MapType(typeof(T)), style, fieldNumber);
        }

        /// <summary>
        /// Writes a protocol-buffer representation of the given instance to the supplied stream,
        /// with a length-prefix. This is useful for socket programming,
        /// as DeserializeWithLengthPrefix can be used to read the single object back
        /// from an ongoing stream.
        /// </summary>
        /// <param name="type">The type being serialized.</param>
        /// <param name="value">The existing instance to be serialized (cannot be null).</param>
        /// <param name="style">How to encode the length prefix.</param>
        /// <param name="dest">The destination stream to write to.</param>
        /// <param name="fieldNumber">The tag used as a prefix to each record (only used with base-128 style prefixes).</param>
        public void SerializeWithLengthPrefix(Stream dest, object value, Type type, PrefixStyle style, int fieldNumber)
        {
            SerializeWithLengthPrefix(dest, value, type, style, fieldNumber, null);
        }
        /// <summary>
        /// Writes a protocol-buffer representation of the given instance to the supplied stream,
        /// with a length-prefix. This is useful for socket programming,
        /// as DeserializeWithLengthPrefix can be used to read the single object back
        /// from an ongoing stream.
        /// </summary>
        /// <param name="type">The type being serialized.</param>
        /// <param name="value">The existing instance to be serialized (cannot be null).</param>
        /// <param name="style">How to encode the length prefix.</param>
        /// <param name="dest">The destination stream to write to.</param>
        /// <param name="fieldNumber">The tag used as a prefix to each record (only used with base-128 style prefixes).</param>
        /// <param name="context">Additional information about this serialization operation.</param>
        public void SerializeWithLengthPrefix(Stream dest, object value, Type type, PrefixStyle style, int fieldNumber, SerializationContext context)
        {
            if (type == null)
            {
                if(value == null) throw new ArgumentNullException("value");
                type = MapType(value.GetType());
            }
            int key = GetKey(ref type);
            using (ProtoWriter writer = new ProtoWriter(dest, this, context))
            {
                switch (style)
                {
                    case PrefixStyle.None:
                        Serialize(key, value, writer, true);
                        break;
                    case PrefixStyle.Base128:
                    case PrefixStyle.Fixed32:
                    case PrefixStyle.Fixed32BigEndian:
                        ProtoWriter.WriteObject(value, key, writer, style, fieldNumber, true);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("style");
                }
                writer.Close();
            }
        }

#if !NO_GENERICS && !IOS
        /// <summary>
        /// Applies a protocol-buffer stream to an existing instance (which may be null).
        /// </summary>
        /// <param name="source">The binary stream to apply to the instance (cannot be null).</param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        public T Deserialize<T>(Stream source)
        {
            return (T)Deserialize(source, null, typeof(T));
        }

        /// <summary>
        /// Applies a protocol-buffer stream to an existing instance (which may be null).
        /// </summary>
        /// <param name="value">The existing instance to be modified (can be null).</param>
        /// <param name="source">The binary stream to apply to the instance (cannot be null).</param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        public T Deserialize<T>(Stream source, T value)
        {
            return (T)Deserialize(source, value, typeof(T));
        }
#endif
        /// <summary>
        /// Applies a protocol-buffer stream to an existing instance (which may be null).
        /// </summary>
        /// <param name="type">The type (including inheritance) to consider.</param>
        /// <param name="value">The existing instance to be modified (can be null).</param>
        /// <param name="source">The binary stream to apply to the instance (cannot be null).</param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        public object Deserialize(Stream source, object value, System.Type type)
        {
            return Deserialize(source, value, type, null);
        }
        /// <summary>
        /// Applies a protocol-buffer stream to an existing instance (which may be null).
        /// </summary>
        /// <param name="type">The type (including inheritance) to consider.</param>
        /// <param name="value">The existing instance to be modified (can be null).</param>
        /// <param name="source">The binary stream to apply to the instance (cannot be null).</param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        /// <param name="context">Additional information about this serialization operation.</param>
        public object Deserialize(Stream source, object value, System.Type type, SerializationContext context)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            bool autoCreate = PrepareDeserialize(value, ref type);
            ProtoReader reader = null;
            try
            {
                reader = ProtoReader.Create(source, this, context, ProtoReader.TO_EOF);
                if (value != null) reader.SetRootObject(value);
                object obj = DeserializeCore(reader, type, value, autoCreate, true);
                reader.CheckFullyConsumed();
                return obj;
            }
            finally
            {
                ProtoReader.Recycle(reader);
            }
#endif
        }

        private bool PrepareDeserialize(object value, ref Type type)
        {
            if (type == null)
            {
                if (value == null)
                {
                    throw new ArgumentNullException("type");
                }
                else
                {
                    type = MapType(value.GetType());
                }
            }
            bool autoCreate = true;
#if !NO_GENERICS
            Type underlyingType = Helpers.GetNullableUnderlyingType(type);
            if (underlyingType != null)
            {
                type = underlyingType;
                autoCreate = false;
            }
#endif
            return autoCreate;
        }

        /// <summary>
        /// Applies a protocol-buffer stream to an existing instance (which may be null).
        /// </summary>
        /// <param name="type">The type (including inheritance) to consider.</param>
        /// <param name="value">The existing instance to be modified (can be null).</param>
        /// <param name="source">The binary stream to apply to the instance (cannot be null).</param>
        /// <param name="length">The number of bytes to consume.</param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        public object Deserialize(Stream source, object value, System.Type type, int length)
        {
            return Deserialize(source, value, type, length, null);
        }
        /// <summary>
        /// Applies a protocol-buffer stream to an existing instance (which may be null).
        /// </summary>
        /// <param name="type">The type (including inheritance) to consider.</param>
        /// <param name="value">The existing instance to be modified (can be null).</param>
        /// <param name="source">The binary stream to apply to the instance (cannot be null).</param>
        /// <param name="length">The number of bytes to consume (or -1 to read to the end of the stream).</param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        /// <param name="context">Additional information about this serialization operation.</param>
        public object Deserialize(Stream source, object value, System.Type type, int length, SerializationContext context)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            bool autoCreate = PrepareDeserialize(value, ref type);
            ProtoReader reader = null;
            try
            {
                reader = ProtoReader.Create(source, this, context, length);
                if (value != null) reader.SetRootObject(value);
                object obj = DeserializeCore(reader, type, value, autoCreate, true);
                reader.CheckFullyConsumed();
                return obj;
            }
            finally
            {
                ProtoReader.Recycle(reader);
            }
#endif
        }
        /// <summary>
        /// Applies a protocol-buffer reader to an existing instance (which may be null).
        /// </summary>
        /// <param name="type">The type (including inheritance) to consider.</param>
        /// <param name="value">The existing instance to be modified (can be null).</param>
        /// <param name="source">The reader to apply to the instance (cannot be null).</param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        public object Deserialize(ProtoReader source, object value, System.Type type)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (source == null) throw new ArgumentNullException("source");
            bool autoCreate = PrepareDeserialize(value, ref type);
            if (value != null) source.SetRootObject(value);
            object obj = DeserializeCore(source, type, value, autoCreate, true);
            source.CheckFullyConsumed();
            return obj;
#endif
        }

        internal const int EnumRootTag = 1;
#if !FEAT_IKVM
        private object DeserializeCore(ProtoReader reader, Type type, object value, bool noAutoCreate, bool isRoot)
        {
            int key = GetKey(ref type);
            if (key >= 0 && !Helpers.IsEnum(type))
            {
                return Deserialize(key, value, reader, isRoot);
            }
            // this returns true to say we actively found something, but a value is assigned either way (or throws)
            TryDeserializeAuxiliaryType(reader, BinaryDataFormat.Default, Serializer.ListItemTag, type, ref value, true, false, noAutoCreate, false, isRoot);
            return value;
        }
        
        /// <summary>
        /// This is the more "complete" version of Deserialize, which handles single instances of mapped types.
        /// The value is read as a complete field, including field-header and (for sub-objects) a
        /// length-prefix..kmc  
        /// 
        /// In addition to that, this provides support for:
        ///  - basic values; individual int / string / Guid / etc
        ///  - IList sets of any type handled by TryDeserializeAuxiliaryType
        /// </summary>
        internal bool TryDeserializeAuxiliaryType(ProtoReader reader, BinaryDataFormat format, int tag, Type type, ref object value, bool skipOtherFields, bool asListItem, bool autoCreate, bool insideList, bool isRoot)
        {
            if (type == null) throw new ArgumentNullException("type");

            Type itemType = null;
            ProtoTypeCode typecode = Helpers.GetTypeCode(type);
            int modelKey;
            WireType wiretype = GetWireType(typecode, format, ref type, out modelKey);

            bool found = false;
            if (wiretype == WireType.None)
            {
                itemType = GetListItemType(this, type);
                if (itemType == null && type.IsArray && type.GetArrayRank() == 1 && type != typeof(byte[]))
                {
                    itemType = type.GetElementType();
                }
                if (itemType != null)
                {
                    if (insideList) throw new ProtoException("TryDeserializeAuxiliaryType should not be called for nested lists, instead they should be registered in model");
                    found = TryDeserializeList(this, reader, format, tag, type, itemType, isRoot, ref value);
                    if (!found && autoCreate)
                    {
                        value = CreateListInstance(type, itemType);
                        if (value != null)
                            ProtoReader.NoteObject(value, reader);
                    }
                    return found;
                }

                // otherwise, not a happy bunny...
                ThrowUnexpectedType(type);
            }

            if (Helpers.IsEnum(type))
            {
                if (isRoot && reader.ReadFieldHeader() != EnumRootTag) return false;
                value = Deserialize(modelKey, value, reader, false);
                return true;
            }

            // to treat correctly, should read all values

            while (true)
            {
                // for convenience (re complex exit conditions), additional exit test here:
                // if we've got the value, are only looking for one, and we aren't a list - then exit
                if (found && asListItem) break;


                // read the next item
                int fieldNumber = reader.ReadFieldHeader();
                if (fieldNumber <= 0) break;
                if (fieldNumber != tag)
                {
                    if (skipOtherFields)
                    {
                        reader.SkipField();
                        continue;
                    }
                    throw ProtoReader.AddErrorData(new InvalidOperationException(
                        "Expected field " + tag.ToString() + ", but found " + fieldNumber.ToString()), reader);
                }
                found = true;
                reader.Hint(wiretype); // handle signed data etc

                if (modelKey >= 0)
                {
                    switch (wiretype)
                    {
                        case WireType.String:
                        case WireType.StartGroup:
                            SubItemToken token = ProtoReader.StartSubItem(reader);
                            value = Deserialize(modelKey, value, reader, isRoot);
                            ProtoReader.EndSubItem(token, reader);
                            continue;
                        default:
                            value = Deserialize(modelKey, value, reader, isRoot);
                            continue;
                    }
                }
                if (reader.TryReadBuiltinType(ref value, typecode, false)) continue;
                if (typecode == ProtoTypeCode.Type && isRoot)
                {
                    value = reader.ReadType();
                }

            }
            if (!found && !asListItem && autoCreate)
            {
                if (type != typeof(string))
                {
                    value = Activator.CreateInstance(type);
                }
            }
            return found;
        }
#endif

#if !NO_RUNTIME
        /// <summary>
        /// Creates a new runtime model, to which the caller
        /// can add support for a range of types. A model
        /// can be used "as is", or can be compiled for
        /// optimal performance.
        /// </summary>
        public static RuntimeTypeModel Create()
        {
            return Create(false, ProtoCompatibilitySettings.Default);
        }

        /// <summary>
        /// Creates a new runtime model, to which the caller
        /// can add support for a range of types. A model
        /// can be used "as is", or can be compiled for
        /// optimal performance.
        /// </summary>
        /// <param name="newestBehavior">If set to true the newest recommended defaults are enabled upon creation. Default: false</param>
        /// <param name="protoCompatibility">Protocol Buffers format compatibility</param>
        public static RuntimeTypeModel Create(bool newestBehavior, ProtoCompatibilitySettings protoCompatibility)
        {
            var r = new RuntimeTypeModel(false, protoCompatibility);
            if (newestBehavior)
            {
                r.AlwaysUseTypeRegistrationForCollections = true;
                r.UseImplicitZeroDefaults = false;
            }
            return r;
        }
#endif

        /// <summary>
        /// Applies common proxy scenarios, resolving the actual type to consider
        /// </summary>
        protected internal static Type ResolveProxies(Type type)
        {
            if (type == null) return null;
#if !NO_GENERICS
            if (type.IsGenericParameter) return null;
            // Nullable<T>
            Type tmp = Helpers.GetNullableUnderlyingType(type);
            if (tmp != null) return tmp;
#endif

#if !(WINRT || CF)
            // EF POCO
            string fullName = type.FullName;
            if (fullName != null && fullName.StartsWith("System.Data.Entity.DynamicProxies.")) return type.BaseType;

            // NHibernate
            Type[] interfaces = type.GetInterfaces();
            for(int i = 0 ; i < interfaces.Length ; i++)
            {
                switch(interfaces[i].FullName)
                {
                    case "NHibernate.Proxy.INHibernateProxy":
                    case "NHibernate.Proxy.DynamicProxy.IProxy":
                    case "NHibernate.Intercept.IFieldInterceptorAccessor":
                        return type.BaseType;
                }
            }
#endif
            return null;
        }
        /// <summary>
        /// Indicates whether the supplied type is explicitly modelled by the model
        /// </summary>
        public bool IsDefined(Type type)
        {
            return GetKey(ref type) >= 0;
        }
        /// <summary>
        /// Provides the key that represents a given type in the current model.
        /// The type is also normalized for proxies at the same time.
        /// </summary>
        protected internal int GetKey(ref Type type)
        {
            if (type == null) return -1;
            switch (Helpers.GetTypeCode(type))
            {
                case ProtoTypeCode.Uri:
                case ProtoTypeCode.String:
                case ProtoTypeCode.Type:
                    return -1;
            }
            int key = GetKeyImpl(type);
            if (key < 0)
            {
                Type normalized = ResolveProxies(type);
                if (normalized != null) {
                    type = normalized; // hence ref
                    key = GetKeyImpl(type);
                }
            }
            return key;
        }

        /// <summary>
        /// Provides the key that represents a given type in the current model.
        /// </summary>
        protected abstract int GetKeyImpl(Type type);
        /// <summary>
        /// Writes a protocol-buffer representation of the given instance to the supplied stream.
        /// </summary>
        /// <param name="key">Represents the type (including inheritance) to consider.</param>
        /// <param name="value">The existing instance to be serialized (cannot be null).</param>
        /// <param name="dest">The destination stream to write to.</param>
        protected internal abstract void Serialize(int key, object value, ProtoWriter dest, bool isRoot);
        /// <summary>
        /// Applies a protocol-buffer stream to an existing instance (which may be null).
        /// </summary>
        /// <param name="key">Represents the type (including inheritance) to consider.</param>
        /// <param name="value">The existing instance to be modified (can be null).</param>
        /// <param name="source">The binary stream to apply to the instance (cannot be null).</param>
        /// <returns>The updated instance; this may be different to the instance argument if
        /// either the original instance was null, or the stream defines a known sub-type of the
        /// original instance.</returns>
        protected internal abstract object Deserialize(int key, object value, ProtoReader source, bool isRoot);

        //internal ProtoSerializer Create(IProtoSerializer head)
        //{
        //    return new RuntimeSerializer(head, this);
        //}
        //internal ProtoSerializer Compile

        /// <summary>
        /// Indicates the type of callback to be used
        /// </summary>
        protected internal enum CallbackType
        {
            /// <summary>
            /// Invoked before an object is serialized
            /// </summary>
            BeforeSerialize,
            /// <summary>
            /// Invoked after an object is serialized
            /// </summary>
            AfterSerialize,
            /// <summary>
            /// Invoked before an object is deserialized (or when a new instance is created)
            /// </summary>            
            BeforeDeserialize,
            /// <summary>
            /// Invoked after an object is deserialized
            /// </summary>
            AfterDeserialize
        }
        
        public bool ForceSerializationDuringClone { get; set; }

#if !NO_GENERICS && !IOS
        /// <summary>
        /// Create a deep clone of the supplied instance; any sub-items are also cloned.
        /// </summary>
        public T DeepClone<T>(T genericValue)
        {
            return (T)DeepClone(value: (object)genericValue);
        }
#endif
        
        /// <summary>
        /// Create a deep clone of the supplied instance; any sub-items are also cloned.
        /// </summary>
        public object DeepClone(object value)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (value == null) return null;

            Type type = value.GetType();
            
            if (ForceSerializationDuringClone)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    using (ProtoWriter writer = new ProtoWriter(ms, this, null))
                    {
                        Serialize(writer, value);
                        writer.Close();
                    }
                    ms.Position = 0;
                    ProtoReader reader = null;
                    try
                    {
                        reader = ProtoReader.Create(ms, this, null, ProtoReader.TO_EOF);
                        return Deserialize(reader, null, type);
                    }
                    finally
                    {
                        ProtoReader.Recycle(reader);
                    }
                }
            }
            int key = GetKey(ref type);

            if (key >= 0 && !Helpers.IsEnum(type))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    using(ProtoWriter writer = new ProtoWriter(ms, this, null))
                    {
                        writer.SetRootObject(value);
                        Serialize(key, value, writer, true);
                        writer.Close();
                    }
                    ms.Position = 0;
                    ProtoReader reader = null;
                    try
                    {
                        reader = ProtoReader.Create(ms, this, null, ProtoReader.TO_EOF);
                        return Deserialize(key, null, reader, true);
                    }
                    finally
                    {
                        ProtoReader.Recycle(reader);
                    }
                }
            }
            int modelKey;
            if (type == typeof(byte[])) {
                byte[] orig = (byte[])value, clone = new byte[orig.Length];
                Helpers.BlockCopy(orig, 0, clone, 0, orig.Length);
                return clone;
            }
            else if (GetWireType(Helpers.GetTypeCode(type), BinaryDataFormat.Default, ref type, out modelKey) != WireType.None && modelKey < 0)
            {   // immutable; just return the original value
                return value;
            }
            using (MemoryStream ms = new MemoryStream())
            {
                using (ProtoWriter writer = new ProtoWriter(ms, this, null))
                {
                    if (!TrySerializeAuxiliaryType(writer, type, BinaryDataFormat.Default, Serializer.ListItemTag, value, false, true)) ThrowUnexpectedType(type);
                    writer.Close();
                }
                ms.Position = 0;
                ProtoReader reader = null;
                try
                {
                    reader = ProtoReader.Create(ms, this, null, ProtoReader.TO_EOF);
                    value = null; // start from scratch!
                    TryDeserializeAuxiliaryType(reader, BinaryDataFormat.Default, Serializer.ListItemTag, type, ref value, true, false, true, false, true);
                    return value;
                }
                finally
                {
                    ProtoReader.Recycle(reader);
                }
            }
#endif

        }

        /// <summary>
        /// Indicates that while an inheritance tree exists, the exact type encountered was not
        /// specified in that hierarchy and cannot be processed.
        /// </summary>
        protected internal static void ThrowUnexpectedSubtype(Type expected, Type actual)
        {
            if (expected != TypeModel.ResolveProxies(actual))
            {
                throw new InvalidOperationException("Unexpected sub-type: " + actual.FullName);
            }
        }

        /// <summary>
        /// Indicates that the given type was not expected, and cannot be processed.
        /// </summary>
        protected internal static void ThrowUnexpectedType(Type type)
        {
            string fullName = type == null ? "(unknown)" : type.FullName;
#if !NO_GENERICS && !WINRT
            if (type != null)
            {
                Type baseType = type.BaseType;
                if (baseType != null && baseType.IsGenericType && baseType.GetGenericTypeDefinition().Name == "GeneratedMessage`2")
                {
                    throw new InvalidOperationException(
                        "Are you mixing protobuf-net and protobuf-csharp-port? See http://stackoverflow.com/q/11564914; type: " + fullName);
                }
            }
#endif
            throw new InvalidOperationException("Type is not expected, and no contract can be inferred: " + fullName);
        }
        internal static Exception CreateNestedListsNotSupported()
        {
            return new NotSupportedException("Nested or jagged lists and arrays are not supported");
        }
        /// <summary>
        /// Indicates that the given type cannot be constructed; it may still be possible to 
        /// deserialize into existing instances.
        /// </summary>
        public static void ThrowCannotCreateInstance(Type type)
        {
            throw new ProtoException("No parameterless constructor found for " + (type == null ? "(null)" : type.Name));
        }

        internal static string SerializeType(TypeModel model, System.Type type)
        {
            if (model != null)
            {
                TypeFormatEventHandler handler = model.DynamicTypeFormatting;
                if (handler != null)
                {
                    TypeFormatEventArgs args = new TypeFormatEventArgs(type);
                    handler(model, args);
                    if (!Helpers.IsNullOrEmpty(args.FormattedName)) return args.FormattedName;
                }
            }
            return type.AssemblyQualifiedName;
        }

        internal static System.Type DeserializeType(TypeModel model, string value)
        {

            if (model != null)
            {
                TypeFormatEventHandler handler = model.DynamicTypeFormatting;
                if (handler != null)
                {
                    TypeFormatEventArgs args = new TypeFormatEventArgs(value);
                    handler(model, args);
                    if (args.Type != null) return args.Type;
                }
            }
            return System.Type.GetType(value);
        }

        /// <summary>
        /// Returns true if the type supplied is either a recognised contract type,
        /// or a *list* of a recognised contract type. 
        /// </summary>
        /// <remarks>Note that primitives always return false, even though the engine
        /// will, if forced, try to serialize such</remarks>
        /// <returns>True if this type is recognised as a serializable entity, else false</returns>
        public bool CanSerializeContractType(Type type)
        {
            return CanSerialize(type, false, true, true);
        }

        /// <summary>
        /// Returns true if the type supplied is a basic type with inbuilt handling,
        /// a recognised contract type, or a *list* of a basic / contract type. 
        /// </summary>
        public bool CanSerialize(Type type)
        {
            return CanSerialize(type, true, true, true);
        }

        /// <summary>
        /// Returns true if the type supplied is a basic type with inbuilt handling,
        /// or a *list* of a basic type with inbuilt handling
        /// </summary>
        public bool CanSerializeBasicType(Type type)
        {
            return CanSerialize(type, true, false, true);
        }
        private bool CanSerialize(Type type, bool allowBasic, bool allowContract, bool allowLists)
        {
            if (type == null) throw new ArgumentNullException("type");
            Type tmp = Helpers.GetNullableUnderlyingType(type);
            if (tmp != null) type = tmp;

            // is it a basic type?
            ProtoTypeCode typeCode = Helpers.GetTypeCode(type);
            switch(typeCode)
            {
                case ProtoTypeCode.Empty:
                case ProtoTypeCode.Unknown:
                    break;
                default:
                    return allowBasic; // well-known basic type
            }


            // is it a list?
            if (allowLists)
            {
                Type itemType = null;
                if (type.IsArray)
                {   // note we don't need to exclude byte[], as that is handled by GetTypeCode already
                    if (type.GetArrayRank() == 1) itemType = type.GetElementType();
                }
                else
                {
                    itemType = GetListItemType(this, type);
                }
                if (itemType != null) return CanSerialize(itemType, allowBasic, allowContract, true);
            }
            if (allowContract && GetKey(ref type) >= 0)
            {
                return true; // known contract type
            }

            return false;
        }

        /// <summary>
        /// Suggest a .proto definition for the given type
        /// </summary>
        /// <param name="type">The type to generate a .proto definition for, or <c>null</c> to generate a .proto that represents the entire model</param>
        /// <returns>The .proto definition as a string</returns>
        public virtual string GetSchema(Type type)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Used to provide custom services for writing and parsing type names when using dynamic types. Both parsing and formatting
        /// are provided on a single API as it is essential that both are mapped identically at all times.
        /// </summary>
        public event TypeFormatEventHandler DynamicTypeFormatting;

#if PLAT_BINARYFORMATTER && !(WINRT || PHONE8)
        /// <summary>
        /// Creates a new IFormatter that uses protocol-buffer [de]serialization.
        /// </summary>
        /// <returns>A new IFormatter to be used during [de]serialization.</returns>
        /// <param name="type">The type of object to be [de]deserialized by the formatter.</param>
        public System.Runtime.Serialization.IFormatter CreateFormatter(Type type)
        {
            return new Formatter(this, type);
        }

        internal sealed class Formatter : System.Runtime.Serialization.IFormatter
        {
            private readonly TypeModel model;
            private readonly Type type;
            internal Formatter(TypeModel model, Type type)
            {
                if (model == null) throw new ArgumentNullException("model");
                if (type == null) throw new ArgumentNullException("type");
                this.model = model;
                this.type = type;
            }
            private System.Runtime.Serialization.SerializationBinder binder;
            public System.Runtime.Serialization.SerializationBinder Binder
            {
                get { return binder; }
                set { binder = value; }
            }

            private System.Runtime.Serialization.StreamingContext context;
            public System.Runtime.Serialization.StreamingContext Context
            {
                get { return context; }
                set { context = value; }
            }

            public object Deserialize(Stream source)
            {
#if FEAT_IKVM
                throw new NotSupportedException();
#else
                return model.Deserialize(source, null, type, -1, Context);
#endif
            }

            public void Serialize(Stream destination, object graph)
            {
                model.Serialize(destination, graph, Context);
            }

            private System.Runtime.Serialization.ISurrogateSelector surrogateSelector;
            public System.Runtime.Serialization.ISurrogateSelector SurrogateSelector
            {
                get { return surrogateSelector; }
                set { surrogateSelector = value; }
            }
        }
#endif

#if DEBUG // this is used by some unit tests only, to ensure no buffering when buffering is disabled
        private bool forwardsOnly;
        /// <summary>
        /// If true, buffering of nested objects is disabled
        /// </summary>
        public bool ForwardsOnly
        {
            get { return forwardsOnly; }
            set { forwardsOnly = value; }
        }
#endif

        internal virtual Type GetType(string fullName, Assembly context)
        {
#if FEAT_IKVM
            throw new NotImplementedException();
#else
            return ResolveKnownType(fullName, this, context);
#endif
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static Type ResolveKnownType(string name, TypeModel model, Assembly assembly)
        {
            if (Helpers.IsNullOrEmpty(name)) return null;
            try
            {
#if FEAT_IKVM
                // looks like a NullReferenceException, but this should call into RuntimeTypeModel's version
                Type type = model == null ? null : model.GetType(name, assembly);
#else
                Type type = Type.GetType(name);
#endif
                if (type != null) return type;
            }
            catch { }
            try
            {
                int i = name.IndexOf(',');
                string fullName = (i > 0 ? name.Substring(0, i) : name).Trim();
#if !(WINRT || FEAT_IKVM)
                if (assembly == null) assembly = Assembly.GetCallingAssembly();
#endif
                Type type = assembly == null ? null : assembly.GetType(fullName);
                if (type != null) return type;
            }
            catch { }
            return null;
        }
#if !IOS
        /// <summary>
        /// Serializes a given instance and deserializes it as a different type;
        /// this can be used to translate between wire-compatible objects (where
        /// two .NET types represent the same data), or to promote/demote a type
        /// through an inheritance hierarchy.
        /// </summary>
        /// <remarks>No assumption of compatibility is made between the types.</remarks>
        /// <typeparam name="TFrom">The type of the object being copied.</typeparam>
        /// <typeparam name="TTo">The type of the new object to be created.</typeparam>
        /// <param name="instance">The existing instance to use as a template.</param>
        /// <returns>A new instane of type TNewType, with the data from TOldType.</returns>
        public TTo ChangeType<TFrom, TTo>(TFrom instance)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Serialize(ms, instance);
                ms.Position = 0;
                return Deserialize<TTo>(ms);
            }
        }
#endif

        /// <summary>
        /// Serializes a given instance and deserializes it as a different type;
        /// this can be used to translate between wire-compatible objects (where
        /// two .NET types represent the same data), or to promote/demote a type
        /// through an inheritance hierarchy.
        /// </summary>
        /// <remarks>No assumption of compatibility is made between the types.</remarks>
        /// <typeparam name="TFrom">The type of the object being copied.</typeparam>
        /// <typeparam name="TTo">The type of the new object to be created.</typeparam>
        /// <param name="instance">The existing instance to use as a template.</param>
        /// <returns>A new instane of type TNewType, with the data from TOldType.</returns>
        public object ChangeType(object instance, System.Type to)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Serialize(ms, instance);
                ms.Position = 0;
                return Deserialize(ms, null, to);
            }
        }
    }

}

