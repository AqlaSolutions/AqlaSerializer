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
    sealed class TimeSpanSerializer : IProtoSerializerWithAutoType
    {
#if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(TimeSpan);
#endif
        public TimeSpanSerializer(AqlaSerializer.Meta.TypeModel model)
        {
#if FEAT_IKVM
            expectedType = model.MapType(typeof(TimeSpan));
#endif
        }
        public Type ExpectedType => expectedType;

        bool IProtoSerializer.RequiresOldValue => false;
#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value == null); // since replaces
            return BclHelpers.ReadTimeSpan(source);
        }
        public void Write(object value, ProtoWriter dest)
        {
            BclHelpers.WriteTimeSpan((TimeSpan)value, dest);
        }
#endif
#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue => true;

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitWrite(ctx.MapType(typeof(BclHelpers)), "WriteTimeSpan", valueFrom);
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicRead(ctx.MapType(typeof(BclHelpers)), "ReadTimeSpan", ExpectedType);
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