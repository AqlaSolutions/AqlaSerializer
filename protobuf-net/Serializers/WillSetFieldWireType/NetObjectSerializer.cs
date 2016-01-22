// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
using TriAxis.RunSharp;
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

    sealed class NetObjectSerializer : IProtoSerializerWithWireType
    {
        private readonly int key;
        private readonly Type type;

        private readonly BclHelpers.NetObjectOptions options;

        public NetObjectSerializer(TypeModel model, Type type, int key, BclHelpers.NetObjectOptions options)
        {
            bool dynamicType = (options & BclHelpers.NetObjectOptions.DynamicType) != 0;
            Debug.Assert(dynamicType || key != -1);
            this.key = dynamicType ? -1 : key;
            this.type = dynamicType ? model.MapType(typeof(object)) : type;
            this.options = options;
        }

        public Type ExpectedType
        {
            get { return type; }
        }
        public bool ReturnsValue
        {
            get { return true; }
        }
        public bool RequiresOldValue
        {
            get { return true; }
        }
#if !FEAT_IKVM
        public object Read(object value, ProtoReader source)
        {
            var r = NetObjectHelpers.ReadNetObject(value, source, key, type == typeof(object) ? null : type, options);
            if (Helpers.IsValueType(type) && r == null) return Activator.CreateInstance(type);
            return r;
        }
        public void Write(object value, ProtoWriter dest)
        {
            NetObjectHelpers.WriteNetObject(value, dest, key, options);
        }
#endif

#if FEAT_COMPILER
        public void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (var resultBoxed = new Local(ctx, ctx.MapType(typeof(object))))
            using (var resultUnboxed = new Local(ctx, type))
            {
                ctx.LoadValue(valueFrom);
                ctx.CastToObject(type);
                ctx.LoadReaderWriter();
                ctx.LoadValue(ctx.MapMetaKeyToCompiledKey(key));
                if (type == ctx.MapType(typeof(object))) ctx.LoadNullRef(); else ctx.LoadValue(type);
                ctx.LoadValue((int)options);
                ctx.EmitCall(ctx.MapType(typeof(NetObjectHelpers)).GetMethod("ReadNetObject"));
                // unboxing will convert null or value to nullable automatically
                if (type.IsValueType && Helpers.GetNullableUnderlyingType(type) == null)
                {
                    // TODO do we need to ensure this? versioning - change between reference type/nullable and value type
                    ctx.StoreValue(resultBoxed);
                    ctx.StoreValueOrDefaultFromObject(resultBoxed, resultUnboxed);
                    ctx.LoadValue(resultUnboxed);
                }
                else
                    ctx.CastFromObject(type);
            }
        }
        public void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            // nullable will be boxed as null or as value automatically, e.g. you will never get "123?" there
            ctx.LoadValue(valueFrom);
            ctx.CastToObject(type);
            ctx.LoadReaderWriter();
            ctx.LoadValue(ctx.MapMetaKeyToCompiledKey(key));
            ctx.LoadValue((int)options);
            ctx.EmitCall(ctx.MapType(typeof(NetObjectHelpers)).GetMethod("WriteNetObject"));
        }
#endif
    }
}
#endif