using System;
using AqlaSerializer.Meta;

namespace AqlaSerializer
{
    public static partial class NetObjectHelpers
    {
        /// <summary>
        /// Writes an *implementation specific* bundled .NET object, including (as options) type-metadata, identity/re-use, etc.
        /// </summary>
        public static SubItemToken WriteNetObject_Start(object value, bool isRoot, ProtoWriter dest, BclHelpers.NetObjectOptions options, out int dynamicTypeKey, out bool writeObject)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (dest == null) throw new ArgumentNullException(nameof(dest));

            dynamicTypeKey = -1;
            
            // length not prefixed to not move data in buffer twice just because of NetObject (will be another nested inside)
            // Read method expects group (no length prefix) for missing object keys
            SubItemToken token = ProtoWriter.StartSubItem(null, false, dest);

            // we store position inside group because half-written field (before StartSubItem) has number "written" but position not changed
            int pos = ProtoWriter.GetPosition(dest);

            bool dummy;

            if (value == null)
            {
                if (isRoot)
                    dest.NetCache.AddObjectKey(new object(), true, out dummy);
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
                int objectKey = dest.NetCache.AddObjectKey(value, false, out existing);
                ProtoWriter.WriteFieldHeader(existing ? FieldExistingObjectKey : FieldNewObjectKey, WireType.Variant, dest);
                ProtoWriter.WriteInt32(objectKey, dest);
                if (existing)
                {
                    writeObject = false;
                    if (isRoot) // in auxiliary list, need to mark root object with new key for NetObjectKeyPositionsList
                        dest.NetCache.AddObjectKey(value, true, out dummy);
                }
                else
                {
                    dest.NetCacheKeyPositionsList.SetPosition(objectKey, pos);
                }
            }
            else if (isRoot) throw new InvalidOperationException("Root should be always written as reference!");

            if (writeObject)
            {
                if ((options & BclHelpers.NetObjectOptions.DynamicType) != 0)
                {
                    bool existing;
                    Type type = value.GetType();

                    dynamicTypeKey = dest.GetTypeKey(ref type);
                    int typeRefKey = dest.NetCache.AddObjectKey(type, false, out existing);
                    ProtoWriter.WriteFieldHeader(existing ? FieldExistingTypeKey : FieldNewTypeKey, WireType.Variant, dest);
                    if (!existing) dest.NetCacheKeyPositionsList.SetPosition(typeRefKey, pos);
                    ProtoWriter.WriteInt32(typeRefKey, dest);
                    if (!existing)
                    {
                        ProtoWriter.WriteFieldHeader(FieldTypeName, WireType.String, dest);
                        ProtoWriter.WriteString(dest.SerializeType(type), dest);
                    }
                }

                // do nothing, write will be outside
                ProtoWriter.WriteFieldHeaderBegin((options & BclHelpers.NetObjectOptions.WriteAsLateReference) != 0 ? FieldLateReferenceObject : FieldObject, dest);
            }
            return token;
#endif
        }
    }
}