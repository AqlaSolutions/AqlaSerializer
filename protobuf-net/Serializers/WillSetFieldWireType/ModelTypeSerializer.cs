// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Diagnostics;
using AqlaSerializer.Meta;
#if FEAT_COMPILER
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
                var ser = _model[_key].Serializer;
                var b = builder.Contract(ser.ExpectedType);
                if (b != null)
                    ser.WriteDebugSchema(b);
            }
        }
        
        public bool DemandWireTypeStabilityStatus() => _proxy.Serializer.DemandWireTypeStabilityStatus();

        private readonly int _key;
        private readonly ISerializerProxy _proxy;
        readonly RuntimeTypeModel _model;

        public ModelTypeSerializer(Type type, int key, ISerializerProxy proxy, RuntimeTypeModel model)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (proxy == null) throw new ArgumentNullException(nameof(proxy));
            if (model == null) throw new ArgumentNullException(nameof(model));
            this.ExpectedType = type;
            this._proxy = proxy;
            _model = model;
            this._key = key;
        }

        public Type ExpectedType { get; }
        bool IProtoSerializer.RequiresOldValue => true;

#if !FEAT_IKVM
        void IProtoSerializer.Write(object value, ProtoWriter dest)
        {
            ProtoWriter.WriteRecursionSafeObject(value, _key, dest);
        }

        object IProtoSerializer.Read(object value, ProtoReader source)
        {
            return ProtoReader.ReadObject(value, _key, source);
        }
#endif

#if FEAT_COMPILER
        bool IProtoSerializer.EmitReadReturnsValue => true;

        bool EmitDedicatedMethod(Compiler.CompilerContext ctx, Compiler.Local valueFrom, bool read)
        {
#if SILVERLIGHT
            return false;
#else
            MethodBuilder method = ctx.GetDedicatedMethod(_key, read);
            if (method == null) return false;

            ctx.LoadValue(valueFrom);
            ctx.LoadReaderWriter();
            ctx.EmitCall(method);
            // handle inheritance (we will be calling the *base* version of things,
            // but we expect Read to return the "type" type)
            if (read && ExpectedType != method.ReturnType) ctx.Cast(this.ExpectedType);
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
                    ctx.LoadValue(ctx.MapMetaKeyToCompiledKey(_key)); // re-map for formality, but would expect identical, else dedicated method
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
                    ctx.LoadValue(ctx.MapMetaKeyToCompiledKey(_key)); // re-map for formality, but would expect identical, else dedicated method
                    ctx.LoadReaderWriter();
                    ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod("ReadObject"));
                    ctx.CastFromObject(ExpectedType);
                }
            }
        }
#endif


        bool IProtoTypeSerializer.HasCallbacks(TypeModel.CallbackType callbackType)
        {
            return ((IProtoTypeSerializer)_proxy.Serializer).HasCallbacks(callbackType);
        }

        bool IProtoTypeSerializer.CanCreateInstance()
        {
            return ((IProtoTypeSerializer)_proxy.Serializer).CanCreateInstance();
        }

#if FEAT_COMPILER
        void IProtoTypeSerializer.EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType)
        {
            ((IProtoTypeSerializer)_proxy.Serializer).EmitCallback(ctx, valueFrom, callbackType);
        }

        void IProtoTypeSerializer.EmitCreateInstance(Compiler.CompilerContext ctx)
        {
            ((IProtoTypeSerializer)_proxy.Serializer).EmitCreateInstance(ctx);
        }
#endif
#if !FEAT_IKVM
        void IProtoTypeSerializer.Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {
            ((IProtoTypeSerializer)_proxy.Serializer).Callback(value, callbackType, context);
        }

        object IProtoTypeSerializer.CreateInstance(ProtoReader source)
        {
            return ((IProtoTypeSerializer)_proxy.Serializer).CreateInstance(source);
        }
#endif
    }
}

#endif