// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif

namespace AqlaSerializer.Serializers
{
    sealed class UriDecorator : ProtoDecoratorBase, IProtoSerializerWithWireType
    {
            readonly IProtoSerializerWithWireType _tail;
        public bool DemandWireTypeStabilityStatus() => _tail.DemandWireTypeStabilityStatus();
#if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(Uri);
#endif
        public UriDecorator(AqlaSerializer.Meta.TypeModel model, IProtoSerializerWithWireType tail)
            : base(tail)
        {
            _tail = tail;
#if FEAT_IKVM
            expectedType = model.MapType(typeof(Uri));
#endif
        }

        public override Type ExpectedType => expectedType;
        public override bool RequiresOldValue => false;
        

#if !FEAT_IKVM
        public override void Write(object value, ProtoWriter dest)
        {
            Tail.Write(((Uri)value).OriginalString, dest);
        }

        public override object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value == null); // not expecting incoming
            string s = (string)Tail.Read(null, source);
            return s.Length == 0 ? null : new Uri(s, UriKind.RelativeOrAbsolute);
        }
    #endif

    #if FEAT_COMPILER
            public override bool EmitReadReturnsValue => true;

            protected override void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
            {
                using (ctx.StartDebugBlockAuto(this))
                {
                ctx.LoadValue(valueFrom);
                ctx.LoadValue(typeof(Uri).GetProperty("OriginalString"));
                Tail.EmitWrite(ctx, null);
            }
            }

            protected override void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
            {
                using (ctx.StartDebugBlockAuto(this))
                {
                Tail.EmitRead(ctx, valueFrom);
                ctx.CopyValue();
                Compiler.CodeLabel @nonEmpty = ctx.DefineLabel(), @end = ctx.DefineLabel();
                ctx.LoadValue(typeof(string).GetProperty("Length"));
                ctx.BranchIfTrue(@nonEmpty, true);
                ctx.DiscardValue();
                ctx.LoadNullRef();
                ctx.Branch(@end, true);
                ctx.MarkLabel(@nonEmpty);
                ctx.LoadValue((int)UriKind.RelativeOrAbsolute);
                ctx.EmitCtor(ctx.MapType(typeof(Uri)), ctx.MapType(typeof(string)), ctx.MapType(typeof(UriKind)));
                ctx.MarkLabel(@end);
            }
            }
    #endif 
        }
}
#endif
