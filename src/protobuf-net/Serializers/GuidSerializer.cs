// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
#endif


namespace AqlaSerializer.Serializers
{
    sealed class GuidSerializer : IProtoSerializerWithAutoType
    {
#if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(Guid);
#endif
        public GuidSerializer(AqlaSerializer.Meta.TypeModel model)
        {
#if FEAT_IKVM
            expectedType = model.MapType(typeof(Guid));
#endif
        }

        public Type ExpectedType => expectedType;

        bool IProtoSerializer.RequiresOldValue => false;
        
        public bool CanCancelWriting { get; }

#if !FEAT_IKVM
        public void Write(ProtoWriter dest, ref ProtoWriter.State state, object value)
        {
            BclHelpers.WriteGuid((Guid)value, dest);
        }
        public object Read(ProtoReader source, ref ProtoReader.State state, object value)
        {
            Helpers.DebugAssert(value == null); // since replaces
            return BclHelpers.ReadGuid(source);
        }
#endif

#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue => true;

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitWrite(ctx.MapType(typeof(BclHelpers)), "WriteGuid", valueFrom);
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicRead(ctx.MapType(typeof(BclHelpers)), "ReadGuid", ExpectedType);
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