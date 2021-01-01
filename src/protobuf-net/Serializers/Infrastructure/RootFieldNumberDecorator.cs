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
    sealed class RootFieldNumberDecorator : IProtoTypeSerializer
    {
        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            using (builder.Field(_number))
                _serializer.WriteDebugSchema(builder);
        }
        
        public bool DemandWireTypeStabilityStatus() => _serializer.DemandWireTypeStabilityStatus();
        private readonly IProtoTypeSerializer _serializer;
        readonly int _number;

        public RootFieldNumberDecorator(IProtoTypeSerializer serializer, int number)
        {
            _serializer = serializer;
            _number = number;
        }

        public Type ExpectedType => _serializer.ExpectedType;

        
        public bool CanCancelWriting { get; } // cannot because it doesn't need to be inside field so nothing to cancel
        public bool RequiresOldValue => _serializer.RequiresOldValue;
#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            if (source.ReadFieldHeader() != _number) throw new ProtoException("Expected tag " + _number);
            return _serializer.Read(value, source);
        }
        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteFieldHeaderBegin(_number, dest);
            _serializer.Write(value, dest);
        }
#endif

#if FEAT_COMPILER
        public bool EmitReadReturnsValue => _serializer.EmitReadReturnsValue;

        public void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                var g = ctx.G;
                g.If(g.ReaderFunc.ReadFieldHeader_int() != _number);
                {
                    g.ThrowProtoException("Expected tag " + _number);
                }
                g.End();
                _serializer.EmitRead(ctx, valueFrom);
            }
        }

        public void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                var g = ctx.G;
                g.Writer.WriteFieldHeaderBegin(_number);
                _serializer.EmitWrite(ctx, valueFrom);
            }
        }
#endif
        public bool HasCallbacks(TypeModel.CallbackType callbackType)
        {
            return _serializer.HasCallbacks(callbackType);
        }

        public bool CanCreateInstance()
        {
            return _serializer.CanCreateInstance();
        }
#if !FEAT_IKVM
        public object CreateInstance(ProtoReader source)
        {
            return _serializer.CreateInstance(source);
        }

        public void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {
            _serializer.Callback(value, callbackType, context);
        }
#endif
#if FEAT_COMPILER
        public void EmitCallback(CompilerContext ctx, Local valueFrom, TypeModel.CallbackType callbackType)
        {
            _serializer.EmitCallback(ctx, valueFrom, callbackType);
        }

        public void EmitCreateInstance(CompilerContext ctx)
        {
            _serializer.EmitCreateInstance(ctx);
        }
#endif
    }
}
#endif