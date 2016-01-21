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
    sealed class StringSerializer : IProtoSerializerWithAutoType
    {
#if FEAT_IKVM
        readonly Type expectedType;
#else
        static readonly Type expectedType = typeof(string);
#endif
        public StringSerializer(AqlaSerializer.Meta.TypeModel model)
        {
#if FEAT_IKVM
            expectedType = model.MapType(typeof(string));
#endif
        }
        public Type ExpectedType { get { return expectedType; } }
        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteString((string)value, dest);
        }
        bool IProtoSerializer.RequiresOldValue { get { return false; } }
        bool IProtoSerializer.ReturnsValue { get { return true; } }

        public object Read(object value, ProtoReader source)
        {
            Helpers.DebugAssert(value == null); // since replaces
            return source.ReadString();
        }
#if FEAT_COMPILER
        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.EmitBasicWrite("WriteString", valueFrom);
        }
        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            ctx.EmitBasicRead("ReadString", ExpectedType);
        }
#endif
    }
}
#endif