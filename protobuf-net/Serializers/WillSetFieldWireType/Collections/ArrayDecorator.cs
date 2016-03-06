// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
#if FEAT_COMPILER
using TriAxis.RunSharp;
using AqlaSerializer.Compiler;
#endif
using AqlaSerializer.Meta;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

namespace AqlaSerializer.Serializers
{
    sealed class ArrayDecorator : ProtoDecoratorBase, IProtoTypeSerializer
    {   
        // will be always group or string and won't change between group and string in same session
        public bool DemandWireTypeStabilityStatus() => !_protoCompatibility || _writePacked;
#if !FEAT_IKVM
        public override void Write(object value, ProtoWriter dest)
        {
            _listHelpers.Write(value, null, ((IList)value)?.Count, null, dest);
        }

        public override object Read(object value, ProtoReader source)
        {
            Array result = null;
            BasicList list = null;
            int reservedTrap = -1;
            int index=0;
            
            _listHelpers.Read(
                null,
                length =>
                    {
                        if (length >= 0)
                        {
                            // TODO use same instance when length equals, don't forget to NoteObject
                            int oldLen;
                            result = Read_CreateInstance(value, length.Value, -1, out oldLen, source);
                            index = oldLen;
                        }
                        else
                        {
                            reservedTrap = ProtoReader.ReserveNoteObject(source);
                            list = new BasicList();
                        }
                    },
                v =>
                    {
                        if (result != null)
                            result.SetValue(v, index++);
                        else
                            list.Add(v);
                    },
                source);

            if (result == null)
            {
                int oldLen;
                result = Read_CreateInstance(value, list.Count, reservedTrap, out oldLen, source);
                list.CopyTo(result, oldLen);
            }
            return result;
        }

        Array Read_CreateInstance(object value, int appendCount, int reservedTrap, out int oldLen, ProtoReader source)
        {
            oldLen = AppendToCollection ? (((Array)value)?.Length ?? 0) : 0;
            Array result = Array.CreateInstance(_itemType, oldLen + appendCount);
            if (reservedTrap >= 0)
                ProtoReader.NoteReservedTrappedObject(reservedTrap, result, source);
            else
                ProtoReader.NoteObject(result, source);
            if (oldLen != 0) ((Array)value).CopyTo(result, 0);
            return result;
        }
#endif

        readonly ListHelpers _listHelpers;
        readonly bool _writePacked;
        readonly WireType _packedWireTypeForRead;
        readonly Type _arrayType; // this is, for example, typeof(int[])
        readonly bool _overwriteList;
        readonly Type _itemType; // this is, for example, typeof(int[])
        readonly bool _protoCompatibility;


        bool AppendToCollection => !_overwriteList;
        
        public ArrayDecorator(TypeModel model, IProtoSerializerWithWireType tail, bool writePacked, WireType packedWireTypeForRead, Type arrayType, bool overwriteList, bool protoCompatibility)
            : base(tail)
        {
            Helpers.DebugAssert(arrayType != null, "arrayType should be non-null");
            Helpers.DebugAssert(arrayType.IsArray && arrayType.GetArrayRank() == 1, "should be single-dimension array; " + arrayType.FullName);
            _itemType = tail.ExpectedType;
            if (_itemType != arrayType.GetElementType()) throw new ArgumentException("Expected array type is " + arrayType.GetElementType() + " but tail type is " + _itemType);
            Helpers.DebugAssert(Tail.ExpectedType != model.MapType(typeof(byte)), "Should have used BlobSerializer");
            if (!ListDecorator.CanPack(packedWireTypeForRead))
            {
                if (writePacked) throw new InvalidOperationException("Only simple data-types can use packed encoding");
                _packedWireTypeForRead = WireType.None;
            }
            else
                _packedWireTypeForRead = packedWireTypeForRead;
            _writePacked = writePacked;
            _arrayType = arrayType;
            _overwriteList = overwriteList;
            _protoCompatibility = protoCompatibility;
            _listHelpers = new ListHelpers(_writePacked, _packedWireTypeForRead, _protoCompatibility, tail);
        }

        public override Type ExpectedType => _arrayType;
        public override bool RequiresOldValue => AppendToCollection;

#if FEAT_COMPILER
        public override bool EmitReadReturnsValue => true;

        protected override void EmitWrite(AqlaSerializer.Compiler.CompilerContext ctx, AqlaSerializer.Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            using (Compiler.Local value = ctx.GetLocalWithValue(_arrayType, valueFrom))
            {
                _listHelpers.EmitWrite(ctx.G, value, null, () => value.AsOperand.Property("Length"), null);
            }
        }

        protected override void EmitRead(AqlaSerializer.Compiler.CompilerContext ctx, AqlaSerializer.Compiler.Local valueFrom)
        {
            var g = ctx.G;
            using (ctx.StartDebugBlockAuto(this))
            using (Compiler.Local value = ctx.GetLocalWithValueForEmitRead(this, valueFrom))
            using (Compiler.Local result = ctx.Local(_arrayType, true))
            using (Compiler.Local reservedTrap = ctx.Local(typeof(int)))
            using (Compiler.Local list = ctx.Local(ctx.MapType(typeof(List<>)).MakeGenericType(_itemType)))
            using (Compiler.Local index = ctx.Local(typeof(int)))
            using (Compiler.Local oldLen = ctx.Local(typeof(int)))
            {
                g.Assign(reservedTrap, -1);
                _listHelpers.EmitRead(
                    ctx.G,
                    null,
                    length =>
                        {
                            using (ctx.StartDebugBlockAuto(this, "prepareInstance"))
                            {
                                g.If(length >= 0);
                                {
                                    ctx.MarkDebug("// length read, creating instance");
                                    EmitRead_CreateInstance(g, value, length.Property("Value", g.TypeMapper), null, oldLen, result);
                                    g.Assign(index, oldLen);
                                }
                                g.Else();
                                {
                                    ctx.MarkDebug("// length read, creating list");
                                    g.Assign(reservedTrap, g.ReaderFunc.ReserveNoteObject_int());
                                    g.Assign(list, g.ExpressionFactory.New(list.Type));
                                }
                                g.End();
                            }
                        },
                    v =>
                        {
                            using (ctx.StartDebugBlockAuto(this, "add"))
                            {
                                g.If(result.AsOperand != null);
                                {
                                    g.Assign(result.AsOperand[index], v);
                                    g.Increment(index);
                                }
                                g.Else();
                                {
                                    g.Invoke(list, "Add", v);
                                }
                                g.End();
                            }
                        }
                    );

                g.If(result.AsOperand == null);
                {
                    ctx.MarkDebug("// result == null, creating");
                    EmitRead_CreateInstance(g, value, list.AsOperand.Property("Count"), reservedTrap, oldLen, result);
                    g.Invoke(list, "CopyTo", result, oldLen);
                }
                g.End();
                if (EmitReadReturnsValue)
                    ctx.LoadValue(result);
                else
                    g.Assign(value, result);
            }
        }

        void EmitRead_CreateInstance(SerializerCodeGen g, Local value, Operand appendCount, Local reservedTrap, Local outOldLen, Local outResult)
        {
            using (g.ctx.StartDebugBlockAuto(this))
            {
                g.Assign(outOldLen, AppendToCollection ? (value.AsOperand != null).Conditional(value.AsOperand.Property("Length"), 0) : (Operand)0);
                g.Assign(outResult, g.ExpressionFactory.NewArray(_itemType, outOldLen + appendCount));
                if (!reservedTrap.IsNullRef())
                    g.Reader.NoteReservedTrappedObject(reservedTrap, outResult);
                else
                    g.Reader.NoteObject(outResult);

                g.If(outOldLen.AsOperand != 0);
                {
                    g.Invoke(value, "CopyTo", outResult, 0);
                }
                g.End();
            }
        }
#endif

        public bool HasCallbacks(TypeModel.CallbackType callbackType)
        {
            return false;
        }

        public bool CanCreateInstance()
        {
            return true;
        }

#if !FEAT_IKVM
        public object CreateInstance(ProtoReader source)
        {
            Array r = Array.CreateInstance(_itemType, 0);
            ProtoReader.NoteObject(r, source);
            return r;
        }

        public void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {

        }
#endif
#if FEAT_COMPILER
        public void EmitCallback(CompilerContext ctx, Local valueFrom, TypeModel.CallbackType callbackType)
        {

        }

        public void EmitCreateInstance(CompilerContext ctx)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.G.LeaveNextReturnOnStack();
                ctx.G.Eval(ctx.G.ExpressionFactory.NewArray(_itemType, 0));
                ctx.CopyValue();
                ctx.G.Reader.NoteObject(ctx.G.GetStackValueOperand(_arrayType));
            }
        }
#endif

        public override void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            using (builder.SingleTailDecorator(this, _listHelpers.MakeDebugSchemaDescription(AppendToCollection)))
                Tail.WriteDebugSchema(builder);
        }

    }
}
#endif