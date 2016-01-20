using System;

namespace AqlaSerializer
{
    public static partial class NetObjectHelpers
    {
        // TODO move inside NetObjectSerializer!
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
        public static SubItemToken WriteNetObject_StartInject(object value, ProtoWriter dest, BclHelpers.NetObjectOptions options, out bool writeObject)
        {
            return WriteNetObject_Start(value, dest, -1, options, true, out writeObject);
        }
        
        static SubItemToken WriteNetObject_Start(object value, ProtoWriter dest, int key, BclHelpers.NetObjectOptions options, bool inject, out bool writeObject)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (dest == null) throw new ArgumentNullException("dest");

            // length not prefixed to not move data in buffer twice just because of NetObject (will be another nested inside)
            SubItemToken token = ProtoWriter.StartSubItem(null, false, dest);

            if (value == null)
            {
                // null handling
                writeObject = false;
                return token;
            }

            // even root object can be wrapped with collection
            // so it's not true root
            writeObject = true;
            if ((options & BclHelpers.NetObjectOptions.AsReference) != 0)
            {
                bool existing;
                int objectKey;
                objectKey = dest.NetCache.AddObjectKey(value, out existing);
                ProtoWriter.WriteFieldHeader(existing ? FieldExistingObjectKey : FieldNewObjectKey, WireType.Variant, dest);
                ProtoWriter.WriteInt32(objectKey, dest);
                if (existing)
                {
                    writeObject = false;
                }
            }

            if (writeObject)
            {
                if ((options & BclHelpers.NetObjectOptions.DynamicType) != 0)
                {
                    if (inject) throw new InvalidOperationException("Dynamic types should use registered type mode, *not inject*!");
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

                if (inject)
                {
                    ProtoWriter.WriteFieldHeaderBegin(FieldObject, dest);
                    // do nothing, write will be outside
                }
                else if (value is string)
                {
                    ProtoWriter.WriteFieldHeader(FieldObject, WireType.String, dest);
                    ProtoWriter.WriteString((string)value, dest);
                }
                else if (value is Type)
                {
                    ProtoWriter.WriteFieldHeader(FieldObject, WireType.String, dest);
                    ProtoWriter.WriteType((Type)value, dest);
                }
                else if (value is Uri)
                {
                    ProtoWriter.WriteFieldHeader(FieldObject, WireType.String, dest);
                    ProtoWriter.WriteString(((Uri)value).AbsoluteUri, dest);
                }
                else
                {
                    ProtoWriter.WriteFieldHeaderBegin(FieldObject, dest);
                    ProtoWriter.WriteObject(value, key, true, dest);
                }
            }
            return token;
#endif
        }
    }
}