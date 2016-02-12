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
    abstract class ProtoDecoratorBase : IProtoSerializer
    {
        public abstract Type ExpectedType { get; }
        protected readonly IProtoSerializer Tail;
        protected ProtoDecoratorBase(IProtoSerializer tail) { this.Tail = tail; }
        public abstract bool RequiresOldValue { get; }
#if !FEAT_IKVM
        public abstract void Write(object value, ProtoWriter dest);
        public abstract object Read(object value, ProtoReader source);
#endif

#if FEAT_COMPILER
        public abstract bool EmitReadReturnsValue { get; }
        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom) { EmitWrite(ctx, valueFrom); }
        protected abstract void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom);
        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom) { EmitRead(ctx, valueFrom); }
        // TODO may be return null or local and remove property EmitReadReturnsValue to avoid unnecessary copying between locals
        // TODO need to ensure that valueFrom is always correctly reassigned when not returns to stack
        protected abstract void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom);
#endif
    }
}
#endif