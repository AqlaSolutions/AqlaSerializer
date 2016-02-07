// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using AltLinq;
using AqlaSerializer.Meta;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;

#endif

namespace AqlaSerializer.Serializers
{
    class ListHelpers
    {
        readonly WireType _packedWireTypeForRead;
        readonly IProtoSerializerWithWireType _tail;
        readonly bool _protoCompatibility;
        readonly bool _writePacked;

        public const int FieldItem = 1;
        public const int FieldSubtype = 2;
        public const int FieldLength = 3;
        
        public ListHelpers(bool writePacked, WireType packedWireTypeForRead, bool protoCompatibility, IProtoSerializerWithWireType tail)
        {
            _packedWireTypeForRead = packedWireTypeForRead;
            _tail = tail;
            _protoCompatibility = protoCompatibility;
            _writePacked = writePacked;
        }
#if !FEAT_IKVM
        public void Write(object value, Action subTypeWriter, int? length, Action prepareInstance, ProtoWriter dest)
        {
            SubItemToken token = new SubItemToken();
            int fieldNumber = dest.FieldNumber;
            
            bool writePacked = _writePacked;
            if (_protoCompatibility)
            {
                // empty arrays are nulls, no subitem or field

                if (writePacked)
                    token = ProtoWriter.StartSubItem(null, true, dest);
                else // each element will begin its own header
                    ProtoWriter.WriteFieldHeaderCancelBegin(dest);

                bool any = WriteContent(
                    value,
                    fieldNumber,
                    _writePacked,
                    dest,
                    first: prepareInstance);

                if (writePacked)
                {
                    // last element - end subitem
                    ProtoWriter.EndSubItem(token, dest);
                }
            }
            else
            {
                bool pack = _tail.DemandWireTypeStabilityStatus();
                token = ProtoWriter.StartSubItem(null, pack, dest);

                if (subTypeWriter != null)
                {
                    ProtoWriter.WriteFieldHeaderBegin(FieldSubtype, dest);
                    subTypeWriter?.Invoke();
                }
                if (length != null && length.Value > 0)
                {
                    ProtoWriter.WriteFieldHeader(FieldLength, WireType.Variant, dest);
                    ProtoWriter.WriteInt32(length.Value, dest);
                }

                prepareInstance?.Invoke();

                WriteContent(value, FieldItem, pack, dest);
                ProtoWriter.EndSubItem(token, dest);
            }
        }

        bool WriteContent(object value, int fieldNumber, bool pack, ProtoWriter dest, Action first = null)
        {
            var list = value as IList;
            if (list == null) return WriteContent((IEnumerable)value, fieldNumber, pack, dest, first);
            int len = list.Count;
            if (len > 0)
            {
                first?.Invoke();
                for (int i = 0; i < len; i++)
                {
                    WriteElement(list[i], fieldNumber, pack, dest, i == 0);
                }
            }
            return len > 0;
        }

        bool WriteContent(IEnumerable enumerable, int fieldNumber, bool pack, ProtoWriter dest, Action first = null)
        {
            bool isFirst = true;
            foreach (var obj in enumerable)
            {
                if (isFirst)
                    first?.Invoke();
                
                WriteElement(obj, fieldNumber, pack, dest, isFirst);

                isFirst = false;
            }
            return !isFirst;
        }

        void WriteElement(object obj, int fieldNumber, bool pack, ProtoWriter dest, bool isFirst)
        {
            if (_protoCompatibility)
            {
                if (!pack)
                    ProtoWriter.WriteFieldHeaderBegin(fieldNumber, dest);
                else
                    ProtoWriter.WriteFieldHeaderBeginIgnored(dest);
            }
            else
            {
                // this can only be supported when wire type of elements will be the same each time and no field cancellations
                // we write only first header to get wire type
                // the difference between standard pack and this
                // is that we write wiretype here at the first time
                if (!pack || isFirst)
                    ProtoWriter.WriteFieldHeaderBegin(fieldNumber, dest);
                else
                    ProtoWriter.WriteFieldHeaderBeginIgnored(dest);
            }

            _tail.Write(obj, dest);
        }

        public delegate void ReadPrepareInstanceDelegate(int? length);

        public void Read(Action subTypeHandler, ReadPrepareInstanceDelegate prepareInstance, Action<object> add, ProtoReader source)
        {
            WireType packedWireType = _packedWireTypeForRead;
            bool packed = (!_protoCompatibility || packedWireType != WireType.None) && source.WireType == WireType.String;
            int fieldNumber = source.FieldNumber;
            
            bool subItemNeeded = packed || !_protoCompatibility;

            SubItemToken token = subItemNeeded ? ProtoReader.StartSubItem(source) : new SubItemToken();

            int? length = null;
            if (!_protoCompatibility)
            {
                bool read;

                do
                {
                    read = false;

                    if (source.TryReadFieldHeader(FieldLength))
                    {
                        // we write length to construct an array before deserializing
                        // so we can handle references to array from inside it

                        length = source.ReadInt32();
                        read = true;
                    }

                    if (source.TryReadFieldHeader(FieldSubtype))
                    {
                        if (subTypeHandler == null)
                            source.SkipField();
                        else
                            subTypeHandler();
                        read = true;
                    }

                } while (read);
            }

            // this is not an error that we don't wait for the first item
            // if we are here it's either not compatible mode (so should call anyway)
            // or there is at least one element
            prepareInstance?.Invoke(length);

            if (_protoCompatibility)
            {
                if (packed)
                {
                    while (ProtoReader.HasSubValue(packedWireType, source))
                        add(_tail.Read(null, source));
                }
                else
                {
                    do
                    {
                        add(_tail.Read(null, source));
                    } while (source.TryReadFieldHeader(fieldNumber));
                }
            }
            else
            {
                if (packed)
                {
                    if (source.TryReadFieldHeader(FieldItem))
                    {
                        packedWireType = source.WireType;
                        do
                        {
                            add(_tail.Read(null, source));
                        } while (ProtoReader.HasSubValue(packedWireType, source));
                    }
                }
                else
                {
                    while (source.TryReadFieldHeader(FieldItem))
                        add(_tail.Read(null, source));
                }
            }

            if (subItemNeeded)
                ProtoReader.EndSubItem(token, source);
        }
#endif
    }
}
#endif