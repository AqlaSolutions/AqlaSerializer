#if !NO_RUNTIME && !SILVERLIGHT
using System;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
#endif
using System.Diagnostics;
using AltLinq;
using System.Linq;
using AqlaSerializer.Meta;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#if FEAT_COMPILER
using IKVM.Reflection.Emit;
#endif
#else
using System.Reflection;
#if FEAT_COMPILER
using System.Reflection.Emit;
#endif
#endif

namespace AqlaSerializer.Serializers
{
    // SILVERLIGHT doesn't support methodpairs-thing so can't call other serializer methods directly
    sealed class LateReferenceSerializerProxyCaller : IProtoSerializerWithWireType
    {
        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            _proxy.LateReferenceSerializer.WriteDebugSchema(builder);
        }

        public bool DemandWireTypeStabilityStatus() => false;
        public WireType? ConstantWireType => null;
        readonly ILateReferenceSerializerProxy _proxy;
        readonly int _typeKey;
        
        public Type ExpectedType { get; }
        
        public LateReferenceSerializerProxyCaller(ILateReferenceSerializerProxy proxy, Type type, int typeKey)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (Helpers.IsValueType(type))
                throw new ArgumentException("Can't create " + this.GetType().Name + " for non-reference type " + type.Name + "!");
            ExpectedType = type;
            _proxy = proxy;
            _typeKey = typeKey;
        }

#if !FEAT_IKVM
        public void Write(object value, ProtoWriter dest)
        {
            _proxy.LateReferenceSerializer.Write(value, dest);
        }

        public object Read(object value, ProtoReader source)
        {
            return _proxy.LateReferenceSerializer.Read(value, source);
        }

#endif
        public bool RequiresOldValue => true;
#if FEAT_COMPILER
        public bool EmitReadReturnsValue => true;

        public void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                IProtoSerializerWithWireType ser = ThrowIfNotGenerated(_proxy.LateReferenceSerializer);
                if (!ctx.SupportsMultipleMethods)
                {
                    ser.EmitWrite(ctx, valueFrom);
                    return;
                }

                ctx.LoadValue(valueFrom);
                ctx.EmitWriteCall(ctx, ctx.GetDedicatedMethod(_typeKey).LateReferencePair.Serialize);
            }
        }

        public void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                IProtoSerializerWithWireType ser = ThrowIfNotGenerated(_proxy.LateReferenceSerializer);
                if (!ctx.SupportsMultipleMethods)
                {
                    ser.EmitRead(ctx, valueFrom);
                    return;
                }
                ctx.LoadValue(valueFrom);
                ctx.EmitReadCall(ctx, ExpectedType, ctx.GetDedicatedMethod(_typeKey).LateReferencePair.Deserialize);
            }
        }

        IProtoSerializerWithWireType ThrowIfNotGenerated(IProtoSerializerWithWireType serializer)
        {
            if (serializer == null)
            {
                if (_proxy.LateReferenceSerializerBuildException != null)
                    throw new ProtoException("Late reference was not generated for type " + ExpectedType, _proxy.LateReferenceSerializerBuildException);

                throw new NotSupportedException(LateReferenceSerializer.NotSupportedMessage + " Type = " + ExpectedType);
            }
            return serializer;
        }
#endif
    }
}
#endif