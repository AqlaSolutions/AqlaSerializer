using System;

namespace AqlaSerializer
{
    public static partial class NetObjectHelpers
    {
        // this method is split because we need to insert our implementation inside and it's hard to deal with delegates in emit

        // full version for registered types
        /// <summary>
        /// Reads an *implementation specific* bundled .NET object, including (as options) type-metadata, identity/re-use, etc.
        /// </summary>
        public static object ReadNetObject(object value, ProtoReader source, int key, Type type, BclHelpers.NetObjectOptions options)
        {
            SubItemToken token;
            bool isNewObject;
            var r = ReadNetObjectInternal(value, source, key, type, options, out token, out isNewObject);
            ProtoReader.EndSubItem(token, true, source);
            return r;
        }
        
        // inject version
        public static object ReadNetObject_StartInject(object value, ProtoReader source, ref Type type, BclHelpers.NetObjectOptions options, out SubItemToken token, 
            out bool shouldEnd, out int newObjectKey, out int newTypeKey, out bool isType)
        {
            bool isNewObject;
            int key = -1;
            token = ReadNetObject_Start(ref value, source, ref key, ref type, options, out isNewObject, out newObjectKey, out newTypeKey, out isType, out shouldEnd);
            return value;
        }

        public static void ReadNetObject_EndInjectAndNoteNewObject(object value, ProtoReader source, object oldValue, Type type, int newObjectKey, int newTypeKey, bool isType, BclHelpers.NetObjectOptions options, SubItemToken token)
        {
            ReadNetObject_NoteNewObject(value, source, oldValue, type, newObjectKey, newTypeKey, isType, options);
            ProtoReader.EndSubItem(token, source);
        }

        static object ReadNetObjectInternal(object value, ProtoReader source, int key, Type type, BclHelpers.NetObjectOptions options, out SubItemToken token, out bool isNewObject)
        {
            int newObjectKey;
            int newTypeKey;
            bool isType;
            bool shouldEnd;
            token = ReadNetObject_Start(ref value, source, ref key, ref type, options, out isNewObject, out newObjectKey, out newTypeKey, out isType, out shouldEnd);
            if (shouldEnd)
            {
                object oldValue = value;
                value = ReadNetObject_NewObject(oldValue, source, key, type, isType);
                ReadNetObject_NoteNewObject(value, source, oldValue, type, newObjectKey, newTypeKey, isType, options);
            }
            return value;
        }

        static SubItemToken ReadNetObject_Start(ref object value, ProtoReader source, ref int key, ref Type type, BclHelpers.NetObjectOptions options, out bool isNewObject, out int newObjectKey, out int newTypeKey, out bool isType, out bool shouldEnd)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            isNewObject = false;
            isType = false;
            shouldEnd = false;

            newObjectKey = -1;
            newTypeKey = -1;

            SubItemToken token = ProtoReader.StartSubItem(source);

            int fieldNumber;
            if ((fieldNumber = source.ReadFieldHeader()) == 0)
            {
                // null handling
                return token;
            }
            int tmp;
            do
            {
                switch (fieldNumber)
                {
                    case FieldExistingObjectKey:
                        tmp = source.ReadInt32();
                        // null handling
                        value = source.NetCache.GetKeyedObject(tmp);
                        break;
                    case FieldNewObjectKey:
                        isNewObject = true;
                        newObjectKey = source.ReadInt32();
                        break;
                    case FieldExistingTypeKey:
                        tmp = source.ReadInt32();
                        type = (Type)source.NetCache.GetKeyedObject(tmp);
                        key = source.GetTypeKey(ref type);
                        break;
                    case FieldNewTypeKey:
                        newTypeKey = source.ReadInt32();
                        break;
                    case FieldTypeName:
                        string typeName = source.ReadString();
                        type = source.DeserializeType(typeName);
                        if (type == null)
                        {
                            throw new ProtoException("Unable to resolve type: " + typeName + " (you can use the TypeModel.DynamicTypeFormatting event to provide a custom mapping)");
                        }
                        if (type == typeof(string))
                        {
                            key = -1;
                        }
                        else
                        {
                            key = source.GetTypeKey(ref type);
                            if (key < 0)
                                throw new InvalidOperationException("Dynamic type is not a contract-type: " + type.Name);
                        }
                        break;
                    case FieldObject:
                        bool isString = type == typeof(string);
                        isType = !isString && Helpers.IsAssignableFrom(typeof(Type), type);
                        shouldEnd = true;
                        bool isUri = type == typeof(Uri);
                        bool wasNull = value == null;
                        bool lateSet = wasNull && (isString || isType || isUri || ((options & BclHelpers.NetObjectOptions.LateSet) != 0));

                        if (newObjectKey >= 0 && !lateSet)
                        {
                            if (value == null)
                            {
                                source.TrapNextObject(newObjectKey);
                            }
                            else
                            {
                                source.NetCache.SetKeyedObject(newObjectKey, value);
                            }
                            if (newTypeKey >= 0) source.NetCache.SetKeyedObject(newTypeKey, type);
                        }
                        return token;
                    default:
                        source.SkipField();
                        break;
                }
            } while ((fieldNumber = source.ReadFieldHeader()) > 0);
            return token;
#endif
        }

        public static object ReadNetObject_NewObject(object oldValue, ProtoReader source, int key, Type type, bool isType)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            bool isString = type == typeof(string);
            bool isUri = type == typeof(Uri);
            object value = null;
            if (isString)
            {
                value = source.ReadString();
            }
            else if (isType)
            {
                value = source.ReadType();
            }
            else if (isUri)
            {
                value = new Uri(source.ReadString());
            }
            else
            {
                value = ProtoReader.ReadTypedObject(oldValue, key, source, type);
            }
            return value;
#endif
        }

        static void ReadNetObject_NoteNewObject(object value, ProtoReader source, object oldValue, Type type, int newObjectKey, int newTypeKey, bool isType, BclHelpers.NetObjectOptions options)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            bool isString = type == typeof(string);
            bool isUri = type == typeof(Uri);
            bool wasNull = oldValue == null;
            bool lateSet = wasNull && (isString || isType || isUri || ((options & BclHelpers.NetObjectOptions.LateSet) != 0));

            if (newObjectKey >= 0)
            {
                if (wasNull && !lateSet)
                { // this both ensures (via exception) that it *was* set, and makes sure we don't shout
                  // about changed references
                    oldValue = source.NetCache.GetKeyedObject(newObjectKey);
                }
                if (lateSet)
                {
                    source.NetCache.SetKeyedObject(newObjectKey, value);
                    if (newTypeKey >= 0) source.NetCache.SetKeyedObject(newTypeKey, type);
                }
            }
            if (newObjectKey >= 0 && !lateSet && !ReferenceEquals(oldValue, value))
            {
                //throw new ProtoException("A reference-tracked object changed reference during deserialization");
            }
            if (newObjectKey < 0 && newTypeKey >= 0)
            {  // have a new type, but not a new object
                source.NetCache.SetKeyedObject(newTypeKey, type);
            }
            if (newObjectKey >= 0 && (options & BclHelpers.NetObjectOptions.AsReference) == 0)
            {
                throw new ProtoException("Object key in input stream, but reference-tracking was not expected");
            }
#endif
        }
        
        private const int
            FieldExistingObjectKey = 1,
            FieldNewObjectKey = 2,
            FieldExistingTypeKey = 3,
            FieldNewTypeKey = 4,
            FieldTypeName = 8,
            FieldObject = 10;

    }
}