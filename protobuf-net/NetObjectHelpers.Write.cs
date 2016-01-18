using System;

namespace AqlaSerializer
{
    public static partial class NetObjectHelpers
    {
        /// <summary>
        /// Writes an *implementation specific* bundled .NET object, including (as options) type-metadata, identity/re-use, etc.
        /// </summary>
        public static void WriteNetObject(object value, ProtoWriter dest, int key, BclHelpers.NetObjectOptions options)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            bool write;
            SubItemToken t = WriteNetObject_Start(value, dest, key, options, false, out write);
            ProtoWriter.EndSubItem(t, dest);
#endif
        }

        /// <summary>
        /// Writes an *implementation specific* bundled .NET object, including (as options) type-metadata, identity/re-use, etc.
        /// </summary>
        public static SubItemToken WriteNetObject_StartRoot(object value, ProtoWriter dest, int key, BclHelpers.NetObjectOptions options, out bool writeObject)
        {
            return WriteNetObject_Start(value, dest, key, options, true, out writeObject);
        }

        /// <summary>
        /// Writes an *implementation specific* bundled .NET object, including (as options) type-metadata, identity/re-use, etc.
        /// </summary>
        public static SubItemToken WriteNetObject_StartInject(object value, ProtoWriter dest, BclHelpers.NetObjectOptions options, out bool writeObject)
        {
            if ((options & BclHelpers.NetObjectOptions.DynamicType) != 0) throw new ProtoException("Can't use dynamic type with non-registered net object type");
            return WriteNetObject_Start(value, dest, -1, options, false, out writeObject);
        }
        
        static SubItemToken WriteNetObject_Start(object value, ProtoWriter dest, int key, BclHelpers.NetObjectOptions options, bool root, out bool writeObject)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (dest == null) throw new ArgumentNullException("dest");
            bool dynamicType = (options & BclHelpers.NetObjectOptions.DynamicType) != 0,
                 asReference = (options & BclHelpers.NetObjectOptions.AsReference) != 0;
            WireType wireType = root
                ? WireType.String
                : dest.WireType;

            SubItemToken token = !root
                ? ProtoWriter.StartSubItem(null, dest)
                : new SubItemToken();

            // even root object can be wrapped with collection
            // so it's not true root
            writeObject = true;
            if (asReference)
            {
                bool existing;
                int objectKey;
                if (value != null)
                    objectKey = dest.NetCache.AddObjectKey(value, out existing);
                else
                {
                    // special handling for first empty element in list
                    objectKey = -2;
                    existing = true;
                }
                ProtoWriter.WriteFieldHeader(existing ? FieldExistingObjectKey : FieldNewObjectKey, WireType.Variant, dest);
                ProtoWriter.WriteInt32(objectKey, dest);
                if (existing)
                {
                    writeObject = false;
                }
            }

            if (writeObject)
            {
                if (dynamicType)
                {
                    bool existing;
                    Type type = value.GetType();

                    if (!(value is string))
                    {
                        key = dest.GetTypeKey(ref type);
                        if (key < 0) throw new InvalidOperationException("Dynamic type is not a contract-type: " + type.Name);
                    }
                    int typeKey = dest.NetCache.AddObjectKey(type, out existing);
                    ProtoWriter.WriteFieldHeader(existing ? FieldExistingTypeKey : FieldNewTypeKey, WireType.Variant, dest);
                    ProtoWriter.WriteInt32(typeKey, dest);
                    if (!existing)
                    {
                        ProtoWriter.WriteFieldHeader(FieldTypeName, WireType.String, dest);
                        ProtoWriter.WriteString(dest.SerializeType(type), dest);
                    }

                }
                ProtoWriter.WriteFieldHeader(FieldObject, wireType, dest);
                if (root)
                {
                    return ProtoWriter.StartSubItem(null, dest);
                }
                else if (value is string)
                {
                    ProtoWriter.WriteString((string)value, dest);
                }
                else if (value is Type)
                {
                    ProtoWriter.WriteType((Type)value, dest);
                }
                else if (value is Uri)
                {
                    ProtoWriter.WriteString(((Uri)value).AbsoluteUri, dest);
                }
                else if (!dynamicType && key < 0)
                {
                    // do nothing, write will be outside
                }
                else
                {
                    ProtoWriter.WriteObject(value, key, dest);
                }
            }
            return token;
#endif
        }
    }
}