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
    sealed class NetObjectValueDecorator : IProtoSerializer
    {
        private readonly IProtoSerializer _serializer;
        bool _asReference;
        private readonly Type type;

        private readonly BclHelpers.NetObjectOptions options;

        public NetObjectValueDecorator(TypeModel model, Type type, IProtoSerializer serializer, bool asReference)
        {
            _serializer = serializer;
            //var typeSer = serializer as IProtoTypeSerializer;
            //if (typeSer == null)
            //{
            //    typeSer = new TypeSerializer(model, type, new[] { 1 }, new[] { serializer }, null, true, true, null, type, null)
            //    {
            //        AllowInheritance = false, // TODO save subtype here! good place!
            //        CanCreateInstance = false
            //    };
            //}
            //_serializer = typeSer;
            _asReference = asReference;
            options = BclHelpers.NetObjectOptions.AsReference | BclHelpers.NetObjectOptions.UseConstructor;
            if (serializer is TupleSerializer)
                options |= BclHelpers.NetObjectOptions.LateSet;
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
            object newValue = NetObjectHelpers.ReadNetObject_StartInject(value, source, ref t, options, out token, out shouldEnd, out newObjectKey, out newTypeKey, out isType);
            if (shouldEnd)
            {
                value = newValue;
                newValue = DoRead(value, source);
            }
            NetObjectHelpers.ReadNetObject_EndInject(shouldEnd, newValue, source, value, t, newObjectKey, newTypeKey, isType, options, token);

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
            SubItemToken t = NetObjectHelpers.WriteNetObject_StartInject(value, dest, options, out write);
            if (write)
            {
                // field header written!
                DoWrite(value, dest);
                ProtoWriter.EndSubItem(t, dest);
            }
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
            using (Local value = RequiresOldValue ? ctx.GetLocalWithValue(type, valueFrom) : new Local(ctx, type))
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
                var g = new CodeGen(ctx.RunSharpContext, false);
                var s = ctx.RunSharpContext.StaticFactory;
                g.WriteLine("Enter");

                if (!_asReference)
                {
                    EmitDoRead(g, value, ctx);
                }
                else
                {
                    g.Assign(t.AsOperand, ctx.MapType(type));
                    g.Assign(
                        newValue.AsOperand,
                        s.Invoke(
                            typeof(NetObjectHelpers),
                            "ReadNetObject_StartInject",
                            value.AsOperand,
                            g.Arg(ctx.ArgIndexReadWriter),
                            t.AsOperand,
                            options,
                            token.AsOperand,
                            shouldEnd.AsOperand,
                            newObjectKey.AsOperand,
                            newTypeKey.AsOperand,
                            isType.AsOperand));

                    g.If(shouldEnd.AsOperand);
                    {
                        g.Assign(oldValue.AsOperand, newValue.AsOperand);
                        g.Assign(resultCasted.AsOperand, newValue.AsOperand.Cast(type));
                        EmitDoRead(g, resultCasted, ctx);
                    }
                    g.End();
                    g.Invoke(
                        typeof(NetObjectHelpers),
                        "ReadNetObject_EndInject",
                        shouldEnd.AsOperand,
                        newValue.AsOperand,
                        g.Arg(ctx.ArgIndexReadWriter),
                        oldValue.AsOperand,
                        t.AsOperand,
                        newObjectKey.AsOperand,
                        newTypeKey.AsOperand,
                        isType.AsOperand,
                        options,
                        token.AsOperand);

                }

                if (_serializer.ReturnsValue)
                {
                    ctx.LoadValue(resultCasted);
                }
            }
        }

        void EmitDoRead(CodeGen g, Local value, CompilerContext ctx)
        {
            var s = ctx.RunSharpContext.StaticFactory;
            using (Local t2 = new Local(ctx, ctx.MapType(typeof(SubItemToken))))
            {
                g.Assign(t2.AsOperand, s.Invoke(typeof(ProtoReader), "StartSubItem", g.Arg(ctx.ArgIndexReadWriter)));
                g.Invoke(g.Arg(ctx.ArgIndexReadWriter), "ReadFieldHeader"); // we always expect that value really has tag inside
                _serializer.EmitRead(ctx, value);
                ctx.StoreValue(value);
                g.Invoke(typeof(ProtoReader), "EndSubItem", t2.AsOperand, g.Arg(ctx.ArgIndexReadWriter));
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
                        t.AsOperand,
                        s.Invoke(ctx.MapType(typeof(NetObjectHelpers)), "WriteNetObject_StartInject", value.AsOperand, g.Arg(ctx.ArgIndexReadWriter), options, write.AsOperand));
                    g.If(write.AsOperand);
                    {
                        // field header written!
                        EmitDoWrite(g, value, ctx);
                        g.Invoke(typeof(ProtoWriter), "EndSubItem", t.AsOperand, g.Arg(ctx.ArgIndexReadWriter));
                    }
                    g.End();
                }
            }

        }

        void EmitDoWrite(CodeGen g, Local value, CompilerContext ctx)
        {
            var s = ctx.RunSharpContext.StaticFactory;
            using (Local t2 = new Local(ctx, ctx.MapType(typeof(SubItemToken))))
            {
                g.Assign(t2.AsOperand, s.Invoke(typeof(ProtoWriter), "StartSubItem", null, g.Arg(ctx.ArgIndexReadWriter)));
                _serializer.EmitWrite(ctx, value);
                g.Invoke(typeof(ProtoWriter), "EndSubItem", t2.AsOperand, g.Arg(ctx.ArgIndexReadWriter));
            }
        }
#endif
        //        public bool HasCallbacks(TypeModel.CallbackType callbackType)
        //        {
        //            return _serializer.HasCallbacks(callbackType);
        //        }

        //        public bool CanCreateInstance()
        //        {
        //            return _serializer.CanCreateInstance();
        //        }
        //#if !FEAT_IKVM
        //        public object CreateInstance(ProtoReader source)
        //        {
        //            return _serializer.CreateInstance(source);
        //        }

        //        public void Callback(object value, TypeModel.CallbackType callbackType, SerializationContext context)
        //        {
        //            _serializer.Callback(value, callbackType, context);
        //        }
        //#endif
        //#if FEAT_COMPILER
        //        public void EmitCallback(CompilerContext ctx, Local valueFrom, TypeModel.CallbackType callbackType)
        //        {
        //            _serializer.EmitCallback(ctx, valueFrom, callbackType);
        //        }

        //        public void EmitCreateInstance(CompilerContext ctx)
        //        {
        //            _serializer.EmitCreateInstance(ctx);
        //        }
        //#endif
    }
}
#endif