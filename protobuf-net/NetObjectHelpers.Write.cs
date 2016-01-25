using System;
using AqlaSerializer.Meta;

namespace AqlaSerializer
{
    public static partial class NetObjectHelpers
    {
        /// <summary>
        /// Writes an *implementation specific* bundled .NET object, including (as options) type-metadata, identity/re-use, etc.
        /// </summary>
        public static SubItemToken WriteNetObject_Start(object value, ProtoWriter dest, BclHelpers.NetObjectOptions options, out int dynamicTypeKey, out bool writeObject)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (dest == null) throw new ArgumentNullException("dest");

            dynamicTypeKey = -1;

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
                int objectKey = dest.NetCache.AddObjectKey(value, out existing);
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
                    bool existing;
                    Type type = value.GetType();

                    dynamicTypeKey = dest.GetTypeKey(ref type);
                    int typeRefKey = dest.NetCache.AddObjectKey(type, out existing);
                    ProtoWriter.WriteFieldHeader(existing ? FieldExistingTypeKey : FieldNewTypeKey, WireType.Variant, dest);
                    ProtoWriter.WriteInt32(typeRefKey, dest);
                    if (!existing)
                    {
                        ProtoWriter.WriteFieldHeader(FieldTypeName, WireType.String, dest);
                        ProtoWriter.WriteString(dest.SerializeType(type), dest);
                    }
                }

                // do nothing, write will be outside
                ProtoWriter.WriteFieldHeaderBegin(FieldObject, dest);
            }
            return token;
#endif
        }
    }
}