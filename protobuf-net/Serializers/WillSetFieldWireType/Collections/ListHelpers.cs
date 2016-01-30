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
        readonly IProtoSerializer _tail;
        readonly bool _protoCompatibility;
        readonly bool _writePacked;

        public const int FieldItem = 1;
        public const int FieldSubtype = 2;
        public const int FieldLength = 3;
        public const int FieldPackType = 4; // TODO write first field header always and pack everything!!!

        public ListHelpers(bool writePacked, WireType packedWireTypeForRead, bool protoCompatibility, IProtoSerializer tail)
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

            if (!_protoCompatibility)
            {
                Action additional =
                    () =>
                        {
                            if (subTypeWriter != null)
                            {
                                ProtoWriter.WriteFieldHeaderBegin(FieldSubtype, dest);
                            }
                            if (length != null && length.Value > 0)
                            {
                                ProtoWriter.WriteFieldHeader(FieldLength, WireType.Variant, dest);
                                ProtoWriter.WriteInt32(length.Value, dest);
                            }
                        };
                prepareInstance = additional + prepareInstance;
            }

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
                // packed or not they will always be in subitem
                // if array is empty the subitem will be empty

                token = ProtoWriter.StartSubItem(null, writePacked, dest);

                prepareInstance?.Invoke();

                WriteContent(value, _writePacked ? fieldNumber : FieldItem, dest);
                ProtoWriter.EndSubItem(token, dest);
            }
        }

        bool WriteContent(object value, int fieldNumber, ProtoWriter dest, Action first = null)
        {
            var list = value as IList;
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

        public delegate void ReadPrepareInstanceDelegate(int? length);

        public void Read(Action subTypeHandler, ReadPrepareInstanceDelegate prepareInstance, Action<object> add, ProtoReader source)
        {
            bool packed = _packedWireTypeForRead != WireType.None && source.WireType == WireType.String;
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

            if (packed)
            {
                while (ProtoReader.HasSubValue(_packedWireTypeForRead, source))
                {
                    add(_tail.Read(null, source));
                }
            }
            else
            {
                if (subItemNeeded) fieldNumber = FieldItem;
                if (_protoCompatibility || source.TryReadFieldHeader(fieldNumber))
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
#endif