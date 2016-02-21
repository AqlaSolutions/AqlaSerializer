// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections.Generic;
using System.Diagnostics;
using AqlaSerializer.Serializers;
using System.Globalization;
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
    public class ValueSerializerBuilder
    {
        internal static IProtoSerializerWithWireType BuildValueFinalSerializer(Type objectType, CollectionSettings collection, bool dynamicType, bool tryAsReference, BinaryDataFormat dataFormat, bool isMemberOrNested, object defaultValue, bool tryAsLateRef, RuntimeTypeModel model)
        {
            WireType wireType;
            return BuildValueFinalSerializer(objectType, collection, dynamicType, tryAsReference, dataFormat, isMemberOrNested, defaultValue, tryAsLateRef, model, out wireType);
        }

        static IProtoSerializerWithWireType BuildValueFinalSerializer(Type objectType, CollectionSettings collection, bool dynamicType, bool tryAsReference, BinaryDataFormat dataFormat, bool isMemberOrNested, object defaultValue, bool tryAsLateRef, RuntimeTypeModel model, out WireType wireType)
        {
            Type collectionItemType = collection.ItemType;
            if (collectionItemType != null)
            {
                // we should re-check list handling on this stage!
                // because it can be changed after setting up the member
                // TODO when changing collection element settings consider that IgnoreListHandling may be also disabled after making member
                // TODO postpone all checks for types when adding member till BuildSerializer, resolve everything only on buildserializer! till that have only local not inherited settings.
                // TODO do not allow EnumPassthru and other settings to affect anything until buildling serializer
                int k = model.GetKey(objectType, false, false);
                if (k != -1 && model[k].IgnoreListHandling)
                {
                    collection.ItemType = collectionItemType = null;
                }
            }

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

        internal static bool CanBeAsLateReference(int key, RuntimeTypeModel model, bool forRead = false)
        {
            if (key < 0) return false;
            MetaType mt = model[key];
            return !mt.Type.IsArray && mt.GetSurrogateOrSelf() == mt && !mt.IsAutoTuple && !Helpers.IsValueType(mt.Type) &&
                   (forRead || model.ProtoCompatibility.AllowExtensionDefinitions.HasFlag(NetObjectExtensionTypes.LateReference));
        }
        internal static bool CheckCanBeAsReference(Type type, bool autoTuple)
        {
            return !autoTuple && !Helpers.IsValueType(type);// && Helpers.GetTypeCode(type) != ProtoTypeCode.String;
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

    }
}
#endif