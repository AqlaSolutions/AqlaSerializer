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
        public void WriteDebugSchema(IDebugSchemaBuilder builder)
        {
            using (builder.SingleTailDecorator(this, _protoCompatibility ? "compatibility" : ""))
                _serializer.WriteDebugSchema(builder);
        }
        
        public bool DemandWireTypeStabilityStatus() => !_protoCompatibility;
        readonly bool _enableReferenceVersioningSeeking;
        readonly bool _protoCompatibility;
        private readonly IProtoTypeSerializer _serializer;

        public RootDecorator(Type type, bool wrap, bool enableReferenceVersioningSeeking, bool protoCompatibility, IProtoTypeSerializer serializer, RuntimeTypeModel model)
        {
            _enableReferenceVersioningSeeking = enableReferenceVersioningSeeking;
            _protoCompatibility = protoCompatibility;
            _serializer = wrap ? new NetObjectValueDecorator(serializer, Helpers.GetNullableUnderlyingType(type) != null, !Helpers.IsValueType(type), false, false, model) : serializer;
        }

        public Type ExpectedType => _serializer.ExpectedType;
        public bool RequiresOldValue => _serializer.RequiresOldValue;

#if !FEAT_IKVM
        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.ExpectRoot(dest);
            if (_protoCompatibility)
            {
                _serializer.Write(value, dest);
                return;
            }
            
            var rootToken = ProtoWriter.StartSubItem(null, false, dest);
            RootHelpers.WriteOwnHeader(dest);
            _serializer.Write(value, dest);
            RootHelpers.WriteOwnFooter(dest);

            ProtoWriter.EndSubItem(rootToken, dest);
        }

        public object Read(object value, ProtoReader source)
        {
            ProtoReader.ExpectRoot(source);
            if (_protoCompatibility)
                return _serializer.Read(value, source);
            
            var rootToken = ProtoReader.StartSubItem(source);
            int formatVersion = RootHelpers.ReadOwnHeader(_enableReferenceVersioningSeeking, source);
            var r = _serializer.Read(value, source);
            RootHelpers.ReadOwnFooter(_enableReferenceVersioningSeeking, formatVersion, source);
            ProtoReader.EndSubItem(rootToken, true, source);
            return r;
        }

#endif

#if FEAT_COMPILER
        public bool EmitReadReturnsValue => _serializer.EmitReadReturnsValue;
        public void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                var g = ctx.G;

                g.Writer.ExpectRoot();
                if (_protoCompatibility)
                {
                    _serializer.EmitWrite(ctx, valueFrom);
                    return;
                }

                using (var rootToken = ctx.Local(typeof(SubItemToken)))
                {
                    g.Assign(rootToken, g.WriterFunc.StartSubItem(null, false));
                    g.Invoke(typeof(RootHelpers), nameof(RootHelpers.WriteOwnHeader), g.ArgReaderWriter());
                    _serializer.EmitWrite(ctx, valueFrom);
                    g.Invoke(typeof(RootHelpers), nameof(RootHelpers.WriteOwnFooter), g.ArgReaderWriter());
                    g.Writer.EndSubItem(rootToken);
                }
            }
        }

        public void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            using (ctx.StartDebugBlockAuto(this))
            {
                var g = ctx.G;

                g.Reader.ExpectRoot();
                if (_protoCompatibility)
                {
                    _serializer.EmitRead(ctx, _serializer.RequiresOldValue ? valueFrom : null);
                    return;
                }

                using (var rootToken = ctx.Local(typeof(SubItemToken)))
                using (var formatVersion = ctx.Local(typeof(int)))
                using (var value = ctx.GetLocalWithValueForEmitRead(this, valueFrom))
                {
                    g.Assign(rootToken, g.ReaderFunc.StartSubItem());
                    g.Assign(
                        formatVersion,
                        g.StaticFactory.Invoke(
                            typeof(RootHelpers),
                            nameof(RootHelpers.ReadOwnHeader),
                            _enableReferenceVersioningSeeking,
                            g.ArgReaderWriter()));
                    _serializer.EmitRead(ctx, _serializer.RequiresOldValue ? value : null);
                    if (_serializer.EmitReadReturnsValue)
                        g.Assign(value, g.GetStackValueOperand(ExpectedType));
                    g.Invoke(typeof(RootHelpers), nameof(RootHelpers.ReadOwnFooter), _enableReferenceVersioningSeeking, formatVersion, g.ArgReaderWriter());
                    g.Reader.EndSubItem(rootToken, true);

                    if (EmitReadReturnsValue)
                        ctx.LoadValue(value);
                }
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