// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using AltLinq;
using AqlaSerializer.Meta;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;

#endif
#if FEAT_COMPILER
using TriAxis.RunSharp;
using AqlaSerializer.Compiler;
#endif

namespace AqlaSerializer.Serializers
{
    class ListHelpers
    {
        readonly WireType _packedWireTypeForRead = WireType.None;
        readonly IProtoSerializerWithWireType _tail;
        readonly bool _protoCompatibility;
        readonly bool _writePacked;
        readonly Type _itemType;
        public const int FieldItem = 1;
        public const int FieldSubtype = 2;
        public const int FieldLength = 3;
        
        public ListHelpers(bool writePacked, WireType packedWireTypeForRead, bool protoCompatibility, IProtoSerializerWithWireType tail)
        {
            if (protoCompatibility)
            {
                if (ListDecorator.CanPack(packedWireTypeForRead))
                    _packedWireTypeForRead = packedWireTypeForRead;
                else if (writePacked)
                    throw new ArgumentException("For writePacked wire type for read should be specified");
            }
            _tail = tail;
            _itemType = tail.ExpectedType;
            _protoCompatibility = protoCompatibility;
            _writePacked = writePacked;
        }
#if FEAT_COMPILER
        public void EmitWrite(SerializerCodeGen g, Local value, Action subTypeWriter, Func<Operand> getLength, Action prepareInstance)
        {
            using (g.ctx.StartDebugBlockAuto(this))
            {
                using (var token = g.ctx.Local(typeof(SubItemToken)))
                using (var length = getLength != null ? g.ctx.Local(typeof(int)) : null)
                using (var fieldNumber = _protoCompatibility ? g.ctx.Local(typeof(int)) : null)
                {
                    if (!fieldNumber.IsNullRef())
                        g.Assign(fieldNumber, g.ReaderFunc.FieldNumber());
                    
                    bool writePacked = _writePacked;
                    if (_protoCompatibility)
                    {
                        // empty arrays are nulls, no subitem or field

                        if (writePacked)
                            g.Assign(token, g.WriterFunc.StartSubItem(null, true));
                        else // each element will begin its own header
                            g.Writer.WriteFieldHeaderCancelBegin();

                        EmitWriteContent(
                            g,
                            value,
                            fieldNumber.AsOperand,
                            _writePacked,
                            first: prepareInstance);

                        if (writePacked)
                        {
                            // last element - end subitem
                            g.Writer.EndSubItem(token);
                        }
                    }
                    else
                    {
                        bool pack = _tail.DemandWireTypeStabilityStatus();
                        g.Assign(token, g.WriterFunc.StartSubItem(null, pack));

                        if (subTypeWriter != null)
                        {
                            g.Writer.WriteFieldHeaderBegin(FieldSubtype);
                            subTypeWriter?.Invoke();
                        }
                        if (!getLength.IsNullRef())
                        {
                            g.Assign(length, getLength());
                            g.If(length.AsOperand > 0);
                            {
                                g.Writer.WriteFieldHeader(FieldLength, WireType.Variant);
                                g.Writer.WriteInt32(length);
                            }
                            g.End();
                        }

                        prepareInstance?.Invoke();

                        EmitWriteContent(g, value, FieldItem, pack);
                        g.Writer.EndSubItem(token);
                    }
                }
            }
        }
        
        void EmitWriteContent(SerializerCodeGen g, Local enumerable, Operand fieldNumber, bool pack, Action first = null)
        {
            Type listType = g.ctx.MapType(typeof(IList<>)).MakeGenericType(_itemType);
            if (!enumerable.Type.IsArray && !Helpers.IsAssignableFrom(listType, enumerable.Type))
            {
                EmitWriteContentEnumerable(g, enumerable, fieldNumber, pack, first);
                return;
            }
            using (var list = g.ctx.Local(listType))
            using (var len = g.ctx.Local(typeof(int)))
            using (var i = g.ctx.Local(typeof(int)))
            {
                g.Assign(list, enumerable.AsOperand.Cast(listType));
                g.Assign(len, list.AsOperand.Property("Count"));
                g.If(len.AsOperand>0);
                {
                    first?.Invoke();

                    g.For(i.AsOperand.Assign(0), i.AsOperand < len.AsOperand, i.AsOperand.Increment());
                    {
                        EmitWriteElement(g, list.AsOperand[i], fieldNumber, pack, i.AsOperand == 0);
                    }
                    g.End();
                }
                g.End();
                if (enumerable.Type.IsValueType)
                    g.Assign(enumerable, list);
            }
        }

        void EmitWriteContentEnumerable(SerializerCodeGen g, Local enumerable, Operand fieldNumber, bool pack, Action first = null)
        {
            Type enumerableGenericType = g.ctx.MapType(typeof(IEnumerable<>)).MakeGenericType(_itemType);

            bool castNeeded = !Helpers.IsAssignableFrom(enumerableGenericType, enumerable.Type);

            using (var isFirst = g.ctx.Local(typeof(bool)))
            using (var obj = g.ctx.Local(castNeeded ? g.ctx.MapType(typeof(object)) : _itemType))
            {
                g.Assign(isFirst, true);

                var el = g.ForEach(castNeeded ? g.ctx.MapType(typeof(object)) : _itemType, enumerable);
                {
                    g.If(isFirst);
                    {
                        first?.Invoke();
                    }
                    g.End();

                    EmitWriteElement(g, castNeeded ? el.Cast(_itemType) : el, fieldNumber, pack, isFirst);

                    g.Assign(isFirst, false);
                    
                }
                g.End();
            }
        }

        void EmitWriteElement(SerializerCodeGen g, Operand obj, Operand fieldNumber, bool pack, Operand isFirst)
        {
            if (_protoCompatibility)
            {
                if (!pack)
                    g.Writer.WriteFieldHeaderBegin(fieldNumber);
                else
                    g.Writer.WriteFieldHeaderBeginIgnored();
            }
            else
            {
                // this can only be supported when wire type of elements will be the same each time and no field cancellations
                // we write only first header to get wire type
                // the difference between standard pack and this
                // is that we write wiretype here at the first time
                if (pack) g.If(isFirst);
                g.Writer.WriteFieldHeaderBegin(fieldNumber);
                if (pack)
                {
                    g.Else();
                    g.Writer.WriteFieldHeaderBeginIgnored();
                    g.End();
                }
            }
            g.LeaveNextReturnOnStack();
            g.Eval(obj);
            _tail.EmitWrite(g.ctx, null);
        }

        public delegate void EmitReadPrepareInstanceDelegate(Operand length);

        public void EmitRead(SerializerCodeGen g, Action subTypeHandler, EmitReadPrepareInstanceDelegate prepareInstance, Action<Local> add)
        {
            using (g.ctx.StartDebugBlockAuto(this))
            {
                WireType packedWireType = _packedWireTypeForRead;
                bool packedAllowedStatic = (!_protoCompatibility || packedWireType != WireType.None);
                using (var packed = packedAllowedStatic ? g.ctx.Local(typeof(bool)) : null)
                using (var token = g.ctx.Local(typeof(SubItemToken)))
                using (var length = g.ctx.Local(typeof(int?), true))
                using (var fieldNumber = _protoCompatibility ? g.ctx.Local(typeof(int)):null)
                using (var read = g.ctx.Local(typeof(bool)))
                {
                    if (!fieldNumber.IsNullRef())
                        g.Assign(fieldNumber, g.ReaderFunc.FieldNumber());
                    if (packedAllowedStatic)
                        g.Assign(packed, g.ReaderFunc.WireType() == WireType.String);

                    bool subItemNeededStatic = !_protoCompatibility;

                    if (subItemNeededStatic || packedAllowedStatic)
                    {
                        if (!subItemNeededStatic) g.If(packed);
                        g.Assign(token, g.ReaderFunc.StartSubItem());
                        if (!subItemNeededStatic) g.End();
                    }

                    if (!_protoCompatibility)
                    {
                        g.DoWhile();
                        {
                            g.Assign(read, false);

                            g.If(g.ReaderFunc.TryReadFieldHeader_bool(FieldLength));
                            {
                                // we write length to construct an array before deserializing
                                // so we can handle references to array from inside it

                                g.Assign(length, g.ReaderFunc.ReadInt32());
                                g.Assign(read, true);
                            }
                            g.End();

                            g.If(g.ReaderFunc.TryReadFieldHeader_bool(FieldSubtype));
                            {
                                if (subTypeHandler == null)
                                    g.Reader.SkipField();
                                else
                                    subTypeHandler(); // TODO multiple times?
                                g.Assign(read, true);
                            }
                            g.End();
                        }
                        g.EndDoWhile(read);
                    }

                    // this is not an error that we don't wait for the first item
                    // if we are here it's either not compatible mode (so should call anyway)
                    // or there is at least one element
                    prepareInstance?.Invoke(length);

                    g.ctx.MarkDebug("ProtoCompatibility: " + _protoCompatibility);
                    if (_protoCompatibility)
                    {
                        if (packedAllowedStatic)
                        {
                            g.If(packed);
                            {
                                g.While(g.ReaderFunc.HasSubValue_bool(packedWireType));
                                {
                                    EmitReadElementContent(g, add);
                                }
                                g.End();
                            }
                            g.Else();
                        }

                        g.DoWhile();
                        {
                            EmitReadElementContent(g, add);
                        }
                        g.EndDoWhile(g.ReaderFunc.TryReadFieldHeader_bool(fieldNumber));
                        
                        if (packedAllowedStatic) g.End();
                    }
                    else
                    {
                        g.If(packed);
                        {
                            g.If(g.ReaderFunc.TryReadFieldHeader_bool(FieldItem));
                            {
                                using (var packedWireTypeDynamic = g.ctx.Local(typeof(WireType)))
                                {
                                    g.Assign(packedWireTypeDynamic, g.ReaderFunc.WireType());
                                    g.DoWhile();
                                    {
                                        EmitReadElementContent(g, add);
                                    }
                                    g.EndDoWhile(g.ReaderFunc.HasSubValue_bool(packedWireTypeDynamic));
                                }
                            }
                            g.End();
                        }
                        g.Else();
                        {
                            g.While(g.ReaderFunc.TryReadFieldHeader_bool(FieldItem));
                            {
                                EmitReadElementContent(g, add);
                            }
                            g.End();
                        }
                        g.End();
                    }

                    if (subItemNeededStatic || packedAllowedStatic)
                    {
                        if (!subItemNeededStatic) g.If(packed);
                        g.Reader.EndSubItem(token);
                        if (!subItemNeededStatic) g.End();
                    }
                }
            }
        }

        void EmitReadElementContent(SerializerCodeGen g, Action<Local> add)
        {
            using (g.ctx.StartDebugBlockAuto(this))
            {
                using (var loc = g.ctx.Local(_tail.ExpectedType, true))
                {
                    _tail.EmitRead(g.ctx, loc);
                    if (_tail.EmitReadReturnsValue)
                        g.ctx.StoreValue(loc);
                    add(loc);
                }
            }
        }
#endif
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

                WriteContent(
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

        void WriteContent(object value, int fieldNumber, bool pack, ProtoWriter dest, Action first = null)
        {
            var list = value as IList;
            if (list == null)
            {
                WriteContent((IEnumerable)value, fieldNumber, pack, dest, first);
                return;
            }
            int len = list.Count;
            if (len > 0)
            {
                first?.Invoke();
                for (int i = 0; i < len; i++)
                {
                    WriteElement(list[i], fieldNumber, pack, dest, i == 0);
                }
            }
        }

        void WriteContent(IEnumerable enumerable, int fieldNumber, bool pack, ProtoWriter dest, Action first = null)
        {
            bool isFirst = true;
            foreach (var obj in enumerable)
            {
                if (isFirst)
                    first?.Invoke();
                
                WriteElement(obj, fieldNumber, pack, dest, isFirst);

                isFirst = false;
            }
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
                        ReadElementContent(add, source);
                }
                else
                {
                    do
                    {
                        ReadElementContent(add, source);
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
                            ReadElementContent(add, source);
                        } while (ProtoReader.HasSubValue(packedWireType, source));
                    }
                }
                else
                {
                    while (source.TryReadFieldHeader(FieldItem))
                        ReadElementContent(add, source);
                }
            }

            if (subItemNeeded)
                ProtoReader.EndSubItem(token, source);
        }

        void ReadElementContent(Action<object> add, ProtoReader source)
        {
            add(_tail.Read(null, source));
        }
#endif
        
        public string MakeDebugSchemaDescription(bool append)
        {
            string desc = "";
            if (_protoCompatibility) desc += "Compatibility";
            if (_writePacked)
            {
                if (desc.Length != 0)
                    desc += ",";
                desc += "WritePacked";
            }
            if (append)
            {
                if (desc.Length != 0)
                    desc += ", ";
                desc += "Append";
            }
            if (_packedWireTypeForRead != WireType.None)
            {
                if (desc.Length != 0)
                    desc += ", ";
                desc += "PackedRead = " + _packedWireTypeForRead;
            }
            return desc;
        }

    }
}
#endif