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
        // this method is split because we need to insert our implementation inside and it's hard to deal with delegates in emit

        public static void ReadNetObject_End(object value, ReadReturnValue r, ProtoReader source, object oldValue, Type type, BclHelpers.NetObjectOptions options)
        {
            if (r.ShouldRead)
            {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
                // late set may become here true e.g. for dynamic string
                bool wasNull = oldValue == null;
                bool lateSet = wasNull && ((options & BclHelpers.NetObjectOptions.LateSet) != 0);

                if (r.NewObjectKey >= 0)
                {
                    if (wasNull && !lateSet)
                    { // this both ensures (via exception) that it *was* set, and makes sure we don't shout
                        // about changed references
                        oldValue = source.NetCache.GetKeyedObject(r.NewObjectKey, false);

                        if (!ReferenceEquals(oldValue, value))
                        {
                            throw new ProtoException("A reference-tracked object changed reference during deserialization");
                        }
                    }
                    if (lateSet)
                    {
                        source.NetCache.SetKeyedObject(r.NewObjectKey, value, true);
                        // if lateset was false initially, we called TrapNextObject previously (e.g. dynamic string) 
                        ProtoReader.TryCancelTrapNextObject(r.NewObjectKey, source);
                    }
                }
                if (r.NewTypeRefKey >= 0) source.NetCache.SetKeyedObject(r.NewTypeRefKey, type);
#endif
            }
            ProtoReader.EndSubItem(r.Token, source);
            if (r.SeekToReturn != null)
            {
                var s = r.SeekToReturn.Value;
                source.SeekAndExchangeBlockEnd(s.Position, s.BlockEnd);
            }
        }

        public struct ReadReturnValue
        {
            public SubItemToken Token;
            public bool IsDynamic;
            public bool IsLateReference;
            public int NewObjectKey;
            public int NewTypeRefKey;
            public bool ShouldRead;
            public SeekToken? SeekToReturn;
            public bool IsSkipped;
        }

        public struct SeekToken
        {
            public long Position;
            public long BlockEnd;
        }

        /// <summary>
        /// Reads an *implementation specific* bundled .NET object, including (as options) type-metadata, identity/re-use, etc.
        /// </summary>
        public static ReadReturnValue ReadNetObject_Start(ref object value, ProtoReader source, ref Type type, BclHelpers.NetObjectOptions options, ref int typeKey, bool handleMissingKeys, bool replaceOldValue)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            var r = new ReadReturnValue
            {
                ShouldRead = false,
                NewObjectKey = -1,
                NewTypeRefKey = -1,
                IsDynamic = false,
                IsLateReference = false,
                Token = ProtoReader.StartSubItem(source),
            };
            
            int fieldNumber;
            if ((fieldNumber = source.ReadFieldHeader()) == 0)
            {
                // null handling
                // it can be handled outside if null wire type is allowed, or here
                // it's a different rule for different conditions
                // so we can't rely on it
                value = null;
                return r;
            }
            bool hasObject = false;
            do
            {
                int tmp;
                switch (fieldNumber)
                {
                    case FieldExistingObjectKey:
                        hasObject = true;
                        tmp = source.ReadInt32();
                        value = source.NetCache.GetKeyedObject(tmp, handleMissingKeys);
                        if (value == null)
                        {
                            // skip to group end
                            ProtoReader.EndSubItem(r.Token, true, source);
                            var seekToken = new SeekToken
                            {
                                Position = source.LongPosition, // store position on group end
                                BlockEnd = source.SeekAndExchangeBlockEnd(source.NetCacheKeyPositionsList.GetPosition(tmp))
                            };

                            if (!ProtoReader.HasSubValue(WireType.StartGroup, source))
                                throw new ProtoException("New object could not be found on specified position, net key: " + tmp);

                            r = ReadNetObject_Start(
                                ref value,
                                source,
                                ref type,
                                options,
                                ref typeKey,
                                false,
                                replaceOldValue);

                            Helpers.DebugAssert(r.SeekToReturn == null);

                            r.SeekToReturn = seekToken;
                            return r;
                        }
                        break;
                    case FieldNewObjectKey:
                        r.NewObjectKey = source.ReadInt32();
                        break;
                    case FieldExistingTypeKey:
                        tmp = source.ReadInt32();
                        type = (Type)source.NetCache.GetKeyedObject(tmp, true);
                        if (type != null)
                            typeKey = source.GetTypeKey(ref type);
                        else
                        {
                            // so we skipped it
                            // have to seek
                            var pos = source.LongPosition;
                            long blockEnd = source.SeekAndExchangeBlockEnd(source.NetCacheKeyPositionsList.GetPosition(tmp));
                            if (!ProtoReader.HasSubValue(WireType.String, source)) throw new ProtoException("New type could not be found on specified position, net key: " + tmp);
                            ReadNewType(source, out type, out typeKey);
                            source.SeekAndExchangeBlockEnd(pos, blockEnd);
                        }
                        r.IsDynamic = true;
                        break;
                    case FieldNewTypeKey:
                        r.NewTypeRefKey = source.ReadInt32();
                        break;
                    case FieldTypeName:
                        ReadNewType(source, out type, out typeKey);
                        r.IsDynamic = true;
                        break;
                    case FieldLateReferenceObject:
                    case FieldObject:
                        hasObject = true;
                        if (fieldNumber == FieldLateReferenceObject) r.IsLateReference = true;
                        r.ShouldRead = true;
                        if (replaceOldValue) value = null; // old value doesn't matter anymore
                        bool wasNull = value == null;
                        bool lateSet = wasNull && ((options & BclHelpers.NetObjectOptions.LateSet) != 0);
                        if (r.NewObjectKey >= 0 && !lateSet)
                        {
                            if (value == null)
                            {
                                source.TrapNextObject(r.NewObjectKey);
                            }
                            else
                            {
                                source.NetCache.SetKeyedObject(r.NewObjectKey, value);
                            }
                            if (r.NewTypeRefKey >= 0) source.NetCache.SetKeyedObject(r.NewTypeRefKey, type);
                        }
                        return r;
                    default:
                        source.SkipField();
                        break;
                }
            } while ((fieldNumber = source.ReadFieldHeader()) > 0);

            if (!hasObject)
            {
                r.IsSkipped = true;
                if (r.NewObjectKey > 0)
                {
                    if (value == null) throw new ProtoException("NetObjectHelpers: oldValue == null but no object is provided, can't track reference, type = " + type);
                    source.NetCache.SetKeyedObject(r.NewObjectKey, value);
                }
            }

            return r;
#endif
        }
#if !FEAT_IKVM
        static void ReadNewType(ProtoReader source, out Type type, out int typeKey)
        {
            string typeName = source.ReadString();
            type = source.DeserializeType(typeName);
            if (type == null)
                throw new ProtoException("Unable to resolve type: " + typeName + " (you can use the TypeModel.DynamicTypeFormatting event to provide a custom mapping)");
            typeKey = source.GetTypeKey(ref type);
        }
#endif
        private const int
            FieldExistingObjectKey = 1,
            FieldNewObjectKey = 2,
            FieldExistingTypeKey = 3,
            FieldNewTypeKey = 4,
            FieldTypeName = 8,
            FieldObject = 10,
            FieldLateReferenceObject = 11,
            FieldSkippedObject = 12;
    }
}