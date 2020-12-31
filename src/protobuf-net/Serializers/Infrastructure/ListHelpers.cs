// Modified by Vladyslav Taranov for AqlaSerializer, 2016

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using AltLinq; using System.Linq;
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
        public const int FieldItem = 1;
        public const int FieldSubtype = 2;
        public const int FieldLength = 3;
        public const int FieldContent = 4;
#if !NO_RUNTIME
        readonly WireType _expectedTailWireType = WireType.None;
        readonly IProtoSerializerWithWireType _tail;
        readonly bool _protoCompatibility;
        readonly bool _writeProtoPacked;
        readonly Type _itemType;
        readonly bool _skipIList;
        readonly bool _canUsePackedPrefix;
        
        public ListHelpers(bool writeProtoPacked, WireType expectedTailWireType, bool protoCompatibility, IProtoSerializerWithWireType tail, bool skipIList)
        {
            if (protoCompatibility)
            {
                if (ListDecorator.CanPackProtoCompatible(expectedTailWireType))
                    _expectedTailWireType = expectedTailWireType;
                else
                {
                    expectedTailWireType = WireType.None;
                    if (writeProtoPacked)
                        throw new ArgumentException("For writePacked wire type for read should be specified");
                }

                _writeProtoPacked = writeProtoPacked;
            }
            else _expectedTailWireType = expectedTailWireType;
            _tail = tail;
            _skipIList = skipIList;
            _itemType = tail.ExpectedType;
            _protoCompatibility = protoCompatibility;
            _canUsePackedPrefix = (!_protoCompatibility || writeProtoPacked) && CanUsePackedPrefix(expectedTailWireType, _itemType);
        }
        
        public bool CanCancelWriting => _protoCompatibility && !_writeProtoPacked;

        internal static bool CanUsePackedPrefix(WireType packedWireType, Type itemType)
        {
            // needs to be a suitably simple type *and* be definitely not nullable
            switch (packedWireType)
            {
                case WireType.Fixed32:
                case WireType.Fixed64:
                    break;
                default:
                    return false; // nope
            }
            if (!itemType.IsValueType) return false;
            return Helpers.GetNullableUnderlyingType(itemType) == null;
        }

#if FEAT_COMPILER
        public void EmitWrite(SerializerCodeGen g, Local value, Func<ContextualOperand> countNullable, Action protoIncompatibleMetaWriter, Action prepareInstance)
        {
            using (g.ctx.StartDebugBlockAuto(this))
            {

                bool wtStability = _protoCompatibility || _tail.DemandWireTypeStabilityStatus();

                // note for debugging:
                // often mistake is in non-zeroing locals
                // try to zero them one by one and check

                using (var innerToken = g.ctx.Local(typeof(SubItemToken?), true))
                using (var fieldNumber = g.ctx.Local(typeof(int)))
                using (var expectedLength = wtStability && _canUsePackedPrefix && countNullable != null ? g.ctx.Local(typeof(ulong?)) : null)
                using (var startPos = g.ctx.Local(typeof(long)))
                {
                    g.Assign(fieldNumber, g.ReaderFunc.FieldNumber());

                    if (!expectedLength.IsNullRef())
                    {
                        g.If(countNullable() != null);
                        g.Assign(expectedLength, g.WriterFunc.MakePackedPrefix_ulong_nullable(countNullable().ValueFromNullable(), _expectedTailWireType));
                        g.Else();
                        g.Assign(expectedLength, null);
                        g.End();
                    }

                    void WritePackedPrefix()
                    {
                        g.Writer.WriteFieldHeaderComplete(WireType.String);
                        g.Writer.WriteLengthPrefix(expectedLength.AsOperand.ValueFromNullable());
                    }

                    if (_protoCompatibility)
                    {
                        // empty arrays are nulls, no subitem or field

                        if (_writeProtoPacked)
                        {
                            if (!expectedLength.IsNullRef())
                            {
                                g.If(expectedLength.AsOperand != null);
                                WritePackedPrefix();
                                g.Else();
                                g.Assign(innerToken, g.WriterFunc.StartSubItem(null, true));
                                g.End();
                            }
                            else g.Assign(innerToken, g.WriterFunc.StartSubItem(null, true));
                        }
                        else  // each element will begin its own header
                            g.Writer.WriteFieldHeaderCancelBegin();

                        g.Assign(startPos, g.WriterFunc.GetLongPosition());

                        EmitWriteContent(
                            g,
                            value,
                            fieldNumber.AsOperand,
                            _writeProtoPacked,
                            first: prepareInstance);
                        ContentEnd();
                    }
                    else using (var outerToken = g.ctx.Local(typeof(SubItemToken?), true))
                    {
                        bool pack = wtStability;
                        //if (!pack) expectedLength = null; -- done at start

                        if (!expectedLength.IsNullRef())
                        {
                            g.If(expectedLength.AsOperand != null);
                            {
                                g.If(countNullable() > 0);
                                g.Increment(expectedLength);
                                g.End();
                                if (protoIncompatibleMetaWriter != null)
                                {
                                    // start in outer Group (reader - check String/Group)
                                    // this is needed because we predicted content length but can't predict length of metadata
                                    g.Assign(outerToken, g.WriterFunc.StartSubItem(null, false));
                                    protoIncompatibleMetaWriter.Invoke();
                                    g.If(countNullable() != 0);
                                    {
                                        g.Writer.WriteFieldHeaderBegin(FieldContent);
                                        WritePackedPrefix();
                                    }
                                    g.End();
                                }
                                else WritePackedPrefix();
                            }
                            g.Else();
                            NoExpectedLength();
                            g.End();
                        }
                        else NoExpectedLength();

                        void NoExpectedLength()
                        {
                            g.Assign(innerToken, g.WriterFunc.StartSubItem(null, pack));
                            protoIncompatibleMetaWriter?.Invoke();
                        }

                        prepareInstance?.Invoke();

                        g.Assign(startPos, g.WriterFunc.GetLongPosition());
                        if (countNullable != null) g.If(countNullable() != 0);
                        EmitWriteContent(g, value, FieldItem, pack);
                        if (countNullable != null) g.End();

                        ContentEnd();

                        g.If(outerToken.AsOperand != null);
                        g.Writer.EndSubItem(outerToken.AsOperand.ValueFromNullable());
                        g.End();
                    }
                    
                    void ContentEnd()
                    {
                        if (!expectedLength.IsNullRef())
                        {
                            g.If(expectedLength.AsOperand != null && g.WriterFunc.GetLongPosition().Cast(g.ctx.MapType(typeof(ulong)))
                                != (startPos.AsOperand.Cast(g.ctx.MapType(typeof(ulong))) + expectedLength.AsOperand.ValueFromNullable()));
                            {
                                g.ThrowProtoException(
                                    $"Written packed prefix for wiretype {_expectedTailWireType} and " + countNullable().ValueFromNullable()
                                    + $" elements, expected length was " + expectedLength.AsOperand.ValueFromNullable() + " but actual is "
                                    + (g.WriterFunc.GetLongPosition().Cast(g.ctx.MapType(typeof(ulong))) - startPos.AsOperand.Cast(g.ctx.MapType(typeof(ulong)))));
                            }
                            g.End();
                        }

                        g.If(innerToken.AsOperand != null);
                        g.Writer.EndSubItem(innerToken.AsOperand.ValueFromNullable());
                        g.End();
                    }
                }
            }
        }
        
        void EmitWriteContent(SerializerCodeGen g, Local enumerable, Operand fieldNumber, bool pack, Action first = null)
        {
            Type listType = g.ctx.MapType(typeof(IList<>)).MakeGenericType(_itemType);
            if (_skipIList || (!enumerable.Type.IsArray && !Helpers.IsAssignableFrom(listType, enumerable.Type)))
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

        public delegate void EmitReadMetaDelegate(Operand fieldNumber, Action onSuccess, Action onFail);

        public void EmitRead(SerializerCodeGen g, EmitReadMetaDelegate readNextMeta, Action prepareInstance, Action<Local> add)
        {
            if (readNextMeta == null) readNextMeta = (n, s, f) => f();
            var ctx = g.ctx;
            using (g.ctx.StartDebugBlockAuto(this))
            {
                bool packedAllowedStatic = (!_protoCompatibility || _expectedTailWireType != WireType.None);
                using (var packed = packedAllowedStatic ? g.ctx.Local(typeof(bool)) : null)
                using (var outerToken = g.ctx.Local(typeof(SubItemToken)))
                using (var innerToken = g.ctx.Local(typeof(SubItemToken?)))
                using (var fieldNumber = g.ctx.Local(typeof(int)))
                {
                    if (!fieldNumber.IsNullRef())
                        g.Assign(fieldNumber, g.ReaderFunc.FieldNumber());
                    if (packedAllowedStatic)
                        g.Assign(packed, g.ReaderFunc.WireType() == WireType.String);

                    bool subItemNeededStatic = !_protoCompatibility;

                    if (subItemNeededStatic || packedAllowedStatic)
                    {
                        if (!subItemNeededStatic) g.If(packed);
                        g.Assign(outerToken, g.ReaderFunc.StartSubItem());
                        if (!subItemNeededStatic) g.End();
                    }
                    
                    g.ctx.MarkDebug("ProtoCompatibility: " + _protoCompatibility);
                    if (_protoCompatibility)
                    {
                        // this is not an error that we don't wait for the first item
                        // if field is present there is at least one element

                        prepareInstance?.Invoke();

                        if (packedAllowedStatic)
                        {
                            g.If(packed);
                            {
                                g.While(g.ReaderFunc.HasSubValue_bool(_expectedTailWireType));
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
                        var onEmptyLabel = g.DefineLabel();
                        var loopStart = g.DefineLabel();
                        var loopEnd = g.DefineLabel();
                        g.MarkLabel(loopStart);
                        {
                            g.Assign(fieldNumber, g.ReaderFunc.ReadFieldHeader_int());
                            readNextMeta(
                                fieldNumber,
                                () => {
                                    using (g.ctx.StartDebugBlockAuto(this, "readNextMeta.OnSuccess"))
                                    {
                                        g.Goto(loopStart);
                                    }
                                },
                                () => {
                                    using (g.ctx.StartDebugBlockAuto(this, "readNextMeta.OnFail"))
                                    {
                                        g.If(fieldNumber.AsOperand == FieldItem || fieldNumber.AsOperand == FieldContent);
                                        g.Goto(loopEnd);
                                        g.End();

                                        g.If(fieldNumber.AsOperand == 0);
                                        prepareInstance?.Invoke();
                                        g.Goto(onEmptyLabel);
                                        g.End();

                                        g.Reader.SkipField();
                                        g.Goto(loopStart);
                                    }
                                });
                        }
                        g.MarkLabel(loopEnd);

                        prepareInstance?.Invoke();
                        
                        g.If(fieldNumber.AsOperand == FieldContent);
                        {
                            g.Assign(innerToken, g.ReaderFunc.StartSubItem());
                            g.Assign(packed, g.ReaderFunc.WireType() == WireType.String);
                            g.Assign(fieldNumber, g.ReaderFunc.ReadFieldHeader_int());
                        }
                        g.Else();
                        g.Assign(innerToken, null);
                        g.End();

                        g.If(fieldNumber.AsOperand == FieldItem);
                        {
                            g.If(packed);
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
                            g.Else();
                            {
                                g.DoWhile();
                                {
                                    EmitReadElementContent(g, add);
                                }
                                g.EndDoWhile(g.ReaderFunc.TryReadFieldHeader_bool(FieldItem));
                            }
                            g.End();
                        }
                        g.End();
                        
                        g.If(innerToken.AsOperand != null);
                        g.Reader.EndSubItem(innerToken.AsOperand.ValueFromNullable());
                        g.End();
                        
                        g.MarkLabel(onEmptyLabel);
                    }

                    if (subItemNeededStatic || packedAllowedStatic)
                    {
                        if (!subItemNeededStatic) g.If(packed);
                        g.Reader.EndSubItem(outerToken);
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
        public void Write(object value, int? count, Action metaWriter, ProtoWriter dest)
        {
            SubItemToken? innerToken = null;
            int fieldNumber = dest.FieldNumber;

            // early length write
            ulong? expectedLength = _canUsePackedPrefix && count != null ? ProtoWriter.MakePackedPrefix(count.Value, _expectedTailWireType) : (ulong?)null;
            long startPos;

            void WritePackedPrefix()
            {
                ProtoWriter.WriteFieldHeaderComplete(WireType.String, dest);
                dest.WriteLengthPrefix(expectedLength.Value);
            }


            if (_protoCompatibility)
            {
                // empty arrays are nulls, no subitem or field
                
                if (_writeProtoPacked)
                {
                    if (expectedLength != null)
                        WritePackedPrefix();
                    else 
                        innerToken = ProtoWriter.StartSubItem(null, true, dest);
                }
                else // each element will begin its own header
                    ProtoWriter.WriteFieldHeaderCancelBegin(dest);

                startPos = ProtoWriter.GetLongPosition(dest);
                WriteContent(value, fieldNumber, _writeProtoPacked, dest);
                ContentEnd();
            }
            else
            {
                bool pack = _tail.DemandWireTypeStabilityStatus();
                if (!pack) expectedLength = null;

                SubItemToken? outerToken = null;
                
                if (expectedLength != null)
                {
                    if (count > 0) expectedLength += 1; // first element writes field header, it should be 1 byte
                    if (metaWriter != null)
                    {
                        // start in outer Group (reader - check String/Group)
                        // this is needed because we predicted content length but can't predict length of metadata
                        outerToken = ProtoWriter.StartSubItem(null, false, dest);
                        metaWriter.Invoke();
                        if (count != 0)
                        {
                            ProtoWriter.WriteFieldHeaderBegin(FieldContent, dest);
                            WritePackedPrefix();
                        }
                    }
                    else WritePackedPrefix();
                }
                else
                {
                    innerToken = ProtoWriter.StartSubItem(null, pack, dest);
                    metaWriter?.Invoke();
                }

                startPos = ProtoWriter.GetLongPosition(dest);
                if (count != 0)
                    WriteContent(value, FieldItem, pack, dest);

                ContentEnd();
                if (outerToken != null) ProtoWriter.EndSubItem(outerToken.Value, dest);
            }

            void ContentEnd()
            {
                if (expectedLength != null && (ulong)ProtoWriter.GetLongPosition(dest) != (ulong)startPos + expectedLength.Value)
                {
                    throw new ProtoException(
                        $"Written packed prefix for wiretype {_expectedTailWireType} and {count.Value} elements, expected length was {expectedLength.Value} but actual is {(ulong)ProtoWriter.GetLongPosition(dest) - (ulong)startPos}");
                }
                if (innerToken != null) ProtoWriter.EndSubItem(innerToken.Value, dest);
            }
        }

        void WriteContent(object value, int fieldNumber, bool pack, ProtoWriter dest)
        {
            var list = !_skipIList ? value as IList : null;
            if (list == null)
            {
                WriteContent((IEnumerable)value, fieldNumber, pack, dest);
                return;
            }
            int len = list.Count;
            if (len > 0)
            {
                for (int i = 0; i < len; i++)
                {
                    WriteElement(list[i], fieldNumber, pack, dest, i == 0);
                }
            }
        }

        void WriteContent(IEnumerable enumerable, int fieldNumber, bool pack, ProtoWriter dest)
        {
            bool isFirst = true;
            foreach (var obj in enumerable)
            {
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

        public delegate bool TryReadMetaDelegate();
        

        public void Read(TryReadMetaDelegate readNextMeta, Action prepareInstance, Action<object> add, ProtoReader source)
        {
            WireType packedWireType = _expectedTailWireType;
            bool packed = (!_protoCompatibility || packedWireType != WireType.None) && source.WireType == WireType.String;
            int fieldNumber = source.FieldNumber;
            
            bool subItemNeeded = packed || !_protoCompatibility;
            SubItemToken token = subItemNeeded ? ProtoReader.StartSubItem(source) : new SubItemToken();
            
            if (_protoCompatibility)
            {
                // this is not an error that we don't wait for the first item
                // if field is present there is at least one element

                prepareInstance?.Invoke();

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
                bool loop;
                bool empty = false;
                int field;
                do
                {
                    loop = false;
                    field = source.ReadFieldHeader();
                    if (field != 0 && (readNextMeta?.Invoke() ?? false))
                    {
                        loop = true;
                    }
                    else
                    {
                        switch (field)
                        {
                            case FieldItem: // can be packed or not
                            case FieldContent: // length-prefixed subgroup with items
                                break;
                            default:
                                if (field == 0)
                                {
                                    empty = true;
                                    break;
                                }
                                
                                source.SkipField();
                                loop = true;
                                break;
                        }
                    }
                } while (loop);

                prepareInstance?.Invoke();

                if (!empty)
                {
                    SubItemToken? innerToken = null;

                    if (field == FieldContent)
                    {
                        innerToken = ProtoReader.StartSubItem(source);
                        packed = source.WireType == WireType.String;
                        field = source.ReadFieldHeader();
                    }

                    if (field == FieldItem)
                    {
                        if (packed)
                        {
                            packedWireType = source.WireType;
                            do
                            {
                                ReadElementContent(add, source);
                            } while (ProtoReader.HasSubValue(packedWireType, source));
                        }
                        else
                        {
                            do
                            {
                                ReadElementContent(add, source);
                            } while (source.TryReadFieldHeader(FieldItem));
                        }
                    }

                    if (innerToken != null) ProtoReader.EndSubItem(innerToken.Value, source);
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
            if (_protoCompatibility)
                desc += "Compatibility";
            else if (_tail.DemandWireTypeStabilityStatus())
                desc += "NewPacked";

            if (_writeProtoPacked)
            {
                if (desc.Length != 0)
                    desc += ", ";
                desc += "WritePacked";
            }
            if (_expectedTailWireType != WireType.None && _protoCompatibility)
            {
                if (desc.Length != 0)
                    desc += ", ";
                desc += "PackedRead = " + _expectedTailWireType;
            }
            if (append)
            {
                if (desc.Length != 0)
                    desc += ", ";
                desc += "Append";
            }
            return desc;
        }
#endif
    }
}
