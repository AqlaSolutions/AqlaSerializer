// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections.Generic;
using System.Diagnostics;
using AqlaSerializer.Serializers;
using System.Globalization;
using System.Reflection.Emit;
using AltLinq;
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
            bool tryAsReference;
            bool tryAsLateRef;
            object defaultValue;
            var l = CompleteLevel(settings, levelNumber, out tryAsReference, out tryAsLateRef, out defaultValue).Basic;
            Debug.Assert(l.ContentBinaryFormatHint != null, "l.ContentBinaryFormatHint != null");
            Debug.Assert(l.EnhancedFormat != null, "l.EnhancedFormat != null");
            Debug.Assert(l.WriteAsDynamicType != null, "l.WriteAsDynamicType != null");
            Debug.Assert(l.Collection.Append != null, "l.Collection.Append != null");
            Debug.Assert(l.Collection.PackedWireTypeForRead != null, "l.Collection.PackedWireTypeForRead != null");

            // TODO when changing collection element settings consider that IgnoreListHandling may be also disabled after making member
            // TODO postpone all checks for types when adding member till BuildSerializer, resolve everything only on buildserializer! till that have only local not inherited settings.
            // TODO do not allow EnumPassthru and other settings to affect anything until buildling serializer
            wireType = 0;
            Type itemType = l.Collection.ItemType ?? l.EffectiveType;

            if (itemType.IsArray && itemType.GetArrayRank() != 1)
                throw new NotSupportedException("Multi-dimension arrays are not supported");

            if (l.EffectiveType.IsArray && l.EffectiveType.GetArrayRank() != 1)
                throw new NotSupportedException("Multi-dimension arrays are not supported");

            // TODO use Collection.Format!

            bool itemTypeCanBeNull = !Helpers.IsValueType(itemType) || Helpers.GetNullableUnderlyingType(itemType) != null;

            bool isPacked = !itemTypeCanBeNull &&
                            l.Collection.ItemType != null
                            && l.Collection.Format == CollectionFormat.Google
                            && !RuntimeTypeModel.CheckTypeIsCollection(_model, l.Collection.ItemType)
                            && ListDecorator.CanPack(HelpersInternal.GetWireType(HelpersInternal.GetTypeCode(l.Collection.ItemType), l.ContentBinaryFormatHint.Value));

            IProtoSerializerWithWireType ser = null;

            if (l.Collection.ItemType != null)
            {
                Type nestedItemType = null;
                Type nestedDefaultType = null;
                MetaType.ResolveListTypes(_model, itemType, ref nestedItemType, ref nestedDefaultType);

                bool itemIsNestedCollection = nestedItemType != null;
                bool tryHandleAsRegistered = !isMemberOrNested || !itemIsNestedCollection;


                if (tryHandleAsRegistered)
                {
                    object dummy = null;
                    ser = _model.ValueSerializerBuilder.TryGetCoreSerializer(l.ContentBinaryFormatHint.Value, itemType, out wireType, ref tryAsReference, l.WriteAsDynamicType.Value, !l.Collection.Append.Value, isPacked, true, tryAsLateRef, ref dummy);
                }

                if (ser == null && itemIsNestedCollection)
                {
                    // if we already tried to lookup registered type no need to do it again
                    if (!tryHandleAsRegistered && nestedDefaultType == null)
                    {
                        MetaType metaType;
                        if (_model.FindOrAddAuto(itemType, false, true, false, out metaType) >= 0)
                            nestedDefaultType = metaType.ConstructType ?? metaType.Type;
                    }

                    if (tryAsReference && !CanTypeBeAsReference(l.Collection.ItemType, false))
                        tryAsReference = false;

                    if (nestedDefaultType == null)
                    {
                        MetaType metaType;
                        if (_model.FindOrAddAuto(itemType, false, true, false, out metaType) >= 0)
                            nestedDefaultType = metaType.ConstructType ?? metaType.Type;
                    }

                    var nestedLevel = settings.GetSettingsCopy(levelNumber + 1);

                    if (nestedLevel.IsNotAssignable)
                        throw new ProtoException("Nested collection item should be assignable");

                    nestedLevel.Basic.Collection.Append = false; // TODO throw if set to true: throw new ProtoException("AppendCollection is not supported for nested types: " + objectType.Name);

                    if (nestedLevel.Basic.Collection.ItemType == null)
                        nestedLevel.Basic.Collection.ItemType = nestedItemType;
                    else if (!Helpers.IsAssignableFrom(nestedItemType, nestedLevel.Basic.Collection.ItemType))
                        throw new ProtoException(
                            "Nested collection item type " + nestedLevel.Basic.Collection.ItemType + " is not assignable to " + nestedItemType + " for declared collection type " +
                            l.EffectiveType);
                    
                    if (nestedLevel.Basic.EffectiveType == null)
                        nestedLevel.Basic.EffectiveType = itemType;
                    else if (!Helpers.IsAssignableFrom(itemType, nestedLevel.Basic.EffectiveType))
                        throw new ProtoException(
                            "Nested collection type " + nestedLevel.Basic.EffectiveType + " is not assignable to " + itemType);
                    
                    settings.SetSettings(nestedLevel, levelNumber + 1);

                    WireType wt;
                    ser = BuildValueFinalSerializer(
                        settings,
                        true,
                        out wt,
                        levelNumber + 1);

                    isPacked = false;
                }
            }
            else
            {
                if (!isMemberOrNested) tryAsReference = false; // handled outside and not wrapped with collection
                isPacked = false; // it's not even a collection
                ser = _model.ValueSerializerBuilder.TryGetCoreSerializer(l.ContentBinaryFormatHint.Value, itemType, out wireType, ref tryAsReference, l.WriteAsDynamicType.Value, !l.Collection.Append.Value, isPacked, true, tryAsLateRef, ref defaultValue);
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
                ser = new NoNullDecorator(_model, ser, l.Collection.ItemType != null); // throw only for collection elements, otherwise don't write
            }

            if (itemType != ser.ExpectedType && (!l.WriteAsDynamicType.Value || !Helpers.IsAssignableFrom(ser.ExpectedType, itemType)))
                throw new ProtoException(string.Format("Wrong type in the tail; expected {0}, received {1}", ser.ExpectedType, itemType));


            // apply lists if appropriate
            if (l.Collection.ItemType != null)
            {
                if (l.EffectiveType.IsArray)
                {
                    ser = new ArrayDecorator(
                                     _model,
                                     ser,
                                     isPacked,
                                     wireType,
                                     l.EffectiveType,
                                     !l.Collection.Append.Value,
                                     !_model.ProtoCompatibility.AllowExtensionDefinitions.HasFlag(NetObjectExtensionTypes.Collection));
                }
                else
                {
                    ser = ListDecorator.Create(
                        _model,
                        l.EffectiveType,
                        l.CollectionConcreteType,
                        ser,
                        isPacked,
                        wireType,
                        !l.Collection.Append.Value,
                        !_model.ProtoCompatibility.AllowExtensionDefinitions.HasFlag(NetObjectExtensionTypes.Collection),
                        true);
                }

                if (isMemberOrNested)
                {
                    // dynamic type is not applied to lists
                    if (MetaType.IsNetObjectValueDecoratorNecessary(_model, l.EffectiveType, tryAsReference))
                    {
                        ser = new NetObjectValueDecorator(
                            ser,
                            Helpers.GetNullableUnderlyingType(l.EffectiveType) != null,
                            tryAsReference,
                            tryAsReference && CanReallyBeAsLateReference(_model.GetKey(l.EffectiveType, false, true), _model),
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

        public IProtoSerializerWithWireType TryGetCoreSerializer(BinaryDataFormat dataFormat, Type type, out WireType defaultWireType,
             bool tryAsReference, bool dynamicType, bool overwriteList, bool allowComplexTypes)
        {
            object dummy = null;
            return TryGetCoreSerializer(dataFormat, type, out defaultWireType, ref tryAsReference, dynamicType, overwriteList, false, allowComplexTypes, false, ref dummy);
        }

        public IProtoSerializerWithWireType TryGetCoreSerializer(BinaryDataFormat dataFormat, Type type, out WireType defaultWireType,
            ref bool tryAsReference, bool dynamicType, bool overwriteList, bool isPackedCollection, bool allowComplexTypes, bool tryAsLateRef, ref object defaultValue)
        {
            Type originalType = type;
#if !NO_GENERICS
            {
                Type tmp = Helpers.GetNullableUnderlyingType(type);
                if (tmp != null) type = tmp;
            }
#endif
            if (tryAsReference && !CanTypeBeAsReference(type, false))
                tryAsReference = false;

            defaultWireType = WireType.None;
            IProtoSerializerWithWireType ser = null;

            if (Helpers.IsEnum(type))
            {
                if (allowComplexTypes && _model != null)
                {
                    // need to do this before checking the typecode; an int enum will report Int32 etc
                    defaultWireType = WireType.Variant;
                    ser = new WireTypeDecorator(defaultWireType, new EnumSerializer(type, _model.GetEnumMap(type)));
                }
                else
                { // enum is fine for adding as a meta-type
                    defaultWireType = WireType.None;
                    return null;
                }
            }
            if (ser == null)
            {
                ser = TryGetBasicTypeSerializer(dataFormat, type, out defaultWireType, overwriteList);
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
                return (isPackedCollection || !allowComplexTypes) ? ser : DecorateValueSerializer(originalType, dynamicType ? dataFormat : (BinaryDataFormat?)null, ref tryAsReference, tryAsLateRef, ser);

            if (allowComplexTypes)
            {
                int key = _model.GetKey(type, false, true);

                defaultWireType = WireType.StartGroup;

                if (key >= 0 || dynamicType)
                {
                    if (dynamicType)
                        return new NetObjectValueDecorator(originalType, tryAsReference, dataFormat, _model);
                    else if (tryAsLateRef && tryAsReference && CanReallyBeAsLateReference(key, _model))
                    {
                        return new NetObjectValueDecorator(originalType, key, tryAsReference, true, _model[type], _model);
                    }
                    else if (MetaType.IsNetObjectValueDecoratorNecessary(_model, originalType, tryAsReference))
                        return new NetObjectValueDecorator(originalType, key, tryAsReference, false, _model[type], _model);
                    else
                        return new ModelTypeSerializer(type, key, _model[type]);
                }
            }
            defaultWireType = WireType.None;
            return null;
        }

        IProtoSerializerWithWireType DecorateValueSerializer(Type type, BinaryDataFormat? dynamicTypeDataFormat, ref bool asReference, bool tryAsLateRef, IProtoSerializerWithWireType ser)
        {
            if (Helpers.IsValueType(type)) asReference = false;

            // Uri decorator is applied after default value
            // because default value for Uri is treated as string

            if (ser.ExpectedType == _model.MapType(typeof(string)) && type == _model.MapType(typeof(Uri)))
            {
                ser = new UriDecorator(_model, ser);
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
                ser = new NetObjectValueDecorator(type, asReference, dynamicTypeDataFormat.Value, _model);
            }
            else if (MetaType.IsNetObjectValueDecoratorNecessary(_model, type, asReference))
            {
                ser = new NetObjectValueDecorator(ser, Helpers.GetNullableUnderlyingType(type) != null, asReference, tryAsLateRef & asReference && CanReallyBeAsLateReference(_model.GetKey(type, false, true), _model), _model);
            }
            else
            {
                asReference = false;
            }

            return ser;
        }

        internal static bool CanReallyBeAsLateReference(int key, RuntimeTypeModel model, bool forRead = false)
        {
            if (key < 0) return false;
            MetaType mt = model[key];
            return CanTypeBeAsLateReference(mt) &&
                   (forRead || model.ProtoCompatibility.AllowExtensionDefinitions.HasFlag(NetObjectExtensionTypes.LateReference));
        }

        internal static bool CanTypeBeAsLateReference(MetaType mt)
        {
            return mt != null && !mt.Type.IsArray && mt.GetSurrogateOrSelf() == mt && !mt.IsAutoTuple && !Helpers.IsValueType(mt.Type);
        }

        internal static bool CanTypeBeAsReference(Type type, bool autoTuple)
        {
            return !autoTuple && !Helpers.IsValueType(type);// && Helpers.GetTypeCode(type) != ProtoTypeCode.String;
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
                    defaultWireType = WireType.String;
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

        ValueSerializationSettings.LevelValue CompleteLevel(ValueSerializationSettings vs, int levelNr, out bool asReference, out bool asLateReference, out object defaultValue)
        {
            var lv = vs.GetSettingsCopy(levelNr);
            var level = lv.Basic; // do not use lv.Basic, it's overwritten at the end of this method

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

            // TODO merge type settings
            //int memberTypeMetaKey = _model.GetKey(MemberType, false, false);
            //if (memberTypeMetaKey >= 0)
            //{
            //    var typeSettings = _model[memberTypeMetaKey].GetSettings());
            //    level0 = MemberLevelSettingsValue.Merge(typeSettings, level0);
            //}

            asLateReference = false;

            #region Reference and dynamic type

            {
                asReference = GetAsReferenceDefault(_model, level.EffectiveType, false, false);

                EnhancedMode wm = level.EnhancedWriteMode;
                if (level.EnhancedFormat == false)
                {
                    asReference = false;
                    wm = EnhancedMode.NotSpecified;
                }
                else if (wm != EnhancedMode.NotSpecified)
                    asReference = wm == EnhancedMode.LateReference || wm == EnhancedMode.Reference;


                bool dynamicType = level.WriteAsDynamicType.GetValueOrDefault();
                if (dynamicType && level.EnhancedFormat == false) throw new ProtoException("Dynamic type write mode strictly requires enhanced MemberFormat for value of type " + level.EffectiveType);
                if (wm == EnhancedMode.LateReference)
                {
                    int key = _model.GetKey(level.EffectiveType, false, false);
                    // we check here only theoretical possibility for type but not whether it's enabled on model level
                    if (key <= 0 || !ValueSerializerBuilder.CanTypeBeAsLateReference(_model[key]))
                        throw new ProtoException("Value can't be written as late reference because of its type " + level.EffectiveType);

                    asLateReference = true;
                }

                if (asReference)
                {
                    // we check here only theoretical possibility for type but not whether it's enabled on model level
                    if (!ValueSerializerBuilder.CanTypeBeAsReference(level.EffectiveType, false))
                        throw new ProtoException("Value can't be written as reference because of its type " + level.EffectiveType);

                    level.EnhancedFormat = true;
                    level.EnhancedWriteMode = wm = asLateReference ? EnhancedMode.LateReference : EnhancedMode.Reference;
                    if (asLateReference && dynamicType)
                        throw new ProtoException("Dynamic type write mode is not available for LateReference enhanced mode for " + level.EffectiveType);
                }
            }

            #endregion

            #region Collections
            {
                int idx = _model.FindOrAddAuto(level.EffectiveType, false, true, false);
                if (idx >= 0 && _model[level.EffectiveType].IgnoreListHandling)
                    ResetCollectionSettings(ref level);
                else
                {
                    Type newCollectionConcreteType = null;
                    Type newItemType = null;

                    MetaType.ResolveListTypes(_model, level.EffectiveType, ref newItemType, ref newCollectionConcreteType);

                    if (level.Collection.ItemType == null)
                        level.Collection.ItemType = newItemType;

                    if (level.CollectionConcreteType == null && idx >= 0)
                        level.CollectionConcreteType = _model[level.EffectiveType].ConstructType;

                    // should not override with default because what if specified something like List<string> for IList? 
                    if (level.CollectionConcreteType == null)
                        level.CollectionConcreteType = newCollectionConcreteType;
                    else if (!Helpers.IsAssignableFrom(level.EffectiveType, level.CollectionConcreteType))
                    {
                        throw new ProtoException(
                            "Specified CollectionConcreteType " + level.CollectionConcreteType.Name + " is not assignable to " + level.EffectiveType);
                    }

                    if (level.Collection.ItemType == null)
                        ResetCollectionSettings(ref level);
                    else if (!level.Collection.Append.GetValueOrDefault() && lv.IsNotAssignable)
                    {
                        if (level.Collection.Append == null)
                            level.Collection.Append = true;
                        else
                            throw new ProtoException("The property is not writable but AppendCollection was set to false");
                    }
                }
            }
            #endregion

            lv.Basic = level.GetInitializedToValueOrDefault();

            vs.SetSettings(lv, levelNr);

#if FORCE_LATE_REFERENCE
            if (asReference)
                asLateReference = true;
#endif
            return lv;
        }

        static void ResetCollectionSettings(ref MemberLevelSettingsValue level0)
        {
            level0.Collection.ItemType = null;
            level0.Collection.PackedWireTypeForRead = null;
            level0.Collection.Format = CollectionFormat.NotSpecified;
            level0.CollectionConcreteType = null;
        }

        internal static bool GetAsReferenceDefault(RuntimeTypeModel model, Type memberType, bool isProtobufNetLegacyMember, bool isDeterminatedAsAutoTuple)
        {
            if (ValueSerializerBuilder.CanTypeBeAsReference(memberType, isDeterminatedAsAutoTuple))
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
    }
}
#endif