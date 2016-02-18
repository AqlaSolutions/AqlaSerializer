// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections.Generic;
using System.Diagnostics;
using AqlaSerializer.Serializers;
using System.Globalization;
using AltLinq;
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
    /// Represents a member (property/field) that is mapped to a protobuf field
    /// </summary>
    public class ValueMember
    {
        private readonly int fieldNumber;
        /// <summary>
        /// The number that identifies this member in a protobuf stream
        /// </summary>
        public int FieldNumber { get { return fieldNumber; } }
        private readonly MemberInfo member;
        /// <summary>
        /// Gets the member (field/property) which this member relates to.
        /// </summary>
        public MemberInfo Member { get { return member; } }
        private readonly Type parentType, itemType, defaultType, memberType;
        private object defaultValue;
        /// <summary>
        /// Within a list / array / etc, the type of object for each item in the list (especially useful with ArrayList)
        /// </summary>
        public Type ItemType { get { return itemType; } }
        /// <summary>
        /// The underlying type of the member
        /// </summary>
        public Type MemberType { get { return memberType; } }

        /// <summary>
        /// For abstract types (IList etc), the type of concrete object to create (if required)
        /// </summary>
        public Type DefaultType { get { return defaultType; } }

        /// <summary>
        /// The type the defines the member
        /// </summary>
        public Type ParentType { get { return parentType; } }

        /// <summary>
        /// The default value of the item (members with this value will not be serialized)
        /// </summary>
        public object DefaultValue
        {
            get { return defaultValue; }
            set
            {
                ThrowIfFrozen();
                defaultValue = value;
            }
        }

        private RuntimeTypeModel model;
        /// <summary>
        /// Creates a new ValueMember instance
        /// </summary>
        public ValueMember(RuntimeTypeModel model, Type parentType, NormalizedMappedMember mappedMember)
            : this(model, mappedMember.Tag, mappedMember.MappingState.Input.EffectiveMemberType, mappedMember[0].Collection.ItemType, mappedMember[0].CollectionConcreteType, mappedMember[0].ContentBinaryFormatHint.GetValueOrDefault())
        {
            // temp to ensure mapping correctness
            MemberInfo member = mappedMember.Member;
            Type memberType = mappedMember.MappingState.Input.EffectiveMemberType;
            object defaultValue = mappedMember.MappingState.MainValue.DefaultValue;
            int fieldNumber = mappedMember.Tag;


            if (member == null) throw new ArgumentNullException("member");
            if (parentType == null) throw new ArgumentNullException("parentType");
            if (fieldNumber < 1 && !Helpers.IsEnum(parentType)) throw new ArgumentOutOfRangeException("fieldNumber");

            this.member = member;
            this.parentType = parentType;
            if (fieldNumber < 1 && !Helpers.IsEnum(parentType)) throw new ArgumentOutOfRangeException("fieldNumber");
            //#if WINRT
            if (defaultValue != null && model.MapType(defaultValue.GetType()) != memberType)
            //#else
            //            if (defaultValue != null && !memberType.IsInstanceOfType(defaultValue))
            //#endif
            {
                defaultValue = ParseDefaultValue(memberType, defaultValue);
            }
            this.defaultValue = defaultValue;
            this.asReference = GetAsReferenceDefault(model, memberType, false, IsDeterminatedAsAutoTuple);

            if (!Helpers.IsNullOrEmpty(mappedMember.Name)) SetName(mappedMember.Name);
            if (mappedMember[0].Collection.Format == CollectionFormat.Google) IsPacked = true;
            IsRequired = mappedMember.MainValue.IsRequiredInSchema;
            AppendCollection = mappedMember[0].Collection.Append.GetValueOrDefault();
            if (!AppendCollection && !Helpers.CanWrite(model, member)) AppendCollection = true;
            EnhancedMode wm = mappedMember[0].EnhancedWriteMode;
            if (mappedMember[0].MemberFormat == MemberFormat.Compact)
            {
                AsReference = false;
                wm = EnhancedMode.NotSpecified;
            }
            else if (wm != EnhancedMode.NotSpecified)
                AsReference = wm == EnhancedMode.LateReference || wm == EnhancedMode.Reference;
            bool dynamicTypeLocal = mappedMember[0].WriteAsDynamicType.GetValueOrDefault();
            if (dynamicTypeLocal && mappedMember[0].MemberFormat == MemberFormat.Compact) throw new ArgumentException("Dynamic type write mode strictly requires not Compact MemberFormat");
            DynamicType = dynamicTypeLocal;
            if (wm == EnhancedMode.LateReference) AsLateReference = true;
        }

        // autotuple is determinated when building serializer
        // AsReference is re-checked in Serializer property

        internal static bool GetAsReferenceDefault(RuntimeTypeModel model, Type memberType, bool isProtobufNetLegacyMember, bool isDeterminatedAsAutoTuple)
        {
            if (CheckCanBeAsReference(memberType, isDeterminatedAsAutoTuple))
            {
                if (RuntimeTypeModel.CheckTypeDoesntRequireContract(model, memberType))
                    isProtobufNetLegacyMember = false; // inbuilt behavior types doesn't depend on member legacy behavior

                MetaType type = model.FindWithoutAdd(memberType);
                if (type != null)
                {
                    type = type.GetSurrogateOrSelf();
                    return type.AsReferenceDefault;
                }
                else
                { // we need to scan the hard way; can't risk recursion by fully walking it
                    return model.AutoAddStrategy.GetAsReferenceDefault(memberType, isProtobufNetLegacyMember);
                }
            }
            return false;
        }

        bool CheckCanThisBeAsReference()
        {
            return CheckCanBeAsReference(memberType, IsDeterminatedAsAutoTuple);
        }

        private bool IsDeterminatedAsAutoTuple { get { return this.serializer is TupleSerializer; } }

        internal static bool CheckCanBeAsReference(Type type, bool autoTuple)
        {
            return !autoTuple && !Helpers.IsValueType(type);// && Helpers.GetTypeCode(type) != ProtoTypeCode.String;
        }

        /// <summary>
        /// Creates a new ValueMember instance
        /// </summary>
        ValueMember(RuntimeTypeModel model, int fieldNumber, Type memberType, Type itemType, Type defaultType, BinaryDataFormat dataFormat)
        {

            if (memberType == null) throw new ArgumentNullException("memberType");
            if (model == null) throw new ArgumentNullException("model");
            this.fieldNumber = fieldNumber;
            this.memberType = memberType;
            this.itemType = itemType;
            this.defaultType = defaultType;

            this.model = model;
            this.dataFormat = dataFormat;
            // fake ValueMember could be created for lists
            // it will use ListDecorator with returnList = false
            // because it doesn't have writable member
            // so consider it read only for now
            this.AppendCollection = true;
        }
        internal object GetRawEnumValue()
        {
#if WINRT || PORTABLE || CF || FX11
            object value = ((FieldInfo)member).GetValue(null);
            switch(Helpers.GetTypeCode(Enum.GetUnderlyingType(((FieldInfo)member).FieldType)))
            {
                case ProtoTypeCode.SByte: return (sbyte)value;
                case ProtoTypeCode.Byte: return (byte)value;
                case ProtoTypeCode.Int16: return (short)value;
                case ProtoTypeCode.UInt16: return (ushort)value;
                case ProtoTypeCode.Int32: return (int)value;
                case ProtoTypeCode.UInt32: return (uint)value;
                case ProtoTypeCode.Int64: return (long)value;
                case ProtoTypeCode.UInt64: return (ulong)value;
                default:
                    throw new InvalidOperationException();
            }
#else
            return ((FieldInfo)member).GetRawConstantValue();
#endif
        }
        private static object ParseDefaultValue(Type type, object value)
        {
            {
                Type tmp = Helpers.GetNullableUnderlyingType(type);
                if (tmp != null) type = tmp;
            }
            if (value is string)
            {
                string s = (string)value;
                if (Helpers.IsEnum(type)) return Helpers.ParseEnum(type, s);

                switch (Helpers.GetTypeCode(type))
                {
                    case ProtoTypeCode.Boolean: return bool.Parse(s);
                    case ProtoTypeCode.Byte: return byte.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Char: // char.Parse missing on CF/phone7
                        if (s.Length == 1) return s[0];
                        throw new FormatException("Single character expected: \"" + s + "\"");
                    case ProtoTypeCode.DateTime: return DateTime.Parse(s, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Decimal: return decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Double: return double.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Int16: return short.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Int32: return int.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Int64: return long.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.SByte: return sbyte.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.Single: return float.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.String: return s;
                    case ProtoTypeCode.UInt16: return ushort.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.UInt32: return uint.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.UInt64: return ulong.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
                    case ProtoTypeCode.TimeSpan: return TimeSpan.Parse(s);
                    case ProtoTypeCode.Uri: return s; // Uri is decorated as string
                    case ProtoTypeCode.Guid: return new Guid(s);
                }
            }
#if FEAT_IKVM
            if (Helpers.IsEnum(type)) return value; // return the underlying type instead
            System.Type convertType = null;
            switch (Helpers.GetTypeCode(type))
            {
                case ProtoTypeCode.SByte: convertType = typeof(sbyte); break;
                case ProtoTypeCode.Int16: convertType = typeof(short); break;
                case ProtoTypeCode.Int32: convertType = typeof(int); break;
                case ProtoTypeCode.Int64: convertType = typeof(long); break;
                case ProtoTypeCode.Byte: convertType = typeof(byte); break;
                case ProtoTypeCode.UInt16: convertType = typeof(ushort); break;
                case ProtoTypeCode.UInt32: convertType = typeof(uint); break;
                case ProtoTypeCode.UInt64: convertType = typeof(ulong); break;
                case ProtoTypeCode.Single: convertType = typeof(float); break;
                case ProtoTypeCode.Double: convertType = typeof(double); break;
                case ProtoTypeCode.Decimal: convertType = typeof(decimal); break;
            }
            if (convertType != null) return Convert.ChangeType(value, convertType, CultureInfo.InvariantCulture);
            throw new ArgumentException("Unable to process default value: " + value + ", " + type.FullName);
#else
            if (Helpers.IsEnum(type)) return Enum.ToObject(type, value);
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
#endif
        }

        private IProtoSerializerWithWireType serializer;
        internal IProtoSerializerWithWireType Serializer
        {
            get
            {
                if (serializer == null)
                {
                    serializer = BuildSerializer();
                    if (AsReference && !CheckCanThisBeAsReference())
                        AsReference = false;
                }
                return serializer;
            }
        }

        private BinaryDataFormat dataFormat;
        /// <summary>
        /// Specifies the rules used to process the field; this is used to determine the most appropriate
        /// wite-type, but also to describe subtypes <i>within</i> that wire-type (such as SignedVariant)
        /// </summary>
        public BinaryDataFormat DataFormat
        {
            get { return dataFormat; }
            set { ThrowIfFrozen(); this.dataFormat = value; }
        }

        /// <summary>
        /// Indicates whether this field should follow strict encoding rules; this means (for example) that if a "fixed32"
        /// is encountered when "variant" is defined, then it will fail (throw an exception) when parsing. Note that
        /// when serializing the defined type is always used.
        /// </summary>
        public bool IsStrict
        {
            // TODO not used? remove
            get { return HasFlag(OPTIONS_IsStrict); }
            set { SetFlag(OPTIONS_IsStrict, value, true); }
        }

        /// <summary>
        /// Indicates whether this field should use packed encoding (which can save lots of space for repeated primitive values).
        /// This option only applies to list/array data of primitive types (int, double, etc).
        /// </summary>
        public bool IsPacked
        {
            get { return HasFlag(OPTIONS_IsPacked); }
            set { SetFlag(OPTIONS_IsPacked, value, true); }
        }

        /// <summary>
        /// Indicates whether this field should *repace* existing values (the default is false, meaning *append*).
        /// This option only applies to list/array data.
        /// </summary>
        public bool AppendCollection
        {
            get { return HasFlag(OPTIONS_AppendCollection); }
            set { SetFlag(OPTIONS_AppendCollection, value, true); }
        }

        // TODO used only to disable WRITING default value but can just not specify default value at all!, remove this
        /// <summary>
        /// Indicates whether this field is mandatory.
        /// </summary>
        public bool IsRequired
        {
            get { return HasFlag(OPTIONS_IsRequired); }
            set { SetFlag(OPTIONS_IsRequired, value, true); }
        }

        private bool asReference;
        /// <summary>
        /// Enables full object-tracking/full-graph support.
        /// </summary>
        public bool AsReference
        {
            get { return asReference; }
            set
            {
                ThrowIfFrozen();
                if (!CheckCanThisBeAsReference()) value = false;
                asReference = value;
            }
        }

        bool _asLateReference;

        public bool AsLateReference
        {
            get
            {
                return _asLateReference;
            }
            set
            {
                ThrowIfFrozen();
                if (value)
                {
                    AsReference = true;
                    if (!AsReference) value = false;
                }
                _asLateReference = value;
            }
        }

        private bool dynamicType;
        /// <summary>
        /// Embeds the type information into the stream, allowing usage with types not known in advance.
        /// </summary>
        public bool DynamicType
        {
            get { return dynamicType; }
            set { ThrowIfFrozen(); dynamicType = value; }
        }

        private MethodInfo getSpecified, setSpecified;
        /// <summary>
        /// Specifies methods for working with optional data members.
        /// </summary>
        /// <param name="getSpecified">Provides a method (null for none) to query whether this member should
        /// be serialized; it must be of the form "bool {Method}()". The member is only serialized if the
        /// method returns true.</param>
        /// <param name="setSpecified">Provides a method (null for none) to indicate that a member was
        /// deserialized; it must be of the form "void {Method}(bool)", and will be called with "true"
        /// when data is found.</param>
        public void SetSpecified(MethodInfo getSpecified, MethodInfo setSpecified)
        {
            if (getSpecified != null)
            {
                if (getSpecified.ReturnType != model.MapType(typeof(bool))
                    || getSpecified.IsStatic
                    || getSpecified.GetParameters().Length != 0)
                {
                    throw new ArgumentException("Invalid pattern for checking member-specified", "getSpecified");
                }
            }
            if (setSpecified != null)
            {
                ParameterInfo[] args;
                if (setSpecified.ReturnType != model.MapType(typeof(void))
                    || setSpecified.IsStatic
                    || (args = setSpecified.GetParameters()).Length != 1
                    || args[0].ParameterType != model.MapType(typeof(bool)))
                {
                    throw new ArgumentException("Invalid pattern for setting member-specified", "setSpecified");
                }
            }
            ThrowIfFrozen();
            this.getSpecified = getSpecified;
            this.setSpecified = setSpecified;

        }
        private void ThrowIfFrozen()
        {
            if (serializer != null) throw new InvalidOperationException("The type cannot be changed once a serializer has been generated");
        }
        private IProtoSerializerWithWireType BuildSerializer()
        {
            int opaqueToken = 0;
            try
            {
                model.TakeLock(ref opaqueToken);// check nobody is still adding this type


                if (IsPacked && itemType != null && !RuntimeTypeModel.CheckTypeIsCollection(model, itemType)
                    && !ListDecorator.CanPack(HelpersInternal.GetWireType(HelpersInternal.GetTypeCode(itemType), BinaryDataFormat.Default)))
                {
                    throw new InvalidOperationException("Only simple data-types can use packed encoding");
                }
                object finalDefaultValue = null;
                if (defaultValue != null && !IsRequired && getSpecified == null)
                {   // note: "ShouldSerialize*" / "*Specified" / etc ^^^^ take precedence over defaultValue,
                    // as does "IsRequired"
                    finalDefaultValue = defaultValue;
                }

                var ser = BuildValueFinalSerializer(
                    memberType,
                    new CollectionSettings(itemType)
                    {
                        Append = AppendCollection,
                        DefaultType = DefaultType,
                        IsPacked = IsPacked,
                        ReturnList = member != null && Helpers.CanWrite(model, member)
                    },
                    DynamicType,
                    asReference,
                    DataFormat,
                    true,
                    // real type members always handle references if applicable
                    finalDefaultValue,
#if FORCE_LATE_REFERENCE
                    true,
#else
                    AsLateReference,
#endif
                    model);
                // TODO test aqlaattribute with IKVM
                if (member != null)
                {
                    PropertyInfo prop = member as PropertyInfo;
                    if (prop != null)
                    {
                        ser = new PropertyDecorator(model, parentType, (PropertyInfo)member, ser);
                    }
                    else
                    {
                        FieldInfo fld = member as FieldInfo;
                        if (fld != null)
                        {
                            ser = new FieldDecorator(parentType, (FieldInfo)member, ser);
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }
                    if (getSpecified != null || setSpecified != null)
                    {
                        ser = new MemberSpecifiedDecorator(getSpecified, setSpecified, ser);
                    }
                }
                return ser;
            }
            finally
            {
                model.ReleaseLock(opaqueToken);
            }
        }

        internal struct CollectionSettings
        {
            // Type objectItemType, bool tryAsReference, bool appendCollection, bool returnList, bool dynamicType, bool isPacked, Type defaultType, 
            public Type DefaultType { get; set; }
            public Type ItemType { get; set; }
            public bool Append { get; set; }
            public bool ReturnList { get; set; }
            public bool IsPacked { get; set; }

            public CollectionSettings Clone()
            {
                return this;
            }

            public CollectionSettings(Type itemType)
                : this()
            {
                ItemType = itemType;
            }
        }

        internal static IProtoSerializerWithWireType BuildValueFinalSerializer(Type objectType, CollectionSettings collection, bool dynamicType, bool tryAsReference, BinaryDataFormat dataFormat, bool isMemberOrNested, object defaultValue, bool tryAsLateRef, RuntimeTypeModel model)
        {
            WireType wireType;
            return BuildValueFinalSerializer(objectType, collection, dynamicType, tryAsReference, dataFormat, isMemberOrNested, defaultValue, tryAsLateRef, model, out wireType);
        }

        static IProtoSerializerWithWireType BuildValueFinalSerializer(Type objectType, CollectionSettings collection, bool dynamicType, bool tryAsReference, BinaryDataFormat dataFormat, bool isMemberOrNested, object defaultValue, bool tryAsLateRef, RuntimeTypeModel model, out WireType wireType)
        {
            Type collectionItemType = collection.ItemType;
            wireType = 0;
            bool isPacked = collection.IsPacked;
            bool isPackedOriginal = isPacked;
            Type itemType = collectionItemType ?? objectType;

            IProtoSerializerWithWireType ser = null;

            bool originalAsReference = tryAsReference;
            bool itemTypeCanBeNull = !Helpers.IsValueType(itemType) || Helpers.GetNullableUnderlyingType(itemType) != null;

            if (collectionItemType != null)
            {
                isPacked = isPacked && !itemTypeCanBeNull && ListDecorator.CanPack(HelpersInternal.GetWireType(HelpersInternal.GetTypeCode(collectionItemType), dataFormat)); // TODO warn?

                Type nestedItemType = null;
                Type nestedDefaultType = null;
                MetaType.ResolveListTypes(model, itemType, ref nestedItemType, ref nestedDefaultType);

                bool itemIsNestedCollection = nestedItemType != null;
                bool tryHandleAsRegistered = !isMemberOrNested || !itemIsNestedCollection;


                if (tryHandleAsRegistered)
                {
                    object dummy = null;
                    ser = TryGetCoreSerializer(model, dataFormat, itemType, out wireType, ref tryAsReference, dynamicType, !collection.Append, isPacked, true, tryAsLateRef, ref dummy);
                }

                if (ser == null && itemIsNestedCollection)
                {
                    // if we already tried to lookup registered type no need to do it again
                    if (!tryHandleAsRegistered && nestedDefaultType == null)
                    {
                        MetaType metaType;
                        if (model.FindOrAddAuto(itemType, false, true, false, out metaType) >= 0)
                            nestedDefaultType = metaType.ConstructType ?? metaType.Type;
                    }

                    if (tryAsReference && !CheckCanBeAsReference(collectionItemType, false))
                        tryAsReference = false;

                    //if (appendCollection) throw new ProtoException("AppendCollection is not supported for nested types: " + objectType.Name);

                    if (nestedDefaultType == null)
                    {
                        MetaType metaType;
                        if (model.FindOrAddAuto(itemType, false, true, false, out metaType) >= 0)
                            nestedDefaultType = metaType.ConstructType ?? metaType.Type;
                    }


                    var nestedColl = collection.Clone();
                    nestedColl.Append = false;
                    nestedColl.ReturnList = true;
                    nestedColl.ItemType = nestedItemType;
                    nestedColl.DefaultType = nestedDefaultType;

                    nestedColl.IsPacked = isPackedOriginal;

                    ser = BuildValueFinalSerializer(
                        itemType,
                        nestedColl,
                        dynamicType,
                        originalAsReference,
                        dataFormat,
                        true,
                        null,
                        tryAsLateRef,
                        model,
                        out wireType);

                    isPacked = false;
                }
            }
            else
            {
                if (!isMemberOrNested) tryAsReference = false; // handled outside and not wrapped with collection
                isPacked = false; // it's not even a collection
                ser = TryGetCoreSerializer(model, dataFormat, itemType, out wireType, ref tryAsReference, dynamicType, !collection.Append, isPacked, true, tryAsLateRef, ref defaultValue);
            }

            if (ser == null)
            {
                throw new InvalidOperationException("No serializer defined for type: " + itemType.FullName);
            }

            if (itemTypeCanBeNull &&
                (
                (Helpers.GetNullableUnderlyingType(itemType) != null && Helpers.GetNullableUnderlyingType(ser.ExpectedType) == null)
                // TODO get rid of ugly casting, maybe use builder pattern
                || (!Helpers.IsValueType(itemType) && !(ser is NetObjectValueDecorator))
                ))
            {
                // if not wrapped with net obj - wrap with write-null check
                ser = new NoNullDecorator(model, ser, collectionItemType != null); // throw only for collection elements, otherwise don't write
            }

            if (itemType != ser.ExpectedType && (!dynamicType || !Helpers.IsAssignableFrom(ser.ExpectedType, itemType)))
                throw new ProtoException(string.Format("Wrong type in the tail; expected {0}, received {1}", ser.ExpectedType, itemType));


            // apply lists if appropriate
            if (collectionItemType != null)
            {
                if (objectType.IsArray)
                {
                    ser = new ArrayDecorator(
                                     model,
                                     ser,
                                     isPacked,
                                     wireType,
                                     objectType,
                                     !collection.Append,
                                     !model.ProtoCompatibility.AllowExtensionDefinitions.HasFlag(NetObjectExtensionTypes.Collection));
                }
                else
                {
                    ser = ListDecorator.Create(
                        model,
                        objectType,
                        collection.DefaultType,
                        ser,
                        isPacked,
                        wireType,
                        collection.ReturnList,
                        !collection.Append,
                        !model.ProtoCompatibility.AllowExtensionDefinitions.HasFlag(NetObjectExtensionTypes.Collection),
                        true);
                }

                if (isMemberOrNested)
                {
                    // dynamic type is not applied to lists
                    if (MetaType.IsNetObjectValueDecoratorNecessary(model, objectType, tryAsReference))
                    {
                        ser = new NetObjectValueDecorator(
                            ser,
                            Helpers.GetNullableUnderlyingType(objectType) != null,
                            tryAsReference,
                            tryAsReference && CanBeAsLateReference(model.GetKey(objectType, false, true), model),
                            model);
                    }
                    else if (!Helpers.IsValueType(objectType) || Helpers.GetNullableUnderlyingType(objectType) != null)
                    {
                        ser = new NoNullDecorator(model, ser, false);
                    }
                }


                if (objectType != ser.ExpectedType && (!dynamicType || !Helpers.IsAssignableFrom(ser.ExpectedType, itemType)))
                    throw new ProtoException(string.Format("Wrong type in the tail; expected {0}, received {1}", ser.ExpectedType, itemType));
            }

            if (defaultValue != null)
                ser = new DefaultValueDecorator(model, defaultValue, ser);

            return ser;
        }

        private static WireType GetIntWireType(BinaryDataFormat format, int width)
        {
            switch (format)
            {
                case BinaryDataFormat.ZigZag: return WireType.SignedVariant;
                case BinaryDataFormat.FixedSize: return width == 32 ? WireType.Fixed32 : WireType.Fixed64;
                case BinaryDataFormat.TwosComplement:
                case BinaryDataFormat.Default: return WireType.Variant;
                default: throw new InvalidOperationException();
            }
        }
        private static WireType GetDateTimeWireType(BinaryDataFormat format)
        {
            switch (format)
            {
                case BinaryDataFormat.Group: return WireType.StartGroup;
                case BinaryDataFormat.FixedSize: return WireType.Fixed64;
                case BinaryDataFormat.Default: return WireType.String;
                default: throw new InvalidOperationException();
            }
        }

        internal static IProtoSerializerWithWireType TryGetCoreSerializer(RuntimeTypeModel model, BinaryDataFormat dataFormat, Type type, out WireType defaultWireType,
             bool tryAsReference, bool dynamicType, bool overwriteList, bool allowComplexTypes)
        {
            object dummy = null;
            return TryGetCoreSerializer(model, dataFormat, type, out defaultWireType, ref tryAsReference, dynamicType, overwriteList, false, allowComplexTypes, false, ref dummy);
        }

        internal static IProtoSerializerWithWireType TryGetCoreSerializer(RuntimeTypeModel model, BinaryDataFormat dataFormat, Type type, out WireType defaultWireType,
            ref bool tryAsReference, bool dynamicType, bool overwriteList, bool isPackedCollection, bool allowComplexTypes, bool tryAsLateRef, ref object defaultValue)
        {
            Type originalType = type;
#if !NO_GENERICS
            {
                Type tmp = Helpers.GetNullableUnderlyingType(type);
                if (tmp != null) type = tmp;
            }
#endif
            if (tryAsReference && !CheckCanBeAsReference(type, false))
                tryAsReference = false;

            defaultWireType = WireType.None;
            IProtoSerializerWithWireType ser = null;

            if (Helpers.IsEnum(type))
            {
                if (allowComplexTypes && model != null)
                {
                    // need to do this before checking the typecode; an int enum will report Int32 etc
                    defaultWireType = WireType.Variant;
                    ser = new WireTypeDecorator(defaultWireType, new EnumSerializer(type, model.GetEnumMap(type)));
                }
                else
                { // enum is fine for adding as a meta-type
                    defaultWireType = WireType.None;
                    return null;
                }
            }
            if (ser == null)
            {
                ser = TryGetBasicTypeSerializer(model, dataFormat, type, out defaultWireType, overwriteList);
                if (ser != null && Helpers.GetTypeCode(type) == ProtoTypeCode.Uri)
                {
                    // should be after uri but uri should always be before collection
                    if (defaultValue != null)
                    {
                        ser = new DefaultValueDecorator(model, defaultValue, ser);
                        defaultValue = null;
                    }
                }
            }
            if (ser == null)
            {
                var parseable = model.AllowParseableTypes ? ParseableSerializer.TryCreate(type, model) : null;
                if (parseable != null)
                {
                    defaultWireType = WireType.String;
                    ser = new WireTypeDecorator(defaultWireType, parseable);
                }
            }

            if (ser != null)
                return (isPackedCollection || !allowComplexTypes) ? ser : DecorateValueSerializer(model, originalType, dynamicType ? dataFormat : (BinaryDataFormat?)null, ref tryAsReference, tryAsLateRef, ser);

            if (allowComplexTypes)
            {
                int key = model.GetKey(type, false, true);

                defaultWireType = WireType.StartGroup;

                if (key >= 0 || dynamicType)
                {
                    if (dynamicType)
                        return new NetObjectValueDecorator(originalType, tryAsReference, dataFormat, model);
                    else if (tryAsLateRef && tryAsReference && CanBeAsLateReference(key, model))
                    {
                        return new NetObjectValueDecorator(originalType, key, tryAsReference, true, model[type], model);
                    }
                    else if (MetaType.IsNetObjectValueDecoratorNecessary(model, originalType, tryAsReference))
                        return new NetObjectValueDecorator(originalType, key, tryAsReference, false, model[type], model);
                    else
                        return new ModelTypeSerializer(type, key, model[type]);
                }
            }
            defaultWireType = WireType.None;
            return null;
        }

        internal static bool CanBeAsLateReference(int key, RuntimeTypeModel model, bool forRead = false)
        {
            if (key < 0) return false;
            MetaType mt = model[key];
            return !mt.Type.IsArray && mt.GetSurrogateOrSelf() == mt && !mt.IsAutoTuple && !Helpers.IsValueType(mt.Type) &&
                   (forRead || model.ProtoCompatibility.AllowExtensionDefinitions.HasFlag(NetObjectExtensionTypes.LateReference));
        }

        static IProtoSerializerWithWireType TryGetBasicTypeSerializer(RuntimeTypeModel model, BinaryDataFormat dataFormat, Type type, out WireType defaultWireType, bool overwriteList)
        {
            ProtoTypeCode code = Helpers.GetTypeCode(type);
            switch (code)
            {
                case ProtoTypeCode.Int32:
                    defaultWireType = GetIntWireType(dataFormat, 32);
                    return new WireTypeDecorator(defaultWireType, new Int32Serializer(model));
                case ProtoTypeCode.UInt32:
                    defaultWireType = GetIntWireType(dataFormat, 32);
                    return new WireTypeDecorator(defaultWireType, new UInt32Serializer(model));
                case ProtoTypeCode.Int64:
                    defaultWireType = GetIntWireType(dataFormat, 64);
                    return new WireTypeDecorator(defaultWireType, new Int64Serializer(model));
                case ProtoTypeCode.UInt64:
                    defaultWireType = GetIntWireType(dataFormat, 64);
                    return new WireTypeDecorator(defaultWireType, new UInt64Serializer(model));
                case ProtoTypeCode.Single:
                    defaultWireType = WireType.Fixed32;
                    return new WireTypeDecorator(defaultWireType, new SingleSerializer(model));
                case ProtoTypeCode.Double:
                    defaultWireType = WireType.Fixed64;
                    return new WireTypeDecorator(defaultWireType, new DoubleSerializer(model));
                case ProtoTypeCode.Boolean:
                    defaultWireType = WireType.Variant;
                    return new WireTypeDecorator(defaultWireType, new BooleanSerializer(model));
                case ProtoTypeCode.DateTime:
                    defaultWireType = GetDateTimeWireType(dataFormat);
                    return new WireTypeDecorator(defaultWireType, new DateTimeSerializer(model));
                case ProtoTypeCode.Decimal:
                    defaultWireType = WireType.String;
                    return new WireTypeDecorator(defaultWireType, new DecimalSerializer(model));
                case ProtoTypeCode.Byte:
                    defaultWireType = GetIntWireType(dataFormat, 32);
                    return new WireTypeDecorator(defaultWireType, new ByteSerializer(model));
                case ProtoTypeCode.SByte:
                    defaultWireType = GetIntWireType(dataFormat, 32);
                    return new WireTypeDecorator(defaultWireType, new SByteSerializer(model));
                case ProtoTypeCode.Char:
                    defaultWireType = WireType.Variant;
                    return new WireTypeDecorator(defaultWireType, new CharSerializer(model));
                case ProtoTypeCode.Int16:
                    defaultWireType = GetIntWireType(dataFormat, 32);
                    return new WireTypeDecorator(defaultWireType, new Int16Serializer(model));
                case ProtoTypeCode.UInt16:
                    defaultWireType = GetIntWireType(dataFormat, 32);
                    return new WireTypeDecorator(defaultWireType, new UInt16Serializer(model));
                case ProtoTypeCode.TimeSpan:
                    defaultWireType = GetDateTimeWireType(dataFormat);
                    return new WireTypeDecorator(defaultWireType, new TimeSpanSerializer(model));
                case ProtoTypeCode.Guid:
                    defaultWireType = WireType.String;
                    return new WireTypeDecorator(defaultWireType, new GuidSerializer(model));
                case ProtoTypeCode.ByteArray:
                    defaultWireType = WireType.String;
                    return new WireTypeDecorator(defaultWireType, new BlobSerializer(model, overwriteList));
                case ProtoTypeCode.Uri: // treat uri as string; wrapped in decorator later
                case ProtoTypeCode.String:
                    defaultWireType = WireType.String;
                    return new WireTypeDecorator(defaultWireType, new StringSerializer(model));
                case ProtoTypeCode.Type:
                    defaultWireType = WireType.String;
                    return new WireTypeDecorator(defaultWireType, new SystemTypeSerializer(model));
            }

            defaultWireType = WireType.None;
            return null;
        }

        static IProtoSerializerWithWireType DecorateValueSerializer(RuntimeTypeModel model, Type type, BinaryDataFormat? dynamicTypeDataFormat, ref bool asReference, bool tryAsLateRef, IProtoSerializerWithWireType ser)
        {
            if (Helpers.IsValueType(type)) asReference = false;

            // Uri decorator is applied after default value
            // because default value for Uri is treated as string

            if (ser.ExpectedType == model.MapType(typeof(string)) && type == model.MapType(typeof(Uri)))
            {
                ser = new UriDecorator(model, ser);
            }
#if PORTABLE
                else if (ser.ExpectedType == model.MapType(typeof(string)) && type.FullName == typeof(Uri).FullName)
                {
                    // In PCLs, the Uri type may not match (WinRT uses Internal/Uri, .Net uses System/Uri)
                    ser = new ReflectedUriDecorator(type, model, ser);
                }
#endif
            if (dynamicTypeDataFormat != null)
            {
                ser = new NetObjectValueDecorator(type, asReference, dynamicTypeDataFormat.Value, model);
            }
            else if (MetaType.IsNetObjectValueDecoratorNecessary(model, type, asReference))
            {
                ser = new NetObjectValueDecorator(ser, Helpers.GetNullableUnderlyingType(type) != null, asReference, tryAsLateRef & asReference && CanBeAsLateReference(model.GetKey(type, false, true), model), model);
            }
            else
            {
                asReference = false;
            }

            return ser;
        }

        private string name;
        internal void SetName(string name)
        {
            ThrowIfFrozen();
            this.name = name;
        }
        /// <summary>
        /// Gets the logical name for this member in the schema (this is not critical for binary serialization, but may be used
        /// when inferring a schema).
        /// </summary>
        public string Name
        {
            get { return Helpers.IsNullOrEmpty(name) ? member.Name : name; }
        }

        private const byte
           OPTIONS_IsStrict = 1,
           OPTIONS_IsPacked = 2,
           OPTIONS_IsRequired = 4,
           OPTIONS_AppendCollection = 8,
           OPTIONS_SupportNull = 16;

        private byte flags;
        private bool HasFlag(byte flag) { return (flags & flag) == flag; }
        private void SetFlag(byte flag, bool value, bool throwIfFrozen)
        {
            if (throwIfFrozen && HasFlag(flag) != value)
            {
                ThrowIfFrozen();
            }
            if (value)
                flags |= flag;
            else
                flags = (byte)(flags & ~flag);
        }

        /// <summary>
        /// Should lists have extended support for null values? Note this makes the serialization less efficient.
        /// </summary>
        [Obsolete("In AqlaSerializer nulls support goes without saying")]
        public bool SupportNull
        {
            get { return AsReference || HasFlag(OPTIONS_SupportNull); }
            set { SetFlag(OPTIONS_SupportNull, value, true); }
        }

        internal string GetSchemaTypeName(bool applyNetObjectProxy, ref bool requiresBclImport)
        {
            Type effectiveType = ItemType;
            if (effectiveType == null) effectiveType = MemberType;
            return model.GetSchemaTypeName(effectiveType, DataFormat, applyNetObjectProxy && AsReference, applyNetObjectProxy && dynamicType, ref requiresBclImport);
        }

        internal ValueMember CloneAsUnfrozen(RuntimeTypeModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var vm = (ValueMember)MemberwiseClone();
            vm.model = model;
            vm.serializer = null;
            return vm;
        }

        internal sealed class Comparer : System.Collections.IComparer
#if !NO_GENERICS
, System.Collections.Generic.IComparer<ValueMember>
#endif
        {
            public static readonly ValueMember.Comparer Default = new Comparer();
            public int Compare(object x, object y)
            {
                return Compare(x as ValueMember, y as ValueMember);
            }
            public int Compare(ValueMember x, ValueMember y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                return x.FieldNumber.CompareTo(y.FieldNumber);
            }
        }
    }
}
#endif