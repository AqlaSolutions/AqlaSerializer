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
        public virtual void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            using (builder.SingleTailDecorator(this))
                Tail.WriteDebugSchema(builder);
        }

        public abstract Type ExpectedType { get; }
        protected readonly IRuntimeProtoSerializerNode Tail;
        protected ProtoDecoratorBase(IRuntimeProtoSerializerNode tail)
        {
            this.Tail = tail;
        }
        public abstract bool RequiresOldValue { get; }
        public virtual bool CanCancelWriting => Tail.CanCancelWriting;
#if !FEAT_IKVM
        public abstract void Write(ProtoWriter dest, ref ProtoWriter.State state, object value);
        public abstract object Read(ref ProtoReader.State state, object value);

        void IRuntimeProtoSerializerNode.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom) { EmitWrite(ctx, valueFrom); }
        protected abstract void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom);
        void IRuntimeProtoSerializerNode.EmitRead(Compiler.CompilerContext ctx, Compiler.Local entity) { EmitRead(ctx, entity); }
        protected abstract void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom);
#endif

#if FEAT_COMPILER
        public abstract bool EmitReadReturnsValue { get; }
        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom) { EmitWrite(ctx, valueFrom); }
        protected abstract void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom);
        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom) { EmitRead(ctx, valueFrom); }
        // TODO may be return null or local and remove property EmitReadReturnsValue to avoid unnecessary copying between locals
        // TODO need to ensure that valueFrom is always correctly reassigned when not returns to stack
        // TODO may be get rid of returning through stack? always require old value?
        protected abstract void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom);
#endif
    }
}
