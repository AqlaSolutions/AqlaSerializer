﻿// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016
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
        private readonly int key;
        private readonly Type type;

        private readonly BclHelpers.NetObjectOptions options;

        public RootDecorator(TypeModel model, Type type, int key, IProtoTypeSerializer serializer)
        {
            _serializer = serializer;
            options = BclHelpers.NetObjectOptions.AsReference | BclHelpers.NetObjectOptions.UseConstructor;
            if (serializer is TupleSerializer)
                options |= BclHelpers.NetObjectOptions.LateSet;
            this.key = key;
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
            SubItemToken token;
            bool isNewObject;
            object r = NetObjectHelpers.ReadNetObject_StartRoot(value, source, key, type, options, out token, out isNewObject);
            if (isNewObject)
            {
                if (_serializer is TagDecorator)
                    source.ReadFieldHeader();
                r = _serializer.Read(value, source);
                ProtoReader.EndSubItem(token, source);
            }
            return r;
        }
        public void Write(object value, ProtoWriter dest)
        {
            bool write;
            SubItemToken t = NetObjectHelpers.WriteNetObject_StartRoot(value, dest, key, options, out write);
            if (write)
            {
                _serializer.Write(value, dest);
                ProtoWriter.EndSubItem(t, dest);
            }
        }
#endif

#if FEAT_COMPILER
        public void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (Local loc = ctx.GetLocalWithValue(type, valueFrom))
            using (Local token = new Local(ctx, ctx.MapType(typeof(SubItemToken))))
            using (Local callResult = new Local(ctx, ctx.MapType(typeof(object))))
            using (Local resultCasted = new Local(ctx, ctx.MapType(type)))
            using (Local doRead = new Local(ctx, ctx.MapType(typeof(bool))))
            {
                ctx.LoadValue(loc);
                //ctx.CastToObject(ctx.MapType(typeof(object)));
                ctx.CastToObject(type);
                ctx.LoadReaderWriter();
                ctx.LoadValue(ctx.MapMetaKeyToCompiledKey(key));
                if (type == ctx.MapType(typeof(object))) ctx.LoadNullRef();
                else ctx.LoadValue(type);
                ctx.LoadValue((int)options);
                ctx.LoadAddress(token, token.Type);
                ctx.LoadAddress(doRead, doRead.Type);

                ctx.EmitCall(ctx.MapType(typeof(NetObjectHelpers)).GetMethod("ReadNetObject_StartRoot"));
                ctx.StoreValue(callResult);

                var doReadFalseMark = ctx.DefineLabel();
                var doReadEndIfMark = ctx.DefineLabel();
                ctx.LoadValue(doRead);
                ctx.BranchIfFalse(doReadFalseMark, false);
                {

                    if (_serializer is TagDecorator)
                    {
                        ctx.LoadReaderWriter();
                        ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod("ReadFieldHeader"));
                        ctx.DiscardValue();
                    }

                    _serializer.EmitRead(ctx, loc);

                    if (_serializer.ReturnsValue)
                        ctx.StoreValue(resultCasted);

                    ctx.LoadValue(token);
                    ctx.LoadReaderWriter();
                    ctx.EmitCall(ctx.MapType(typeof(ProtoReader)).GetMethod("EndSubItem"));

                    if (_serializer.ReturnsValue)
                        ctx.LoadValue(resultCasted);
                }
                ctx.Branch(doReadEndIfMark, true);
                ctx.MarkLabel(doReadFalseMark);
                {
                    if (_serializer.ReturnsValue)
                    {
                        ctx.LoadValue(callResult);
                        ctx.CastFromObject(type);
                    }
                }
                ctx.MarkLabel(doReadEndIfMark);

            }
        }

        public void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (Local loc = ctx.GetLocalWithValue(type, valueFrom))
            using (Local doWrite = new Local(ctx, ctx.MapType(typeof(bool))))
            using (Local token = new Local(ctx, ctx.MapType(typeof(SubItemToken))))
            {
                ctx.LoadValue(loc);
                ctx.CastToObject(type);
                ctx.LoadReaderWriter();
                ctx.LoadValue(ctx.MapMetaKeyToCompiledKey(key));
                ctx.LoadValue((int)options);
                ctx.LoadAddress(doWrite, doWrite.Type);
                ctx.EmitCall(ctx.MapType(typeof(NetObjectHelpers)).GetMethod("WriteNetObject_StartRoot"));
                ctx.StoreValue(token);

                var doWriteFalse = ctx.DefineLabel();
                ctx.LoadValue(doWrite);
                ctx.BranchIfFalse(doWriteFalse, false);
                {
                    _serializer.EmitWrite(ctx, loc);

                    ctx.LoadValue(token);
                    ctx.LoadReaderWriter();
                    ctx.EmitCall(ctx.MapType(typeof(ProtoWriter)).GetMethod("EndSubItem"));
                }
                ctx.MarkLabel(doWriteFalse);

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