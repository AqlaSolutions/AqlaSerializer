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
    sealed class NetObjectValueDecorator : IProtoTypeSerializer
    {
        readonly IProtoSerializer _serializer;
        readonly bool _asReference;
        readonly Type type;

        readonly BclHelpers.NetObjectOptions _options;

        public NetObjectValueDecorator(Type type, IProtoSerializer serializer, bool asReference)
        {
            _serializer = serializer;
            //wrapping a type makes too much complexity for just one ReadFieldHeader call
            //var typeSer = serializer as IProtoTypeSerializer;
            _asReference = asReference;
            _options = BclHelpers.NetObjectOptions.AsReference | BclHelpers.NetObjectOptions.UseConstructor;
            if (serializer is TupleSerializer)
                _options |= BclHelpers.NetObjectOptions.LateSet;
            this.type = type;
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
            if (!_asReference)
            {
                return DoRead(value, source);
            }
            SubItemToken token;
            bool shouldEnd;
            bool isType;
            int newTypeKey;
            int newObjectKey;
            var t = type;
            object newValue = NetObjectHelpers.ReadNetObject_StartInject(value, source, ref t, _options, out token, out shouldEnd, out newObjectKey, out newTypeKey, out isType);
            if (shouldEnd)
            {
                value = newValue;
                newValue = DoRead(value, source);
            }
            NetObjectHelpers.ReadNetObject_EndInject(shouldEnd, newValue, source, value, t, newObjectKey, newTypeKey, isType, _options, token);

            return newValue;
        }

        object DoRead(object value, ProtoReader source)
        {
            var t2 = ProtoReader.StartSubItem(source);
            source.ReadFieldHeader(); // we always expect that value really has tag inside
            value = _serializer.Read(value, source);
            ProtoReader.EndSubItem(t2, source);
            return value;
        }

        public void Write(object value, ProtoWriter dest)
        {
            if (!_asReference)
            {
                DoWrite(value, dest);
                return;
            }
            bool write;
            SubItemToken t = NetObjectHelpers.WriteNetObject_StartInject(value, dest, _options, out write);
            if (write)
            {
                // field header written!
                DoWrite(value, dest);
            }
            ProtoWriter.EndSubItem(t, dest);
        }

        void DoWrite(object value, ProtoWriter dest)
        {
            var t2 = ProtoWriter.StartSubItem(null, dest);
            _serializer.Write(value, dest);
            ProtoWriter.EndSubItem(t2, dest);
        }
#endif

#if FEAT_COMPILER
        public void EmitRead(CompilerContext ctx, Local valueFrom)
        {
            var g = new CodeGen(ctx.RunSharpContext, false);
            var s = ctx.RunSharpContext.StaticFactory;

            using (Local value = RequiresOldValue ? ctx.GetLocalWithValue(type, valueFrom) : new Local(ctx, type))
            {
                using (Local token = new Local(ctx, ctx.MapType(typeof(SubItemToken))))
                using (Local shouldEnd = new Local(ctx, ctx.MapType(typeof(bool))))
                using (Local isType = new Local(ctx, ctx.MapType(typeof(bool))))
                using (Local newTypeKey = new Local(ctx, ctx.MapType(typeof(int))))
                using (Local newObjectKey = new Local(ctx, ctx.MapType(typeof(int))))
                using (Local t = new Local(ctx, ctx.MapType(typeof(System.Type))))
                using (Local newValue = new Local(ctx, ctx.MapType(typeof(object))))
                using (Local oldValue = new Local(ctx, ctx.MapType(typeof(object))))
                using (Local resultCasted = new Local(ctx, ctx.MapType(type)))
                {
                    if (!RequiresOldValue)
                    {
                        ctx.LoadNullRef();
                        ctx.StoreValue(value);
                    }

                    if (!_asReference)
                    {
                        EmitDoRead(g, value, ctx);
                    }
                    else
                    {
                        g.Assign(t, ctx.MapType(type));
                        g.Assign(
                            newValue,
                            s.Invoke(
                                typeof(NetObjectHelpers),
                                nameof(NetObjectHelpers.ReadNetObject_StartInject),
                                value,
                                g.Arg(ctx.ArgIndexReadWriter),
                                t,
                                _options,
                                token,
                                shouldEnd,
                                newObjectKey,
                                newTypeKey,
                                isType));

                        g.If(shouldEnd);
                        {
                            g.Assign(oldValue, newValue);
                            g.Assign(resultCasted, newValue.AsOperand.Cast(type));
                            EmitDoRead(g, resultCasted, ctx);
                        }
                        g.End();
                        g.Invoke(
                            typeof(NetObjectHelpers),
                            nameof(NetObjectHelpers.ReadNetObject_EndInject),
                            shouldEnd,
                            newValue,
                            g.Arg(ctx.ArgIndexReadWriter),
                            oldValue,
                            t,
                            newObjectKey,
                            newTypeKey,
                            isType,
                            _options,
                            token);

                    }

                    if (_serializer.ReturnsValue)
                    {
                        ctx.LoadValue(resultCasted);
                    }
                }
            }
        }

        void EmitDoRead(CodeGen g, Local value, CompilerContext ctx)
        {
            var s = ctx.RunSharpContext.StaticFactory;
            using (Local t2 = new Local(ctx, ctx.MapType(typeof(SubItemToken))))
            {
                g.Assign(t2, s.Invoke(typeof(ProtoReader), nameof(ProtoReader.StartSubItem), g.Arg(ctx.ArgIndexReadWriter)));
                g.Invoke(g.Arg(ctx.ArgIndexReadWriter), nameof(ProtoReader.ReadFieldHeader)); // we always expect that value really has tag inside
                _serializer.EmitRead(ctx, value);
                ctx.StoreValue(value);
                g.Invoke(typeof(ProtoReader), nameof(ProtoReader.EndSubItem), t2, g.Arg(ctx.ArgIndexReadWriter));
            }
        }

        public void EmitWrite(CompilerContext ctx, Local valueFrom)
        {
            using (Local value = ctx.GetLocalWithValue(type, valueFrom))
            {
                var g = new CodeGen(ctx.RunSharpContext, false);
                if (!_asReference)
                {
                    EmitDoWrite(g, value, ctx);
                    return;
                }
                using (Local write = new Local(ctx, ctx.MapType(typeof(bool))))
                using (Local t = new Local(ctx, ctx.MapType(typeof(SubItemToken))))
                {
                    var s = ctx.RunSharpContext.StaticFactory;
                    g.Assign(
                        t,
                        s.Invoke(ctx.MapType(typeof(NetObjectHelpers)), nameof(NetObjectHelpers.WriteNetObject_StartInject), value, g.Arg(ctx.ArgIndexReadWriter), _options, write));
                    g.If(write);
                    {
                        // field header written!
                        EmitDoWrite(g, value, ctx);
                    }
                    g.End();
                    g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.EndSubItem), t, g.Arg(ctx.ArgIndexReadWriter));
                }
            }

        }

        void EmitDoWrite(CodeGen g, Local value, CompilerContext ctx)
        {
            var s = ctx.RunSharpContext.StaticFactory;
            using (Local t2 = new Local(ctx, ctx.MapType(typeof(SubItemToken))))
            {
                g.Assign(t2, s.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.StartSubItem), null, g.Arg(ctx.ArgIndexReadWriter)));
                _serializer.EmitWrite(ctx, value);
                g.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.EndSubItem), t2, g.Arg(ctx.ArgIndexReadWriter));
            }
        }
#endif

        public bool HasCallbacks(TypeModel.CallbackType callbackType)
        {
            IProtoTypeSerializer pts = _serializer as IProtoTypeSerializer;
            return pts != null && pts.HasCallbacks(callbackType);
        }

        public bool CanCreateInstance()
        {
            IProtoTypeSerializer pts = _serializer as IProtoTypeSerializer;
            return pts != null && pts.CanCreateInstance();
        }
#if !FEAT_IKVM
        public object CreateInstance(ProtoReader source)
        {
            return ((IProtoTypeSerializer)_serializer).CreateInstance(source);
        }
        public void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        {
            IProtoTypeSerializer pts = _serializer as IProtoTypeSerializer;
            if (pts != null) pts.Callback(value, callbackType, context);
        }
#endif
#if FEAT_COMPILER
        public void EmitCallback(Compiler.CompilerContext ctx, Compiler.Local valueFrom, TypeModel.CallbackType callbackType)
        {
            // we only expect this to be invoked if HasCallbacks returned true, so implicitly _serializer
            // **must** be of the correct type
            ((IProtoTypeSerializer)_serializer).EmitCallback(ctx, valueFrom, callbackType);
        }
        public void EmitCreateInstance(Compiler.CompilerContext ctx)
        {
            ((IProtoTypeSerializer)_serializer).EmitCreateInstance(ctx);
        }
#endif
    }
}
#endif