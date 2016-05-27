// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Diagnostics;
using AqlaSerializer.Meta;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
#if FEAT_IKVM
using IKVM.Reflection.Emit;
using Type = IKVM.Reflection.Type;
#else
using System.Reflection.Emit;

#endif
#endif

namespace AqlaSerializer.Serializers
{
    sealed class ModelTypeSerializer : IProtoTypeSerializer
    {
        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            using (builder.SingleTailDecorator(this))
            {
                var ser = _model[_baseKey].Serializer;
                var b = builder.Contract(ser.ExpectedType);
                if (b != null)
                    ser.WriteDebugSchema(b);
            }
        }
        
        public bool DemandWireTypeStabilityStatus() => _concreteSerializerProxy.Serializer.DemandWireTypeStabilityStatus();

        private readonly int _baseKey;
        private readonly ISerializerProxy _concreteSerializerProxy;
        readonly RuntimeTypeModel _model;

        public ModelTypeSerializer(Type type, int baseKey, ISerializerProxy concreteSerializerProxy, RuntimeTypeModel model)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (concreteSerializerProxy == null) throw new ArgumentNullException(nameof(concreteSerializerProxy));
            if (model == null) throw new ArgumentNullException(nameof(model));
            ExpectedType = type;
            _concreteSerializerProxy = concreteSerializerProxy;
            _model = model;
            _baseKey = baseKey;
        }

        public Type ExpectedType { get; }
        bool IProtoSerializer.RequiresOldValue => true;

#if !FEAT_IKVM
        void IProtoSerializer.Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteRecursionSafeObject(value, _baseKey, dest);
        }

        object IProtoSerializer.Read(object value, ProtoReader source)
        {
            return ProtoReader.ReadObject(value, _baseKey, source);
        }
#endif

#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue => true;

        bool EmitDedicatedMethod(Compiler.CompilerContext ctx, Compiler.Local valueFrom, bool read)
        {
#if SILVERLIGHT
            return false;
#else
            var pair = ctx.GetDedicatedMethod(_baseKey)?.BasicPair;
            MethodBuilder method = read ? pair?.Deserialize : pair?.Serialize;
            if (method == null) return false;

            ctx.LoadValue(valueFrom);
            if (read)
                ctx.EmitReadCall(ctx, ExpectedType, method);
            else
                ctx.EmitWriteCall(ctx, method);
#endif
            return true;
        }

        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                if (!EmitDedicatedMethod(ctx, valueFrom, false))
                {
                    ctx.LoadValue(valueFrom);
                    if (ExpectedType.IsValueType) ctx.CastToObject(ExpectedType);
                    ctx.LoadValue(ctx.MapMetaKeyToCompiledKey(_baseKey)); // re-map for formality, but would expect identical, else dedicated method
                    ctx.LoadReaderWriter();
                    ctx.EmitCall(ctx.MapType(typeof(ProtoWriter)).GetMethod("WriteRecursionSafeObject"));
                }
            }
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                if (!EmitDedicatedMethod(ctx, valueFrom, true))
                {
                    ctx.LoadValue(valueFrom);
                    if (ExpectedType.IsValueType) ctx.CastToObject(ExpectedType);
                    ctx.LoadValue(ctx.MapMetaKeyToCompiledKey(_baseKey)); // re-map for formality, but would expect identical, else dedicated method
                    ctx.LoadReaderWriter();
                    ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod("ReadObject"));
                    ctx.CastFromObject(ExpectedType);
                }
            }
        }
#endif


        bool IProtoTypeSerializer.HasCallbacks(TypeModel.CallbackType callbackType)
        {
            return ((IProtoTypeSerializer)_concreteSerializerProxy.Serializer).HasCallbacks(callbackType);
        }

        bool IProtoTypeSerializer.CanCreateInstance()
        {
            return ((IProtoTypeSerializer)_concreteSerializerProxy.Serializer).CanCreateInstance();
        }

#if FEAT_COMPILER
        void IProtoTypeSerializer.EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType)
        {
            ((IProtoTypeSerializer)_concreteSerializerProxy.Serializer).EmitCallback(ctx, valueFrom, callbackType);
        }

        void IProtoTypeSerializer.EmitCreateInstance(Compiler.CompilerContext ctx)
        {
            ((IProtoTypeSerializer)_concreteSerializerProxy.Serializer).EmitCreateInstance(ctx);
        }
#endif
#if !FEAT_IKVM
        void IProtoTypeSerializer.Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {
            ((IProtoTypeSerializer)_concreteSerializerProxy.Serializer).Callback(value, callbackType, context);
        }

        object IProtoTypeSerializer.CreateInstance(ProtoReader source)
        {
            return ((IProtoTypeSerializer)_concreteSerializerProxy.Serializer).CreateInstance(source);
        }
#endif
    }
}

#endif