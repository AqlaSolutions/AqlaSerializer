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
    sealed class DecimalSerializer : IProtoSerializerWithAutoType
    {
        #if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(decimal);
#endif
        public DecimalSerializer(AqlaSerializer.Meta.TypeModel model)
        {
#if FEAT_IKVM
            expectedType = model.MapType(typeof(decimal));
#endif
        }
        public Type ExpectedType => expectedType;

        bool IProtoSerializer.RequiresOldValue => false;
        
        public bool CanCancelWriting { get; }

#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value == null); // since replaces
            return BclHelpers.ReadDecimal(source);
        }
        public void Write(object value, ProtoWriter dest)
        {
            BclHelpers.WriteDecimal((decimal)value, dest);
        }
#endif
#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue => true;

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitWrite(ctx.MapType(typeof(BclHelpers)), "WriteDecimal", valueFrom);
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicRead(ctx.MapType(typeof(BclHelpers)), "ReadDecimal", ExpectedType);
            }
        }
#endif

        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            builder.SingleValueSerializer(this);
        }
    }
}
#endif