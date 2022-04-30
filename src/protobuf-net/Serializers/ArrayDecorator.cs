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
        readonly int _readLengthLimit;

        public override bool CanCancelWriting => _listHelpers.CanCancelWriting;

        // will be always group or string and won't change between group and string in same session
        public bool DemandWireTypeStabilityStatus() => !_protoCompatibility || _writeProtoPacked;
#if !FEAT_IKVM
        public override void Write(object value, ProtoWriter dest)
        {
            _listHelpers.Write(value,
                               ((Array)value).Length,
                               () =>
                                   {
                                       int length = ((Array)value).Length;
                                       if (length > 0)
                                       {
                                           ProtoWriter.WriteFieldHeader(ListHelpers.FieldLength, WireType.Variant, dest);
                                           ProtoWriter.WriteInt32(length, dest);
                                       }
                                   }, dest);
        }

        public static void ThrowExceededLengthLimit(int length, int limit)
        {
            throw new ProtoException(
                                    "Total array length " + length + " exceeded the limit " + limit + ", " +
                                    "set MetaType.ArrayLengthReadLimit");
        }

        public override object Read(object value, ProtoReader source)
        {
            Array result = null;
            List<object> list = null;
            int reservedTrap = -1;
            int index = 0;
            int? length = null;
            _listHelpers.Read(
                () =>
                    {
                        if (source.FieldNumber == ListHelpers.FieldLength)
                        {
                            // we write length to construct an array before deserializing
                            // so we can handle references to array from inside it

                            length = source.ReadInt32();
                            return true;
                        }
                        return false;
                    },
                () =>
                    {
                        if (length != null)
                        {
                            if (length.Value > _readLengthLimit)
                                ThrowExceededLengthLimit(length.Value, _readLengthLimit);

                            // TODO use same instance when length equals, don't forget to NoteObject
                            int oldLen;
                            result = Read_CreateInstance(value, length.Value, -1, out oldLen, source);
                            index = oldLen;
                        }
                        else
                        {
                            reservedTrap = ProtoReader.ReserveNoteObject(source);
                            list = new List<object>();
                        if (AppendToCollection && value != null)
                            {
                                foreach (var el in (Array)value)
                                    list.Add(el);
                            }
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
                ((IList)list).CopyTo(result, oldLen);
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
        readonly bool _writeProtoPacked;
        readonly Type _arrayType; // this is, for example, typeof(int[])
        readonly bool _overwriteList;
        readonly Type _itemType; // this is, for example, typeof(int[])
        readonly bool _protoCompatibility;


        bool AppendToCollection => !_overwriteList;

        public ArrayDecorator(TypeModel model, IProtoSerializerWithWireType tail, bool writeProtoPacked, WireType expectedTailWireType, Type arrayType, bool overwriteList, int readLengthLimit, bool protoCompatibility)
            : base(tail)
        {
            Helpers.DebugAssert(arrayType != null, "arrayType should be non-null");
            if (!arrayType.IsArray || arrayType.GetArrayRank() != 1) throw new ArgumentException("should be single-dimension array; " + arrayType.FullName, nameof(arrayType));
            _itemType = tail.ExpectedType;
            if (_itemType != arrayType.GetElementType() && !Helpers.IsAssignableFrom(Tail.ExpectedType, arrayType.GetElementType())) throw new ArgumentException("Expected array type is " + arrayType.GetElementType() + " but tail type is " + _itemType);
            Helpers.DebugAssert(Tail.ExpectedType != model.MapType(typeof(byte)), "Should have used BlobSerializer");
            _writeProtoPacked = writeProtoPacked;
            _arrayType = arrayType;
            _overwriteList = overwriteList;
            _protoCompatibility = protoCompatibility;
            _readLengthLimit = readLengthLimit;
            _listHelpers = new ListHelpers(_writeProtoPacked, expectedTailWireType, _protoCompatibility, tail, false);
        }

        public override Type ExpectedType => _arrayType;
        public override bool RequiresOldValue => AppendToCollection;

#if FEAT_COMPILER
        public override bool EmitReadReturnsValue => true;

        protected override void EmitWrite(AqlaSerializer.Compiler.CompilerContext ctx, AqlaSerializer.Compiler.Local valueFrom)
        {
            var g = ctx.G;
            using (ctx.StartDebugBlockAuto(this))
            using (Compiler.Local value = ctx.GetLocalWithValue(_arrayType, valueFrom))
            {
                _listHelpers.EmitWrite(ctx.G, value,
                                       () => value.AsOperand.Property("Length"),
                                       () =>
                                           {
                                               var length = value.AsOperand.Property("Length");
                                               g.If(length > 0);
                                               {
                                                   g.Writer.WriteFieldHeader(ListHelpers.FieldLength, WireType.Variant);
                                                   g.Writer.WriteInt32(length);
                                               }
                                               g.End();
                                           }, null);
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
            using (Compiler.Local length = ctx.Local(typeof(int?), true))
            using (Compiler.Local oldLen = ctx.Local(typeof(int)))
            {
                g.Assign(reservedTrap, -1);
                _listHelpers.EmitRead(
                    ctx.G,
                    (fieldNumber, onSuccess, onFail) =>
                    {
                        using (ctx.StartDebugBlockAuto(this, "meta"))
                        {
                            g.If(fieldNumber == ListHelpers.FieldLength);
                            {
                                g.Assign(length, g.ReaderFunc.ReadInt32());
                                onSuccess();
                            }
                            g.Else();
                            {
                                onFail();
                            }
                            g.End();
                        }
                    },
                    () =>
                        {
                            using (ctx.StartDebugBlockAuto(this, "prepareInstance"))
                            {
                                g.If(length.AsOperand >= 0);
                                {
                                    ctx.MarkDebug("// length read, creating instance");
                                    var lengthValue = length.AsOperand.Property("Value");
                                    g.If(lengthValue > _readLengthLimit);
                                    {
                                        EmitThrowExceededLengthLimit(g, lengthValue, _readLengthLimit);
                                    }
                                    g.End();

                                    EmitRead_CreateInstance(g, value, lengthValue, null, oldLen, result);
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

        public static void EmitThrowExceededLengthLimit(SerializerCodeGen g, Operand length, int limit)
        {
            g.ThrowProtoException("Total array length " + length + " exceeded the limit " + limit + ", " +
                                    "set MetaType.ArrayLengthReadLimit");
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