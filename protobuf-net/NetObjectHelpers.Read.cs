using System;
using System.Diagnostics;

namespace AqlaSerializer
{
    public static partial class NetObjectHelpers
    {
        // this method is split because we need to insert our implementation inside and it's hard to deal with delegates in emit
        
        public static void ReadNetObject_EndWithNoteNewObject(object value, ProtoReader source, object oldValue, Type type, int newObjectKey, int newTypeRefKey, BclHelpers.NetObjectOptions options, SubItemToken token)
        {
            ReadNetObject_NoteNewObject(value, source, oldValue, type, newObjectKey, newTypeRefKey, options);
            ProtoReader.EndSubItem(token, source);
        }

        /// <summary>
        /// Reads an *implementation specific* bundled .NET object, including (as options) type-metadata, identity/re-use, etc.
        /// </summary>
        public static SubItemToken ReadNetObject_Start(ref object value, ProtoReader source, ref Type type, BclHelpers.NetObjectOptions options, out bool isDynamic, ref int typeKey, out int newObjectKey, out int newTypeRefKey, out bool shouldEnd)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            shouldEnd = false;
            
            newObjectKey = -1;
            newTypeRefKey = -1;
            isDynamic = false;
            
            SubItemToken token = ProtoReader.StartSubItem(source);

            int fieldNumber;
            if ((fieldNumber = source.ReadFieldHeader()) == 0)
            {
                // null handling
                value = null;
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
                        newObjectKey = source.ReadInt32();
                        break;
                    case FieldExistingTypeKey:
                        tmp = source.ReadInt32();
                        type = (Type)source.NetCache.GetKeyedObject(tmp);
                        typeKey = source.GetTypeKey(ref type);
                        break;
                    case FieldNewTypeKey:
                        newTypeRefKey = source.ReadInt32();
                        break;
                    case FieldTypeName:
                        string typeName = source.ReadString();
                        type = source.DeserializeType(typeName);
                        if (type == null)
                        {
                            throw new ProtoException("Unable to resolve type: " + typeName + " (you can use the TypeModel.DynamicTypeFormatting event to provide a custom mapping)");
                        }
                        typeKey = source.GetTypeKey(ref type);
                        isDynamic = true;
                        break;
                    case FieldObject:
                        shouldEnd = true;
                        bool wasNull = value == null;
                        bool lateSet = wasNull && ((options & BclHelpers.NetObjectOptions.LateSet) != 0);
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
                            if (newTypeRefKey >= 0) source.NetCache.SetKeyedObject(newTypeRefKey, type);
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
        
        static void ReadNetObject_NoteNewObject(object value, ProtoReader source, object oldValue, Type type, int newObjectKey, int newTypeRefKey, BclHelpers.NetObjectOptions options)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            // late set may become here true e.g. for dynamic string
            bool wasNull = oldValue == null;
            bool lateSet = wasNull && ((options & BclHelpers.NetObjectOptions.LateSet) != 0);
            
            if (newObjectKey >= 0)
            {
                if (wasNull && !lateSet)
                { // this both ensures (via exception) that it *was* set, and makes sure we don't shout
                  // about changed references
                    oldValue = source.NetCache.GetKeyedObject(newObjectKey);

                    if (!ReferenceEquals(oldValue, value))
                    {
                        throw new ProtoException("A reference-tracked object changed reference during deserialization");
                    }
                }
                if (lateSet)
                {
                    source.NetCache.SetKeyedObject(newObjectKey, value, true);
                }
            }
            if (newTypeRefKey >= 0) source.NetCache.SetKeyedObject(newTypeRefKey, type);
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