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
    sealed class DoubleSerializer : IProtoSerializerWithAutoType
    {
#if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(double);
#endif
        public DoubleSerializer(AqlaSerializer.Meta.TypeModel model)
        {
#if FEAT_IKVM
            expectedType = model.MapType(typeof(double));
#endif
        }

        public Type ExpectedType => expectedType;
        bool IProtoSerializer.RequiresOldValue => false;

#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value == null); // since replaces
            return source.ReadDouble();
        }
        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteDouble((double)value, dest);
        }
#endif
#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue => true;

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicWrite("WriteDouble", valueFrom);
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicRead("ReadDouble", ExpectedType);
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