// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
#endif
using System.Diagnostics;
using AqlaSerializer.Meta;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;

#endif

namespace AqlaSerializer.Serializers
{
    /// <summary>
    /// Normal derived types (using TypeSerializer) should never use root serializer
    /// </summary>
    sealed class ForbiddenRootStub : IProtoTypeSerializer
    {
        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            throw new NotSupportedException();
        }

        public bool DemandWireTypeStabilityStatus() => false;

        public ForbiddenRootStub(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            ExpectedType = type;
        }

        public Type ExpectedType { get; set; }
        public bool RequiresOldValue => false;
        
        public bool CanCancelWriting { get; }
#if !FEAT_IKVM
        public void Write(object value, ProtoWriter dest)
        {
            throw new NotSupportedException();
        }

        public object Read(object value, ProtoReader source)
        {
            throw new NotSupportedException();
        }
#endif

#if FEAT_COMPILER
        public bool EmitReadReturnsValue => false;
        public void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.G.ThrowNotSupportedException();
            }
        }

        public void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                ctx.G.ThrowNotSupportedException();
            }
        }

#endif
        public bool HasCallbacks(TypeModel.CallbackType callbackType)
        {
            return false;
        }

        public bool CanCreateInstance()
        {
            return false;
        }

#if !FEAT_IKVM
        public object CreateInstance(ProtoReader source)
        {
            throw new NotSupportedException();
        }

        public void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {
            throw new NotSupportedException();
        }
#endif
#if FEAT_COMPILER
        public void EmitCallback(CompilerContext ctx, Local valueFrom, TypeModel.CallbackType callbackType)
        {
            ctx.G.ThrowNotSupportedException();
        }

        public void EmitCreateInstance(CompilerContext ctx)
        {
            ctx.G.ThrowNotSupportedException();
        }
#endif
    }
}

#endif