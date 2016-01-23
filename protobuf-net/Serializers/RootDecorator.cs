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

    sealed class RootDecorator : IProtoTypeSerializer
    {
        private readonly IProtoTypeSerializer _serializer;
        
        public RootDecorator(Type type, bool wrap, IProtoTypeSerializer serializer)
        {
            _serializer = wrap ? new NetObjectValueDecorator(type, serializer, !Helpers.IsValueType(type)) : serializer;
        }

        public Type ExpectedType
        {
            get { return _serializer.ExpectedType; }
        }
        public bool ReturnsValue
        {
            get { return _serializer.ReturnsValue; }
        }
        public bool RequiresOldValue
        {
            get { return _serializer.RequiresOldValue; }
        }
#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            ProtoReader.ExpectRoot(source);
            return _serializer.Read(value, source);
        }
        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.ExpectRoot(dest);
            _serializer.Write(value, dest);
        }
#endif

#if FEAT_COMPILER
        public void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            
        }

        public void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {

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