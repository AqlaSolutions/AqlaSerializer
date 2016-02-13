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
    sealed class UInt32Serializer : IProtoSerializerWithAutoType
    {
#if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(uint);
#endif
        public UInt32Serializer(AqlaSerializer.Meta.TypeModel model)
        {
#if FEAT_IKVM
            expectedType = model.MapType(typeof(uint));
#endif
        }
        public Type ExpectedType { get { return expectedType; } }

        bool IProtoSerializer.RequiresOldValue { get { return false; } }
#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value == null); // since replaces
            return source.ReadUInt32();
        }
        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteUInt32((uint)value, dest);
        }
#endif
#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue { get { return true; } }

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicWrite("WriteUInt32", valueFrom);
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.EmitBasicRead("ReadUInt32", ctx.MapType(typeof(uint)));
            }
        }
#endif
    }
}
#endif