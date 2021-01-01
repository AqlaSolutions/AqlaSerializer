// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if FEAT_COMPILER && !(FX11 || FEAT_IKVM)
using System;
using AqlaSerializer.Meta;



namespace AqlaSerializer.Serializers
{
    sealed class CompiledSerializer : IProtoTypeSerializer
    {
        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            _head.WriteDebugSchema(builder);
        }
        
        readonly bool _isStableWireType;

        public bool DemandWireTypeStabilityStatus()
        {
            return _isStableWireType;
        }

        bool IProtoTypeSerializer.HasCallbacks(TypeModel.CallbackType callbackType)
        {
            return _head.HasCallbacks(callbackType); // these routes only used when bits of the model not compiled
        }
        bool IProtoTypeSerializer.CanCreateInstance()
        {
            return _head.CanCreateInstance();
        }
        object IProtoTypeSerializer.CreateInstance(ProtoReader source)
        {
            return _head.CreateInstance(source);
        }
        public void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {
            _head.Callback(value, callbackType, context); // these routes only used when bits of the model not compiled
        }
        public static CompiledSerializer Wrap(IProtoTypeSerializer head, RuntimeTypeModel model)
        {
            CompiledSerializer result = head as CompiledSerializer;
            if (result == null)
            {
                result = new CompiledSerializer(head, model);
                Helpers.DebugAssert(((IProtoTypeSerializer)result).ExpectedType == head.ExpectedType);
            }
            return result;
        }
        private readonly IProtoTypeSerializer _head;
        private readonly Compiler.ProtoSerializer _serializer;
        private readonly Compiler.ProtoDeserializer _deserializer;
        private CompiledSerializer(IProtoTypeSerializer head, RuntimeTypeModel model)
        {
            this._head = head;
            _isStableWireType = head.DemandWireTypeStabilityStatus();
            _serializer = Compiler.CompilerContext.BuildSerializer(head, model);
            _deserializer = Compiler.CompilerContext.BuildDeserializer(head, model);
        }
        bool IProtoSerializer.RequiresOldValue => _head.RequiresOldValue;
        
        public bool CanCancelWriting { get; }

        bool IProtoSerializer.EmitReadReturnsValue => _head.EmitReadReturnsValue;

        Type IProtoSerializer.ExpectedType => _head.ExpectedType;

        void IProtoSerializer.Write(object value, ProtoWriter dest)
        {
            _serializer(value, dest);
        }
        object IProtoSerializer.Read(object value, ProtoReader source)
        {
            return _deserializer(value, source);
        }

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                _head.EmitWrite(ctx, valueFrom);
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                _head.EmitRead(ctx, valueFrom);
            }
        }

        void IProtoTypeSerializer.EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType)
        {
            _head.EmitCallback(ctx, valueFrom, callbackType);
        }
        void IProtoTypeSerializer.EmitCreateInstance(Compiler.CompilerContext ctx)
        {
            _head.EmitCreateInstance(ctx);
        }
    }
}
#endif