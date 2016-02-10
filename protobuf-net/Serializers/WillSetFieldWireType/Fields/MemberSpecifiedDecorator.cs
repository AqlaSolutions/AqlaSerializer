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
        public bool DemandWireTypeStabilityStatus() => false;
        // may be not specified, right?
        public override Type ExpectedType => Tail.ExpectedType;
        public override bool RequiresOldValue => true;
        public override bool ReturnsValue => Tail.ReturnsValue;
        private readonly MethodInfo _getSpecified, _setSpecified;
        readonly IProtoSerializerWithWireType _tail;

        public MemberSpecifiedDecorator(MethodInfo getSpecified, MethodInfo setSpecified, IProtoSerializerWithWireType tail)
            : base(tail)
        {
            if (getSpecified == null && setSpecified == null) throw new InvalidOperationException();
            this._getSpecified = getSpecified;
            this._setSpecified = setSpecified;
            _tail = tail;
        }
#if !FEAT_IKVM
        public override void Write(object value, ProtoWriter dest)
        {
            if(_getSpecified == null || (bool)_getSpecified.Invoke(value, null))
            {
                Tail.Write(value, dest);
            }
            else
                ProtoWriter.WriteFieldHeaderCancelBegin(dest);
        }
        public override object Read(object value, ProtoReader source)
        {
            object result = Tail.Read(value, source);
            if (_setSpecified != null) _setSpecified.Invoke(value, new object[] { true });
            return result;
        }
#endif

#if FEAT_COMPILER
        protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
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
        protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
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
#endif
    }
}
#endif