// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AltLinq;
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
    sealed class MultiDimensionalArrayDecorator : ProtoDecoratorBase, IProtoTypeSerializer
    {
        readonly int _readLengthLimit;
        readonly int _rank;

        // will be always group or string and won't change between group and string in same session
        public bool DemandWireTypeStabilityStatus() => true;

        public WireType? ConstantWireType => _listHelpers.ConstantWireType;

#if !FEAT_IKVM
        public override void Write(object value, ProtoWriter dest)
        {
            _listHelpers.Write(value,
                               () =>
                                   {
                                       var arr = (Array)value;
                                       for (int i = 0; i < _rank; i++)
                                       {
                                           ProtoWriter.WriteFieldHeader(ListHelpers.FieldLength, WireType.Variant, dest);
                                           ProtoWriter.WriteInt32(arr.GetLength(i), dest);
                                       }
                                   }, null, dest);
        }

        public override object Read(object value, ProtoReader source)
        {
            Array result = null;

            int[] lengths = new int[_rank];
            int[] indexes = new int[_rank];
            int deepestRank = 0;
            int deepestRankLength = 0;

            int totalLength = 1;

            _listHelpers.Read(
                () =>
                    {
                        if (source.TryReadFieldHeader(ListHelpers.FieldLength))
                        {
                            int length = source.ReadInt32();
                            lengths[deepestRank++] = length;
                            totalLength *= length;

                            return true;
                        }
                        return false;
                    },
                () =>
                    {
                        // count
                        if (deepestRank != _rank) ThrowWrongRank();
                        // last
                        deepestRank--;

                        deepestRankLength = lengths[deepestRank];

                        if (totalLength > _readLengthLimit)
                            ArrayDecorator.ThrowExceededLengthLimit(totalLength, _readLengthLimit);

                        // TODO use same instance when length equals and no AppendCollection, don't forget to NoteObject even for the same instance
                        
                        result = Read_CreateInstance(value, lengths, out indexes[0], source);
                    },
                v =>
                {
                    result.SetValue(v, indexes);
                    //Debug.WriteLine(string.Join(",", indexes.Select(x => x.ToString()).ToArray()));
                    int newIndex = ++indexes[deepestRank];
                    if (newIndex >= deepestRankLength)
                    {
                        int rankIndex = deepestRank;
                        while (rankIndex > 0)
                        {
                            indexes[rankIndex] = 0;
                            --rankIndex;
                            indexes[rankIndex]++;

                            if (indexes[rankIndex] < lengths[rankIndex]) break;
                        }
                    }
                },
                source);
            return result;
        }

        void ThrowWrongRank()
        {
            throw new ProtoException("Wrong array rank read from source stream, type " + ExpectedType);
        }
        
        Array Read_CreateInstance(object value, int[] lengths, out int oldFirstDimLength, ProtoReader source)
        {
            var valueArr = AppendToCollection ? value as Array : null;
            if (valueArr != null)
                lengths[0] += oldFirstDimLength = valueArr.GetLength(0);
            else
                oldFirstDimLength = 0;

            Array result = Array.CreateInstance(_itemType, lengths);
            ProtoReader.NoteObject(result, source);

            if (oldFirstDimLength > 0) Array.Copy(valueArr, result, valueArr.Length);

            return result;
        }
#endif

        readonly ListHelpers _listHelpers;
        readonly Type _arrayType; // this is, for example, typeof(int[])
        readonly bool _overwriteList;
        readonly Type _itemType; // this is, for example, typeof(int[])
        
        bool AppendToCollection => !_overwriteList;

        public MultiDimensionalArrayDecorator(RuntimeTypeModel model, IProtoSerializerWithWireType tail, Type arrayType, bool overwriteList, int readLengthLimit)
            : base(tail)
        {
            Helpers.DebugAssert(arrayType != null, "arrayType should be non-null");
            _rank = arrayType.GetArrayRank();
            if (_rank <= 1) throw new ArgumentException("should be multi-dimension array; " + arrayType.FullName, nameof(arrayType));
            _itemType = tail.ExpectedType;
            if (_itemType != arrayType.GetElementType()) throw new ArgumentException("Expected array type is " + arrayType.GetElementType() + " but tail type is " + _itemType);
            _arrayType = arrayType;
            _overwriteList = overwriteList;
            _readLengthLimit = readLengthLimit;
            _listHelpers = new ListHelpers(false, model.ProtoCompatibility.UseVersioning, WireType.None, false, tail, true);
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
                                       () =>
                                           {
                                               var arr = value;
                                               for (int i = 0; i < _rank; i++)
                                               {
                                                   g.Writer.WriteFieldHeader(ListHelpers.FieldLength, WireType.Variant);
                                                   g.Writer.WriteInt32(arr.AsOperand.Invoke("GetLength", i));
                                               }
                                           }, null);
            }
        }

        protected override void EmitRead(AqlaSerializer.Compiler.CompilerContext ctx, AqlaSerializer.Compiler.Local valueFrom)
        {
            var g = ctx.G;
            using (ctx.StartDebugBlockAuto(this))
            using (Compiler.Local value = ctx.GetLocalWithValueForEmitRead(this, valueFrom))
            using (Compiler.Local result = ctx.Local(_arrayType, true))
            using (Compiler.Local lengthTemp = ctx.Local(typeof(int)))
            using (Compiler.Local deepestRank = ctx.Local(typeof(int), true))
            using (Compiler.Local totalLength = ctx.Local(typeof(int)))
            {
                var lengths = Enumerable.Range(0, _rank).Select(x => g.ctx.Local(typeof(int))).ToArray();
                var indexes = Enumerable.Range(0, _rank).Select(x => g.ctx.Local(typeof(int), true)).ToArray();
                var indexesOp = indexes.Select(x => (Operand)x).ToArray();

                var deepestRankLength = lengths[lengths.Length - 1];

                g.Assign(totalLength, 1);

                _listHelpers.EmitRead(
                    ctx.G,
                    (onSuccess, onFail) =>
                    {
                        using (ctx.StartDebugBlockAuto(this, "meta"))
                        {
                            g.If(g.ReaderFunc.TryReadFieldHeader_bool(ListHelpers.FieldLength));
                            {
                                g.Assign(lengthTemp, g.ReaderFunc.ReadInt32());
                                g.Switch(deepestRank);
                                {
                                    for (int i = 0; i < _rank; i++)
                                    {
                                        g.Case(i);
                                        {
                                            g.Assign(lengths[i], lengthTemp);
                                        }
                                        g.Break();
                                    }

                                    g.DefaultCase();
                                    EmitThrowWrongRank(g);
                                }
                                g.End();
                                g.Increment(deepestRank);
                                g.Assign(totalLength, totalLength.AsOperand * lengthTemp.AsOperand);
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
                            g.If(deepestRank.AsOperand != _rank);
                            {
                                EmitThrowWrongRank(g);
                            }
                            g.End();

                            //g.Decrement(deepestRank); - not used

                            g.If(totalLength.AsOperand > _readLengthLimit);
                            {
                                ArrayDecorator.EmitThrowExceededLengthLimit(g, totalLength, _readLengthLimit);
                            }
                            g.End();

                            ctx.MarkDebug("// length read, creating instance");
                            EmitRead_CreateInstance(g, value, lengths, indexes[0], result);
                        }
                    },
                    v =>
                    {
                        using (ctx.StartDebugBlockAuto(this, "add"))
                        {
                            g.Assign(result.AsOperand[indexesOp], v);

                            var newIndex = indexes[_rank - 1];
                            g.Increment(newIndex);
                            g.If(newIndex.AsOperand >= deepestRankLength.AsOperand);
                            {
                                // unwrapped loop
                                var breakLabel = g.DefineLabel();
                                int rankIndex = _rank - 1;
                                while (rankIndex > 0)
                                {
                                    g.Assign(indexes[rankIndex], 0);
                                    --rankIndex;
                                    g.Increment(indexes[rankIndex]);
                                    g.If(indexes[rankIndex].AsOperand < lengths[rankIndex].AsOperand);
                                    {
                                        g.Goto(breakLabel);
                                    }
                                    g.End();
                                }
                                g.MarkLabel(breakLabel);
                            }
                            g.End();
                        }
                    });

                foreach (Local local in indexes.Concat(lengths))
                    local.Dispose();

                if (EmitReadReturnsValue)
                    ctx.LoadValue(result);
                else
                    g.Assign(value, result);
            }
        }

        void EmitRead_CreateInstance(SerializerCodeGen g, Local valueArr, Local[] lengths, Operand optionalOutOldFirstDimLength, Local outResult)
        {
            using (g.ctx.StartDebugBlockAuto(this))
            {
                if (AppendToCollection)
                {
                    g.If(valueArr.AsOperand != null);
                    {
                        g.AssignAdd(lengths[0], optionalOutOldFirstDimLength.Assign(valueArr.AsOperand.Invoke("GetLength", 0)));
                    }
                    g.End();
                }

                g.Assign(outResult, g.ExpressionFactory.NewArray(_itemType, lengths.Select(l => (Operand)l.AsOperand).ToArray()));
                
                g.Reader.NoteObject(outResult);

                if (AppendToCollection)
                {
                    g.If(valueArr.AsOperand != null && optionalOutOldFirstDimLength > 0);
                    {
                        g.Invoke(typeof(Array), "Copy", valueArr, outResult, valueArr.AsOperand.Property("Length"));
                    }
                    g.End();
                }
            }
        }


        void EmitThrowWrongRank(SerializerCodeGen g)
        {
            g.ThrowProtoException("Wrong array rank read from source stream, type " + ExpectedType);
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
            Array r = Array.CreateInstance(_itemType, new int[_rank]);
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
                ctx.G.Eval(ctx.G.ExpressionFactory.NewArray(_itemType, Enumerable.Range(0, _rank).Select(x => (Operand)0).ToArray()));
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