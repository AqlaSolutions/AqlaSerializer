using System;
using AqlaSerializer.Compiler;
using AqlaSerializer.Meta;

namespace AqlaSerializer.Serializers
{
    sealed class CollectionRootFieldDecorator : IProtoTypeSerializer
    {
        private readonly IProtoTypeSerializer _serializer;
        
        public CollectionRootFieldDecorator(IProtoTypeSerializer serializer)
        {
            _serializer = serializer;
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
            if (source.ReadFieldHeader() != ListHelpers.FieldItem) throw new ProtoException("Expected list tag");
            return _serializer.Read(value, source);
        }
        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteFieldHeaderBegin(ListHelpers.FieldItem, dest);
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