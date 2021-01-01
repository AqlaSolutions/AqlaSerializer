// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using AqlaSerializer.Meta;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif



namespace AqlaSerializer.Serializers
{
    sealed class MemberSpecifiedDecorator : ProtoDecoratorBase, IProtoSerializerWithWireType
    {
        public override void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            using (builder.SingleTailDecorator(this, _getSpecified.Name))
                Tail.WriteDebugSchema(builder);
        }

        public bool DemandWireTypeStabilityStatus() => false;
        // may be not specified, right?
        public override Type ExpectedType => Tail.ExpectedType;
        public override bool RequiresOldValue => true;
        
        public override bool CanCancelWriting => true;
        private readonly MethodInfo _getSpecified, _setSpecified;
        
        public MemberSpecifiedDecorator(MethodInfo getSpecified, MethodInfo setSpecified, IProtoSerializerWithWireType tail)
            : base(tail)
        {
            if (getSpecified == null && setSpecified == null) throw new InvalidOperationException();
            this._getSpecified = getSpecified;
            this._setSpecified = setSpecified;
        }
#if !FEAT_IKVM
        public override void Write(ProtoWriter dest, ref ProtoWriter.State state, object value)
        {
            if(_getSpecified == null || (bool)_getSpecified.Invoke(value, null))
            {
                Tail.Write(value, dest);
            }
            else
                ProtoWriter.WriteFieldHeaderCancelBegin(dest);
        }
        public override object Read(ProtoReader source, ref ProtoReader.State state, object value)
        {
            object result = Tail.Read(value, source);
            _setSpecified?.Invoke(value, new object[] { true });
            return result;
        }

        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            if (getSpecified is null)
            {
                Tail.EmitWrite(ctx, valueFrom);
                return;
            }
            using Compiler.Local loc = ctx.GetLocalWithValue(ExpectedType, valueFrom);
            ctx.LoadAddress(loc, ExpectedType);
            ctx.EmitCall(getSpecified);
            Compiler.CodeLabel done = ctx.DefineLabel();
            ctx.BranchIfFalse(done, false);
            Tail.EmitWrite(ctx, loc);
            ctx.MarkLabel(done);
        }
        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            if (setSpecified is null)
            {
                Tail.EmitRead(ctx, valueFrom);
                return;
            }
            using Compiler.Local loc = ctx.GetLocalWithValue(ExpectedType, valueFrom);
            Tail.EmitRead(ctx, loc);
            ctx.LoadAddress(loc, ExpectedType);
            ctx.LoadValue(1); // true
            ctx.EmitCall(setSpecified);
        }
#endif

#if FEAT_COMPILER
        public override bool EmitReadReturnsValue => Tail.EmitReadReturnsValue;

        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this, _getSpecified?.Name))
            {
                if (_getSpecified == null)
                {
                    Tail.EmitWrite(ctx, valueFrom);
                    return;
                }
                using (Compiler.Local loc = ctx.GetLocalWithValue(ExpectedType, valueFrom))
                {
                    ctx.LoadAddress(loc, ExpectedType);
                    ctx.EmitCall(_getSpecified);
                    Compiler.CodeLabel done = ctx.DefineLabel();
                    Compiler.CodeLabel notSpecified = ctx.DefineLabel();
                    ctx.BranchIfFalse(notSpecified, false);
                    {
                        Tail.EmitWrite(ctx, loc);
                    }
                    ctx.Branch(done, true);
                    ctx.MarkLabel(notSpecified);
                    {
                        ctx.G.Writer.WriteFieldHeaderCancelBegin();
                    }
                    ctx.MarkLabel(done);
                }

            }
        }

        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this, _setSpecified?.Name))
            {
                if (_setSpecified == null)
                {
                    Tail.EmitRead(ctx, valueFrom);
                    return;
                }
                using (Compiler.Local loc = ctx.GetLocalWithValue(ExpectedType, valueFrom))
                {
                    Tail.EmitRead(ctx, loc);
                    ctx.LoadAddress(loc, ExpectedType);
                    ctx.LoadValue(1); // true
                    ctx.EmitCall(_setSpecified);
                }
            }
        }
#endif
    }
}
