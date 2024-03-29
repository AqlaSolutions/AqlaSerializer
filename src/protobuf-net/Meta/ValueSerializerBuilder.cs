﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections.Generic;
using System.Diagnostics;
using AqlaSerializer.Serializers;
using System.Globalization;
using AltLinq; using System.Linq;
using AqlaSerializer.Internal;
using AqlaSerializer.Settings;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

namespace AqlaSerializer.Meta
{
    internal class ValueSerializerBuilder : IValueSerializerBuilder
    {
        readonly RuntimeTypeModel _model;

        public ValueSerializerBuilder(RuntimeTypeModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            _model = model;
        }

        public IProtoSerializerWithWireType BuildValueFinalSerializer(ValueSerializationSettings settings, bool isMemberOrNested, out WireType wireType)
        {
            return BuildValueFinalSerializer(settings, isMemberOrNested, out wireType, 0);
        }

        IProtoSerializerWithWireType BuildValueFinalSerializer(ValueSerializationSettings settings, bool isMemberOrNested, out WireType wireType, int levelNumber)
        {
            object defaultValue;
            var l = CompleteLevel(settings, levelNumber, out defaultValue).Basic;
            // to ensure that model can be copied and used again
            for (int i = 1; i <= 3; i++)
            {
                var l2 = CompleteLevel(settings, levelNumber, out defaultValue);
                Debug.Assert(l.Equals(l2.Basic));
            }

            Debug.Assert(l.ContentBinaryFormatHint != null, "l.ContentBinaryFormatHint != null");
            Debug.Assert(l.WriteAsDynamicType != null, "l.WriteAsDynamicType != null");
            Debug.Assert(l.Collection.Append != null, "l.Collection.Append != null");

            // postpone all checks for types when adding member till BuildSerializer, resolve everything only on buildserializer! till that have only local not inherited settings.
            // do not allow EnumPassthru and other settings to affect anything until buildling serializer
            wireType = 0;
            Type itemType = l.Collection.ItemType ?? l.EffectiveType;

            bool itemTypeCanBeNull = CanTypeBeNull(itemType);

            bool protoPacked = CanBePackedCollection(l);

            IProtoSerializerWithWireType ser = null;

            if (l.Collection.IsCollection)
            {
                Type nestedItemType = null;
                Type nestedDefaultType = null;
                int idx = _model.FindOrAddAuto(itemType, false, true, false);
                MetaType type = idx < 0 ? null : _model[itemType];
                if (!type?.GetFinalSettingsCopy().IgnoreListHandling ?? true)
                    MetaType.ResolveListTypes(_model, itemType, ref nestedItemType, ref nestedDefaultType);

                bool itemIsNestedCollection = nestedItemType != null;

                // primitive types except System.Object may be handled as nested through recursion
                bool tryHandleAsRegistered = !isMemberOrNested || itemType == _model.MapType(typeof(object));

                if (tryHandleAsRegistered)
                {
                    var nestedLevel = settings.GetSettingsCopy(levelNumber + 1);
                    nestedLevel = PrepareNestedLevelForBuild(nestedLevel, itemType);
                    settings.SetSettings(nestedLevel, levelNumber + 1);
                    // should use its level settings and merge from type, ...
                    ser = BuildValueFinalSerializer(settings, true, out wireType, levelNumber + 1);

                    //object dummy = null;

                    //ser = TryGetCoreSerializer(l.ContentBinaryFormatHint.Value, nestedLevel.Basic.EffectiveType, out wireType, ref nestedLevel.Basic.Format, nestedLevel.Basic.WriteAsDynamicType.GetValueOrDefault(), l.Collection.Append.Value, isPacked, true, ref dummy);
                    //if (ser != null)
                    //    ThrowIfHasMoreLevels(settings, levelNumber + 1, l, ", no more nested type detected");
                }
                else if (!itemIsNestedCollection)
                {
                    var nestedLevel = settings.GetSettingsCopy(levelNumber + 1);
                    nestedLevel = PrepareNestedLevelForBuild(nestedLevel, itemType);
                    nestedLevel.Basic.Collection.ItemType = null; // IgnoreListHandling or not a collection
                    settings.SetSettings(nestedLevel, levelNumber + 1);

                    ser = BuildValueFinalSerializer(settings, true, out wireType, levelNumber + 1);
                }

                if (ser == null && itemIsNestedCollection)
                {
                    // if we already tried to lookup registered type no need to do it again

                    MetaType metaType;
                    if (_model.FindOrAddAuto(itemType, false, true, false, out metaType) >= 0)
                        nestedDefaultType = metaType.GetFinalSettingsCopy().ConstructType ?? nestedDefaultType ?? metaType.Type;

                    var nestedLevel = settings.GetSettingsCopy(levelNumber + 1);

                    if (nestedLevel.Basic.Collection.ConcreteType == null)
                        nestedLevel.Basic.Collection.ConcreteType = nestedDefaultType;

                    if (nestedLevel.IsNotAssignable)
                        throw new ProtoException("Nested collection item should be assignable");

                    nestedLevel.Basic.Collection.Append = false;
                        // TODO throw if set to true: throw new ProtoException("AppendCollection is not supported for nested types: " + objectType.Name);

                    if (nestedLevel.Basic.Collection.ItemType == null)
                        nestedLevel.Basic.Collection.ItemType = nestedItemType;
                    else if (!Helpers.IsAssignableFrom(nestedItemType, nestedLevel.Basic.Collection.ItemType))
                        throw new ProtoException(
                            "Nested collection item type " + nestedLevel.Basic.Collection.ItemType + " is not assignable to " + nestedItemType + " for declared collection type " +
                            l.EffectiveType);

                    nestedLevel = PrepareNestedLevelForBuild(nestedLevel, itemType);

                    settings.SetSettings(nestedLevel, levelNumber + 1);

                    WireType wt;
                    ser = BuildValueFinalSerializer(
                        settings,
                        true,
                        out wt,
                        levelNumber + 1);

                    protoPacked = false;
                }
            }
            else
            {
                // handled outside and not wrapped with collection
                if (!isMemberOrNested) l.Format = ValueFormat.Compact;

                protoPacked = false; // it's not even a collection
                ser = TryGetCoreSerializer(l.ContentBinaryFormatHint.Value, itemType, out wireType, ref l.Format, l.WriteAsDynamicType.Value, l.Collection.Append.Value, protoPacked, true, ref defaultValue);
                if (ser != null)
                    ThrowIfHasMoreLevels(settings, levelNumber, l, ", no more nested type detected");
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
                // nested level may be not collection and already wrapped with nonull, may later add check whether handled as registered vs as nested
                if (!(ser is NoNullDecorator))
                {
                    // if not wrapped with net obj - wrap with write-null check
                    ser = new NoNullDecorator(_model, ser, l.Collection.ItemType != null); // throw only for collection elements, otherwise don't write
                }
            }

            if (itemType != ser.ExpectedType && (!l.WriteAsDynamicType.Value || !Helpers.IsAssignableFrom(ser.ExpectedType, itemType)))
                throw new ProtoException(string.Format("Wrong type in the tail; expected {0}, received {1}", ser.ExpectedType, itemType));

            // apply lists if appropriate
            if (l.Collection.IsCollection)
            {
                bool protoCompatibility = l.Collection.Format == CollectionFormat.Protobuf || l.Collection.Format == CollectionFormat.ProtobufNotPacked;

                WireType expectedTailWireType;
                if (!protoCompatibility)
                {
                    expectedTailWireType = l.Collection.PackedWireTypeForRead ?? wireType;
                    if (expectedTailWireType == WireType.None) expectedTailWireType = wireType;
                    Debug.Assert(!protoPacked); // should be determinated before passing to TryGetCoreSerializer
                    protoPacked = false;
                }
                else
                {
                    if (CanPackProtoCompatible(l.Collection.ItemType, l.ContentBinaryFormatHint))
                    {
                        expectedTailWireType = l.Collection.PackedWireTypeForRead ?? wireType;
                        if (expectedTailWireType == WireType.None) expectedTailWireType = wireType;
                    }
                    else
                    {
                        expectedTailWireType = WireType.None;
                        protoPacked = false;
                    }
                }

                if (l.EffectiveType.IsArray)
                {
                    if (l.EffectiveType.GetArrayRank() == 1)
                    {
                        ser = new ArrayDecorator(
                            _model,
                            ser,
                            protoPacked,
                            expectedTailWireType,
                            l.EffectiveType,
                            !l.Collection.Append.Value,
                            l.Collection.ArrayLengthReadLimit.Value,
                            protoCompatibility);
                    }
                    else
                    {
                        if (protoCompatibility)
                            throw new NotSupportedException("Multi-dimensional arrays are supported only in Enhanced collection format");

                        ser = new MultiDimensionalArrayDecorator(
                            _model,
                            ser,
                            l.EffectiveType,
                            !l.Collection.Append.Value,
                            l.Collection.ArrayLengthReadLimit.Value);
                    }
                }
                else
                {
                    ser = ListDecorator.Create(
                        _model,
                        l.EffectiveType,
                        l.Collection.ConcreteType,
                        ser,
                        protoPacked,
                        expectedTailWireType,
                        !l.Collection.Append.Value,
                        protoCompatibility,
                        true,
                        l.Collection.ArrayLengthReadLimit.Value);
                }

                if (isMemberOrNested)
                {
                    if (MetaType.IsNetObjectValueDecoratorNecessary(_model, l.Format))
                    {
                        ser = new NetObjectValueDecorator(
                            ser,
                            Helpers.GetNullableUnderlyingType(l.EffectiveType) != null,
                            l.Format == ValueFormat.Reference || l.Format == ValueFormat.LateReference,
                            l.Format == ValueFormat.LateReference && CanTypeBeAsLateReferenceOnBuildStage(_model.GetKey(l.EffectiveType, false, true), _model),
                            !_model.ProtoCompatibility.SuppressNullWireType,
                            _model);
                    }
                    else if (!Helpers.IsValueType(l.EffectiveType) || Helpers.GetNullableUnderlyingType(l.EffectiveType) != null)
                    {
                        ser = new NoNullDecorator(_model, ser, false);
                    }
                }


                if (l.EffectiveType != ser.ExpectedType && (!l.WriteAsDynamicType.Value || !Helpers.IsAssignableFrom(ser.ExpectedType, itemType)))
                    throw new ProtoException(string.Format("Wrong type in the tail; expected {0}, received {1}", ser.ExpectedType, itemType));
            }

            if (levelNumber == 0 && defaultValue != null)
                ser = new DefaultValueDecorator(_model, defaultValue, ser);

            return ser;
        }

        public bool CanBePackedCollection(MemberLevelSettingsValue level)
        {
            return level.Collection.IsCollection
                   && CanPackProtoCompatible(level.Collection.ItemType, level.ContentBinaryFormatHint)
                   && level.Collection.Format == CollectionFormat.Protobuf;
        }

        public bool CanPackProtoCompatible(Type type, BinaryDataFormat? contentBinaryFormatHint)
        {
            return type != _model.MapType(typeof(string))
                && !CanTypeBeNull(type)
                && !RuntimeTypeModel.CheckTypeIsCollection(_model, type)
                && ListDecorator.CanPackProtoCompatible(HelpersInternal.GetWireType(HelpersInternal.GetTypeCode(type), contentBinaryFormatHint.GetValueOrDefault()));
        }

        internal static void EnsureCorrectFormatSpecified(RuntimeTypeModel model, ref ValueFormat format, Type type, ref bool? dynamicType, bool finalStage)
        {
            if (format == ValueFormat.NotSpecified)
            {
                format = CanTypeBeAsReference(type)
                             ? ValueFormat.Reference
                             : ((dynamicType.GetValueOrDefault() || CanTypeBeNull(type))
#if FORCE_ADVANCED_VERSIONING
                                || !model.SkipForcedAdvancedVersioning
#endif
                                    ? ValueFormat.MinimalEnhancement
                                    : ValueFormat.Compact);
            }
            else if ((format == ValueFormat.LateReference || format == ValueFormat.Reference) && !CanTypeBeAsReference(type))
                throw new ProtoException("Type " + type + " can't be handled as reference");
            else if (format == ValueFormat.LateReference && !CanTypeBeAsLateReference(type))
                throw new ProtoException("Type " + type + " can't be handled as late reference");
            else if (format == ValueFormat.LateReference && dynamicType.GetValueOrDefault())
                throw new ProtoException("Type " + type + " can't be handled both as dynamic and as late reference");
            else if (format == ValueFormat.Compact && dynamicType.GetValueOrDefault())
                throw new ProtoException("Type " + type + " can't be handled both as dynamic and as compact");

            // no forced late reference because late reference serializers won't use nested level settings
//            if (format == ValueFormat.Reference && !dynamicType.GetValueOrDefault() && CanTypeBeAsReference(type))
//            {
//#if FORCE_LATE_REFERENCE
//                if (!model.SkipForcedLateReference)
//                    format = ValueFormat.LateReference;
//#endif
//            }

            if (finalStage)
            {
                if (model.ProtoCompatibility.SuppressValueEnhancedFormat)
                {
                    format = ValueFormat.Compact;
                    dynamicType = false;
                }
                else if (format == ValueFormat.LateReference)
                {
                    int idx = model.FindOrAddAuto(type, false, true, false);
                    if (idx < 0 || !CanTypeBeAsLateReferenceOnBuildStage(idx, model) || model.ProtoCompatibility.SuppressOwnRootFormat)
                        format = ValueFormat.Reference;
                }
            }
        }


        public static ValueFormat GetDefaultLegacyFormat(Type type, RuntimeTypeModel model)
        {
            return CanTypeBeNull(type)
#if FORCE_ADVANCED_VERSIONING
                   || !model.SkipForcedAdvancedVersioning
#endif
                       ? ValueFormat.MinimalEnhancement
                       : ValueFormat.Compact;
        }


        internal static bool CanTypeBeNull(Type type)
        {
            return !Helpers.IsValueType(type) || Helpers.GetNullableUnderlyingType(type) != null;
        }

        static void ThrowIfHasMoreLevels(ValueSerializationSettings settings, int currentLevelNr, MemberLevelSettingsValue currentLevel, string description)
        {
            if (settings.MaxSpecifiedNestedLevel > currentLevelNr)
            {
                throw new ProtoException(
                    "Found unused specified nested level settings, maximum possible nested level is " + currentLevelNr + "-" + currentLevel.EffectiveType.Name + description);
            }
        }

        ValueSerializationSettings.LevelValue PrepareNestedLevelForBuild(ValueSerializationSettings.LevelValue nestedLevel, Type itemType)
        {
            if (nestedLevel.Basic.EffectiveType == null)
                nestedLevel.Basic.EffectiveType = itemType;
            else if (!Helpers.IsAssignableFrom(itemType, nestedLevel.Basic.EffectiveType))
                throw new ProtoException(
                    "Nested collection type " + nestedLevel.Basic.EffectiveType + " is not assignable to " + itemType);

            // no no until we merge from type
            //EnsureCorrectFormatSpecified(_model, ref nestedLevel.Basic.Format, nestedLevel.Basic.EffectiveType, ref nestedLevel.Basic.WriteAsDynamicType, true);
            return nestedLevel;
        }

        public IProtoSerializerWithWireType TryGetSimpleCoreSerializer(BinaryDataFormat dataFormat, Type type, out WireType defaultWireType)
        {
            object dummy = null;
            ValueFormat format = ValueFormat.Compact;
            return TryGetCoreSerializer(dataFormat, type, out defaultWireType, ref format, false, false, false, false, ref dummy);
        }

        public IProtoSerializerWithWireType TryGetCoreSerializer(BinaryDataFormat dataFormat, Type type, out WireType defaultWireType,
            ref ValueFormat format, bool dynamicType, bool appendCollection, bool isPackedCollection, bool allowComplexTypes, ref object defaultValue)
        {
            if (format == ValueFormat.NotSpecified) throw new ArgumentException("Format should be specified for TryGetCoreSerializer", nameof(format));
            if (format != ValueFormat.Compact && _model.ProtoCompatibility.SuppressValueEnhancedFormat)
                throw new InvalidOperationException("TryGetCoreSerializer should receive final format with ProtoCompatibility already taken into account");
            Type originalType = type;
            {
                Type tmp = Helpers.GetNullableUnderlyingType(type);
                if (tmp != null) type = tmp;
            }
            defaultWireType = WireType.None;
            IProtoSerializerWithWireType ser = null;

            if (Helpers.IsEnum(type))
            {
                if (allowComplexTypes && _model != null)
                {
                    // need to do this before checking the typecode; an int enum will report Int32 etc
                    defaultWireType = WireType.Variant;
                    ser = new WireTypeDecorator(defaultWireType, new EnumSerializer(type, _model.GetEnumMap(type), false));
                }
                else
                { // enum is fine for adding as a meta-type
                    defaultWireType = WireType.None;
                    return null;
                }
            }
            if (ser == null)
            {
                ser = TryGetBasicTypeSerializer(dataFormat, type, out defaultWireType, !appendCollection);
                if (ser != null && Helpers.GetTypeCode(type) == ProtoTypeCode.Uri)
                {
                    // should be after uri but uri should always be before collection
                    if (defaultValue != null)
                    {
                        ser = new DefaultValueDecorator(_model, defaultValue, ser);
                        defaultValue = null;
                    }
                }
            }
            if (ser == null)
            {
                var parseable = _model.AllowParseableTypes ? ParseableSerializer.TryCreate(type, _model) : null;
                if (parseable != null)
                {
                    defaultWireType = WireType.String;
                    ser = new WireTypeDecorator(defaultWireType, parseable);
                }
            }

            if (ser != null)
                return (isPackedCollection || !allowComplexTypes) ? ser : DecorateValueSerializer(originalType, dynamicType ? dataFormat : (BinaryDataFormat?)null, ref format, ser, ref defaultWireType);

            if (allowComplexTypes)
            {
                int baseKey = _model.GetKey(type, false, true);

                defaultWireType = WireType.StartGroup; // NetObjectHelpers always use Group

                if (baseKey >= 0 || dynamicType)
                {
                    if (dynamicType)
                        return new NetObjectValueDecorator(originalType, format == ValueFormat.Reference || format == ValueFormat.LateReference, dataFormat, !_model.ProtoCompatibility.SuppressNullWireType, _model);
                    else if (format == ValueFormat.LateReference && CanTypeBeAsLateReferenceOnBuildStage(baseKey, _model))
                    {
                        return new NetObjectValueDecorator(originalType, baseKey, true, true, _model[type], !_model.ProtoCompatibility.SuppressNullWireType, _model);
                    }
                    else if (MetaType.IsNetObjectValueDecoratorNecessary(_model, format))
                        return new NetObjectValueDecorator(originalType, baseKey, format == ValueFormat.Reference || format == ValueFormat.LateReference, false, _model[type], !_model.ProtoCompatibility.SuppressNullWireType, _model);
                    else
                    {
                        defaultWireType = (_model[type].PrefixLength ?? _model.ProtoCompatibility.UseLengthPrefixedNestingAsDefault) ? WireType.String : WireType.EndGroup;
                        return new ModelTypeSerializer(type, baseKey, _model[type], _model);
                    }
                }
                else
                {
                    if (format == ValueFormat.LateReference && CanTypeBeAsLateReferenceOnBuildStage(baseKey, _model))
                    { }
                    else if (MetaType.IsNetObjectValueDecoratorNecessary(_model, format))
                    { }
                    else
                    {
                        defaultWireType = _model.ProtoCompatibility.UseLengthPrefixedNestingAsDefault ? WireType.String : WireType.StartGroup;
                    }
                }
            }
            defaultWireType = WireType.None;
            return null;
        }

        IProtoSerializerWithWireType DecorateValueSerializer(Type type, BinaryDataFormat? dynamicTypeDataFormat, ref ValueFormat format, IProtoSerializerWithWireType ser, ref WireType defaultWireType)
        {
            // Uri decorator is applied after default value
            // because default value for Uri is treated as string

            if (ser.ExpectedType == _model.MapType(typeof(string)) && type == _model.MapType(typeof(Uri)))
            {
                ser = new UriDecorator(_model, ser);
            }
#if PORTABLE
            else if (ser.ExpectedType == _model.MapType(typeof(string)) && type.FullName == typeof(Uri).FullName)
            {
                // In PCLs, the Uri type may not match (WinRT uses Internal/Uri, .Net uses System/Uri)
                ser = new ReflectedUriDecorator(type, _model, ser);
            }
#endif
            if (dynamicTypeDataFormat != null)
            {
                defaultWireType = WireType.StartGroup;
                ser = new NetObjectValueDecorator(type, format == ValueFormat.Reference || format == ValueFormat.LateReference, dynamicTypeDataFormat.Value, !_model.ProtoCompatibility.SuppressNullWireType, _model);
            }
            else if (MetaType.IsNetObjectValueDecoratorNecessary(_model, format))
            {
                defaultWireType = WireType.StartGroup;
                ser = new NetObjectValueDecorator(
                    ser,
                    Helpers.GetNullableUnderlyingType(type) != null,
                    format == ValueFormat.Reference || format == ValueFormat.LateReference,
                    format == ValueFormat.LateReference && CanTypeBeAsLateReferenceOnBuildStage(_model.GetKey(type, false, true), _model),
                    !_model.ProtoCompatibility.SuppressNullWireType,
                    _model);
            }
            else
            {
                format = ValueFormat.Compact;
            }

            return ser;
        }

        internal static bool CanTypeBeAsLateReferenceOnBuildStage(int key, RuntimeTypeModel model, bool forRead = false)
        {
            if (key < 0) return false;
            MetaType mt = model[key];
            return CanTypeBeAsLateReference(mt.Type) && mt.GetSurrogateOrSelf() == mt && !mt.GetFinalSettingsCopy().IsAutoTuple && !model.ProtoCompatibility.SuppressOwnRootFormat;
        }

        internal static bool CanTypeBeAsLateReference(Type type)
        {
            return !type.IsArray && CanTypeBeAsReference(type);
        }


        internal static bool CanTypeBeAsReference(Type type)
        {
            return !Helpers.IsValueType(type);
        }

        IProtoSerializerWithWireType TryGetBasicTypeSerializer(BinaryDataFormat dataFormat, Type type, out WireType defaultWireType, bool overwriteList)
        {
            ProtoTypeCode code = Helpers.GetTypeCode(type);
            switch (code)
            {
                case ProtoTypeCode.Int32:
                    defaultWireType = GetIntWireType(dataFormat, 32);
                    return new WireTypeDecorator(defaultWireType, new Int32Serializer(_model));
                case ProtoTypeCode.UInt32:
                    defaultWireType = GetIntWireType(dataFormat, 32);
                    return new WireTypeDecorator(defaultWireType, new UInt32Serializer(_model));
                case ProtoTypeCode.Int64:
                    defaultWireType = GetIntWireType(dataFormat, 64);
                    return new WireTypeDecorator(defaultWireType, new Int64Serializer(_model));
                case ProtoTypeCode.UInt64:
                    defaultWireType = GetIntWireType(dataFormat, 64);
                    return new WireTypeDecorator(defaultWireType, new UInt64Serializer(_model));
                case ProtoTypeCode.Single:
                    defaultWireType = WireType.Fixed32;
                    return new WireTypeDecorator(defaultWireType, new SingleSerializer(_model));
                case ProtoTypeCode.Double:
                    defaultWireType = WireType.Fixed64;
                    return new WireTypeDecorator(defaultWireType, new DoubleSerializer(_model));
                case ProtoTypeCode.Boolean:
                    defaultWireType = WireType.Variant;
                    return new WireTypeDecorator(defaultWireType, new BooleanSerializer(_model));
                case ProtoTypeCode.DateTime:
                    defaultWireType = GetDateTimeWireType(dataFormat);
                    return new WireTypeDecorator(defaultWireType, new DateTimeSerializer(_model));
                case ProtoTypeCode.Decimal:
                    defaultWireType = WireType.String;
                    return new WireTypeDecorator(defaultWireType, new DecimalSerializer(_model));
                case ProtoTypeCode.Byte:
                    defaultWireType = GetIntWireType(dataFormat, 32);
                    return new WireTypeDecorator(defaultWireType, new ByteSerializer(_model));
                case ProtoTypeCode.SByte:
                    defaultWireType = GetIntWireType(dataFormat, 32);
                    return new WireTypeDecorator(defaultWireType, new SByteSerializer(_model));
                case ProtoTypeCode.Char:
                    defaultWireType = WireType.Variant;
                    return new WireTypeDecorator(defaultWireType, new CharSerializer(_model));
                case ProtoTypeCode.Int16:
                    defaultWireType = GetIntWireType(dataFormat, 32);
                    return new WireTypeDecorator(defaultWireType, new Int16Serializer(_model));
                case ProtoTypeCode.UInt16:
                    defaultWireType = GetIntWireType(dataFormat, 32);
                    return new WireTypeDecorator(defaultWireType, new UInt16Serializer(_model));
                case ProtoTypeCode.TimeSpan:
                    defaultWireType = GetDateTimeWireType(dataFormat);
                    return new WireTypeDecorator(defaultWireType, new TimeSpanSerializer(_model));
                case ProtoTypeCode.Guid:
                    defaultWireType = dataFormat == BinaryDataFormat.Group ? WireType.StartGroup : WireType.String;
                    return new WireTypeDecorator(defaultWireType, new GuidSerializer(_model));
                case ProtoTypeCode.ByteArray:
                    defaultWireType = WireType.String;
                    return new WireTypeDecorator(defaultWireType, new BlobSerializer(_model, overwriteList));
                case ProtoTypeCode.Uri: // treat uri as string; wrapped in decorator later
                case ProtoTypeCode.String:
                    defaultWireType = WireType.String;
                    return new WireTypeDecorator(defaultWireType, new StringSerializer(_model));
                case ProtoTypeCode.Type:
                    defaultWireType = WireType.String;
                    return new WireTypeDecorator(defaultWireType, new SystemTypeSerializer(_model));
            }

            defaultWireType = WireType.None;
            return null;
        }

        WireType GetIntWireType(BinaryDataFormat format, int width)
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

        WireType GetDateTimeWireType(BinaryDataFormat format)
        {
            switch (format)
            {
                case BinaryDataFormat.Group: return WireType.StartGroup;
                case BinaryDataFormat.FixedSize: return WireType.Fixed64;
                case BinaryDataFormat.Default: return WireType.String;
                default: throw new InvalidOperationException();
            }
        }


        object ParseDefaultValue(Type type, object value)
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

        ValueSerializationSettings.LevelValue CompleteLevel(ValueSerializationSettings vs, int levelNr, out object defaultValue)
        {
            var lv = vs.GetSettingsCopy(levelNr);
            var level = lv.Basic; // do not use lv.Basic, it's overwritten at the end of this method

            var originalLevel = level;

            if (levelNr == 0)
            {
                //#if WINRT
                if (vs.DefaultValue != null && _model.MapType(vs.DefaultValue.GetType()) != level.EffectiveType)
                    //#else
                    //            if (defaultValue != null && !memberType.IsInstanceOfType(defaultValue))
                    //#endif
                {
                    vs.DefaultValue = ParseDefaultValue(level.EffectiveType, vs.DefaultValue);
                }
                defaultValue = vs.DefaultValue;
            }
            else defaultValue = null;

            int idx = _model.FindOrAddAuto(level.EffectiveType, false, true, false);
            MetaType effectiveMetaType = null;
            if (idx >= 0)
            {
                effectiveMetaType = _model[idx];
                var typeSettings = effectiveMetaType.GetFinalSettingsCopy();
                level = MemberLevelSettingsValue.Merge(typeSettings.Member, level);
            }

            MemberDefaultsMode defaultsMode = level.DefaultsMode.GetValueOrDefault();
            if (level.Format == ValueFormat.NotSpecified && defaultsMode == MemberDefaultsMode.Legacy)
                level.Format = GetDefaultLegacyFormat(level.EffectiveType, _model);

            if (level.Format == ValueFormat.LateReference)
            {
                if (vs.MaxSpecifiedNestedLevel > levelNr)
                    throw new ProtoException("LateReference member levels can't have nested levels");
                var defaultSettings = effectiveMetaType?.GetFinalSettingsCopy().Member ?? new MemberLevelSettingsValue().GetInitializedToValueOrDefault();
                if (level.ContentBinaryFormatHint.GetValueOrDefault() != defaultSettings.ContentBinaryFormatHint.GetValueOrDefault()
                    || level.WriteAsDynamicType.GetValueOrDefault() != defaultSettings.WriteAsDynamicType.GetValueOrDefault()
                    || level.Collection.Append.GetValueOrDefault() != defaultSettings.Collection.Append.GetValueOrDefault()
                    || level.Collection.PackedWireTypeForRead != defaultSettings.Collection.PackedWireTypeForRead)
                {
                    throw new ProtoException("LateReference member levels can't override default member settings specified on MetaType");
                }
            }

            Type newCollectionConcreteType = null;
            Type newItemType = null;

            if (!effectiveMetaType?.GetFinalSettingsCopy().IgnoreListHandling ?? true)
                MetaType.ResolveListTypes(_model, level.EffectiveType, ref newItemType, ref newCollectionConcreteType);

            // tuples depend on collection
            if (defaultsMode == MemberDefaultsMode.LegacyTuple && level.Format == ValueFormat.NotSpecified && newItemType != null)
                level.Format = ValueFormat.Compact;

            EnsureCorrectFormatSpecified(_model, ref level.Format, level.EffectiveType, ref level.WriteAsDynamicType, true);

            #region Collections

            {
                if (effectiveMetaType?.GetFinalSettingsCopy().IgnoreListHandling ?? false)
                    ResetCollectionSettings(ref level);
                else
                {
                    // defaults for ItemType and others were already merged from type settings
                    if (level.Collection.ItemType != null)
                    {
                        if (level.Format == ValueFormat.LateReference)
                        {
                            Type defaultItemType = (effectiveMetaType?.GetFinalSettingsCopy().Member.Collection.ItemType ?? newItemType);
                            if (level.Collection.ItemType != defaultItemType)
                                throw new ProtoException("LateReference member settings level should have default collection item type (" + defaultItemType + ")");
                        }
                    }
                    else
                        level.Collection.ItemType = newItemType;

                    if (level.Collection.ItemType == null)
                        ResetCollectionSettings(ref level);
                    else
                    {
                        // should not override with default because: what if specified something like List<string> for IList?
                        if (level.Collection.ConcreteType != null)
                        {
                            if (!Helpers.IsAssignableFrom(level.EffectiveType, level.Collection.ConcreteType))
                            {
                                throw new ProtoException(
                                    "Specified CollectionConcreteType " + level.Collection.ConcreteType.Name + " is not assignable to " + level.EffectiveType);
                            }

                            if (level.Format == ValueFormat.LateReference)
                            {
                                Type defaultConcreteType = (effectiveMetaType?.GetFinalSettingsCopy().Member.Collection.ConcreteType ?? newCollectionConcreteType);
                                if (level.Collection.ConcreteType != defaultConcreteType)
                                    throw new ProtoException("LateReference member settings level should have default collection concrete type (" + defaultConcreteType + ")");
                            }
                        }
                        else level.Collection.ConcreteType = newCollectionConcreteType;

                        if (!level.Collection.Append.GetValueOrDefault() && lv.IsNotAssignable)
                        {
                            if (level.Collection.Append == null)
                                level.Collection.Append = true;
                            else
                                throw new ProtoException("The property is not writable but AppendCollection was set to false");
                        }

                        if (level.Collection.PackedWireTypeForRead == null)
                            level.Collection.PackedWireTypeForRead = WireType.None;
                        else if (level.Collection.PackedWireTypeForRead != WireType.None
                            && (level.Collection.Format == CollectionFormat.ProtobufNotPacked
                                || (level.Collection.Format == CollectionFormat.Protobuf && !CanPackProtoCompatible(level.Collection.ItemType, level.ContentBinaryFormatHint))))
                        {
                                throw new ProtoException("PackedWireTypeForRead " + level.Collection.PackedWireTypeForRead + " specified but type can't be packed");

                        }

                        if (level.Collection.Format == CollectionFormat.NotSpecified)
                        {
                            level.Collection.Format = !_model.ProtoCompatibility.SuppressCollectionEnhancedFormat
                                                          ? CollectionFormat.Enhanced
                                                          : CollectionFormat.Protobuf;
                        }
                        else if (level.Collection.Format == CollectionFormat.Enhanced && _model.ProtoCompatibility.SuppressCollectionEnhancedFormat)
                            level.Collection.Format = CollectionFormat.Protobuf;

                    }
                }

                if (level.Collection.ArrayLengthReadLimit == null)
                    level.Collection.ArrayLengthReadLimit = TypeModel.DefaultArrayLengthReadLimit;
                else if (level.Collection.ArrayLengthReadLimit <= 0)
                    throw new ProtoException("ArrayLengthReadLimit should be greater than zero or not specified");
            }

            #endregion

            lv.Basic = level.GetInitializedToValueOrDefault();

            vs.SetSettings(lv, levelNr);

            originalLevel.GetHashCode();

            return lv;
        }

        static void ResetCollectionSettings(ref MemberLevelSettingsValue level0)
        {
            level0.Collection.ItemType = null;
            level0.Collection.PackedWireTypeForRead = null;
            level0.Collection.Format = CollectionFormat.NotSpecified;
            level0.Collection.ConcreteType = null;
            level0.Collection.ArrayLengthReadLimit = null;
        }
    }
}
#endif