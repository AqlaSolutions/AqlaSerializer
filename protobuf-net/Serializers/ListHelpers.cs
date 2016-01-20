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
    // struct to avoid callvirt
    struct ListHelpers
    {
        readonly WireType _packedWireTypeForRead;
        readonly IProtoSerializer _tail;
        readonly bool _protoCompatibility;
        readonly bool _writePacked;

        public ListHelpers(bool writePacked, WireType packedWireTypeForRead, bool protoCompatibility, IProtoSerializer tail)
        {
            _packedWireTypeForRead = packedWireTypeForRead;
            _tail = tail;
            _protoCompatibility = protoCompatibility;
            _writePacked = writePacked;
        }
#if !FEAT_IKVM
        public void Write(object value, Action prepareInstance, ProtoWriter dest)
        {
            SubItemToken token = new SubItemToken();
            int fieldNumber = dest.FieldNumber;

            bool writePacked = _writePacked;
            if (_protoCompatibility)
            {
                // empty arrays are nulls, no subitem or field

                bool any = WriteContent(
                    value,
                    fieldNumber,
                    dest,
                    first: () =>
                        {
                            if (writePacked)
                                token = ProtoWriter.StartSubItem(null, true, dest);
                            else
                                ProtoWriter.WriteFieldHeaderCancelBegin(dest);
                            prepareInstance?.Invoke();
                        });

                if (!any) // no elements - no field
                    ProtoWriter.WriteFieldHeaderCancelBegin(dest);
                else if (writePacked)
                    // last element - end subitem
                    ProtoWriter.EndSubItem(token, dest);
            }
            else
            {
                // packed or not they will always be in subitem
                // if array is empty the subitem will be empty

                ProtoWriter.StartSubItem(null, writePacked, dest);

                prepareInstance?.Invoke();

                WriteContent(value, 1, dest);
                ProtoWriter.EndSubItem(token, dest);
            }
        }

        bool WriteContent(object value, int fieldNumber, ProtoWriter dest, Action first = null)
        {
            var list = (IList)value;
            if (list == null) return WriteContent((IEnumerable)value, fieldNumber, dest, first);
            int len = list.Count;
            if (len > 0)
            {
                first?.Invoke();
                for (int i = 0; i < len; i++)
                {
                    object obj = list[i];

                    if (_writePacked)
                        ProtoWriter.WriteFieldHeaderBeginIgnored(dest);
                    else
                        ProtoWriter.WriteFieldHeaderBegin(fieldNumber, dest);

                    _tail.Write(obj, dest);
                }
            }
            return len > 0;
        }

        bool WriteContent(IEnumerable enumerable, int fieldNumber, ProtoWriter dest, Action first = null)
        {
            bool isFirst = true;
            foreach (var obj in enumerable)
            {
                if (isFirst)
                {
                    isFirst = false;
                    first?.Invoke();
                }
                if (_writePacked)
                    ProtoWriter.WriteFieldHeaderBeginIgnored(dest);
                else
                    ProtoWriter.WriteFieldHeaderBegin(fieldNumber, dest);

                _tail.Write(obj, dest);
            }
            return !isFirst;
        }

        public void Read(Action prepareInstance, Action<object> add, ProtoReader source)
        {
            bool packed = _packedWireTypeForRead != WireType.None && source.WireType == WireType.String;
            int fieldNumber = source.FieldNumber;

            bool subItemNeeded = packed || !_protoCompatibility;

            SubItemToken token = subItemNeeded ? ProtoReader.StartSubItem(source) : new SubItemToken();

            prepareInstance?.Invoke();

            if (packed)
            {
                while (ProtoReader.HasSubValue(_packedWireTypeForRead, source))
                {
                    add(_tail.Read(null, source));
                }
            }
            else
            {
                if (subItemNeeded) fieldNumber = 1;
                if (!_protoCompatibility || source.TryReadFieldHeader(fieldNumber))
                {
                    do
                    {
                        add(_tail.Read(null, source));
                    } while (source.TryReadFieldHeader(fieldNumber));
                }
            }

            if (subItemNeeded)
                ProtoReader.EndSubItem(token, source);
        }
#endif
    }
}