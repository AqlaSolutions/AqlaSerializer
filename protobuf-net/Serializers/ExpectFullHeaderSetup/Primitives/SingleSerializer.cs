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
    sealed class SingleSerializer : IProtoSerializerWithAutoType
    {
#if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(float);
#endif
        public Type ExpectedType { get { return expectedType; } }

        public SingleSerializer(TypeModel model)
        {
#if FEAT_IKVM
            expectedType = model.MapType(typeof(float));
#endif
        }
        bool IProtoSerializer.RequiresOldValue { get { return false; } }
#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value == null); // since replaces
            return source.ReadSingle();
        }
        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteSingle((float)value, dest);
        }
#endif

#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue { get { return true; } }

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicWrite("WriteSingle", valueFrom);
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicRead("ReadSingle", ExpectedType);
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