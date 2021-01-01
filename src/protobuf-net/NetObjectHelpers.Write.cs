using System;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
#endif
using System.Diagnostics;
using AqlaSerializer.Meta;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;

#endif

namespace AqlaSerializer
{
    public static partial class NetObjectHelpers
    {
        /// <summary>
        /// Writes an *implementation specific* bundled .NET object, including (as options) type-metadata, identity/re-use, etc.
        /// </summary>
        public static SubItemToken WriteNetObject_Start(object value, ProtoWriter dest, BclHelpers.NetObjectOptions options, bool allowCancelField, out int dynamicTypeKey, out bool writeObject)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if (dest == null) throw new ArgumentNullException(nameof(dest));

            dynamicTypeKey = -1;
            
            // optimization: if nothing is written into this subgroup go back in buffer and remove subitem and field entirely
            // of course only if not flushed already
            // and only if we are in a state of a started and non-completed field here or no opened field (wiretype == none)
            // TODO test without this optimization
            long? cancelPos = allowCancelField && (dest.HasIncompleteField || dest.WireType == WireType.None) ? (long?)ProtoWriter.GetLongPosition(dest) : null;
            // never cancel header field
            if (cancelPos == 0) cancelPos = null;

            // length not prefixed to not move data in buffer twice just because of NetObject (will be another nested inside)
            // Read method expects group (no length prefix) for missing object keys
            SubItemToken token = ProtoWriter.StartSubItem(null, dest.TakeIsExpectingRootType(), dest);

            // we store position inside group, not outside, because half-written field (before StartSubItem) has number "written" but position not changed
            long insideStartPos = ProtoWriter.GetLongPosition(dest);
            
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
                int objectKey = dest.NetCache.AddObjectKey(value, out bool existing);
                ProtoWriter.WriteFieldHeader(existing ? FieldExistingObjectKey : FieldNewObjectKey, WireType.Variant, dest);
                ProtoWriter.WriteInt32(objectKey, dest);
                if (existing)
                {
                    writeObject = false;
                }
                else
                {
                    dest.NetCacheKeyPositionsList.SetPosition(objectKey, insideStartPos);
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
                        dest.NetCacheKeyPositionsList.SetPosition(typeRefKey, ProtoWriter.GetPosition(dest));
                        ProtoWriter.WriteString(dest.SerializeType(type), dest);
                    }
                }

                // if any of previous code written anything
                // we don't want to discard it with optimization
                if (ProtoWriter.GetLongPosition(dest) > insideStartPos) cancelPos = null;

                // do nothing, write will be outside
                // but if writing is cancelled we use FieldSkippedObject field number otherwise reading will consider empty group as null value
                ProtoWriter.WriteFieldHeaderBegin((options & BclHelpers.NetObjectOptions.WriteAsLateReference) != 0 ? FieldLateReferenceObject : FieldObject, dest);
                // alternative to FieldSkippedObject - seek back
                token.SeekOnEndOrMakeNullField = new SeekOnEndOrMakeNullFieldCondition { PositionShouldBeEqualTo = insideStartPos, ThenTrySeekToPosition = cancelPos, NullFieldNumber = FieldSkippedObject };
            }
            return token;
#endif
        }
    }
}