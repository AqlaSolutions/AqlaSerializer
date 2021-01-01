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
    sealed class BooleanSerializer : IProtoSerializerWithAutoType
    {
#if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(bool);
#endif
        public BooleanSerializer(AqlaSerializer.Meta.TypeModel model)
        {
#if FEAT_IKVM
            expectedType = model.MapType(typeof(bool));
#endif
        }
        public Type ExpectedType => expectedType;

#if !FEAT_IKVM
        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteBoolean((bool)value, dest);
        }
        public object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value == null); // since replaces
            
            return source.ReadBoolean();
        }
#endif
        bool IProtoSerializer.RequiresOldValue => false;
        
        public bool CanCancelWriting { get; }

#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue => true;

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicWrite("WriteBoolean", valueFrom);
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicRead("ReadBoolean", ExpectedType);
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