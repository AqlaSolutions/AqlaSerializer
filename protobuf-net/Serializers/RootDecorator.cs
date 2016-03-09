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
        readonly bool _protoCompatibility;
        private readonly IProtoTypeSerializer _serializer;

        public RootDecorator(Type type, bool wrap, bool protoCompatibility, IProtoTypeSerializer serializer, RuntimeTypeModel model)
        {
            _protoCompatibility = protoCompatibility;
            _serializer = wrap ? new NetObjectValueDecorator(serializer, Helpers.GetNullableUnderlyingType(type) != null, !Helpers.IsValueType(type), false, false, model) : serializer;
        }

        public Type ExpectedType => _serializer.ExpectedType;
        public bool RequiresOldValue => _serializer.RequiresOldValue;

        const int CurrentFormatVersion = 3;

#if !FEAT_IKVM
        public void Write(object value, ProtoWriter dest)
        {
            ProtoWriter.ExpectRoot(dest);
            if (_protoCompatibility)
            {
                _serializer.Write(value, dest);
                return;
            }

            int typeKey;
            object obj;
            int refKey;
            var rootToken = ProtoWriter.StartSubItem(null, false, dest);
            ProtoWriter.WriteFieldHeaderBegin(CurrentFormatVersion, dest);
            _serializer.Write(value, dest);
            while (ProtoWriter.TryGetNextLateReference(out typeKey, out obj, out refKey, dest))
            {
                ProtoWriter.WriteFieldHeaderBegin(refKey + 1, dest);
                ProtoWriter.WriteRecursionSafeObject(obj, typeKey, dest);
            }
            ProtoWriter.EndSubItem(rootToken, dest);
        }

        public object Read(object value, ProtoReader source)
        {
            ProtoReader.ExpectRoot(source);
            if (_protoCompatibility)
                return _serializer.Read(value, source);

            int typeKey;
            object obj;
            int expectedRefKey;
            var rootToken = ProtoReader.StartSubItem(source);
            int formatVersion = source.ReadFieldHeader();
            if (formatVersion != CurrentFormatVersion) throw new ProtoException("Wrong format version, required " + CurrentFormatVersion + " but actual " + formatVersion);
            var r = _serializer.Read(value, source);
            while (ProtoReader.TryGetNextLateReference(out typeKey, out obj, out expectedRefKey, source))
            {
                int actualRefKey;
                do
                {
                    actualRefKey = source.ReadFieldHeader() - 1;
                    if (actualRefKey != expectedRefKey)
                    {
                        if (actualRefKey <= -1) throw new ProtoException("Expected field for late reference");
                        // should go only up
                        if (actualRefKey > expectedRefKey) throw new ProtoException("Mismatched order of late reference objects");
                        source.SkipField(); // refKey < num
                    }
                } while (actualRefKey < expectedRefKey);
                object lateObj = ProtoReader.ReadObject(obj, typeKey, source);
                if (!ReferenceEquals(lateObj, obj)) throw new ProtoException("Late reference changed during deserializing");
            }
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
                using (var typeKey = ctx.Local(typeof(int)))
                using (var obj = ctx.Local(typeof(object)))
                using (var refKey = ctx.Local(typeof(int)))
                {
                    g.Assign(rootToken, g.WriterFunc.StartSubItem(null, false));
                    g.Writer.WriteFieldHeaderBegin(CurrentFormatVersion);
                    _serializer.EmitWrite(ctx, valueFrom);
                    g.While(g.StaticFactory.Invoke(typeof(ProtoWriter), nameof(ProtoWriter.TryGetNextLateReference), typeKey, obj, refKey, g.ArgReaderWriter()));
                    {
                        g.Writer.WriteFieldHeaderBegin(refKey.AsOperand + 1);
                        g.Writer.WriteRecursionSafeObject(obj, typeKey);
                    }
                    g.End();
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
                using (var typeKey = ctx.Local(typeof(int)))
                using (var obj = ctx.Local(typeof(object)))
                using (var formatVersion = ctx.Local(typeof(int)))
                using (var expectedRefKey = ctx.Local(typeof(int)))
                using (var actualRefKey = ctx.Local(typeof(int)))
                using (var value = ctx.GetLocalWithValueForEmitRead(this, valueFrom))
                {
                    g.Assign(rootToken, g.ReaderFunc.StartSubItem());
                    g.Assign(formatVersion, g.ReaderFunc.ReadFieldHeader_int());
                    g.If(formatVersion.AsOperand != CurrentFormatVersion);
                    {
                        g.ThrowProtoException("Wrong format version, required " + CurrentFormatVersion + " but actual " + formatVersion.AsOperand);
                    }
                    g.End();
                    _serializer.EmitRead(ctx, _serializer.RequiresOldValue ? value : null);
                    if (_serializer.EmitReadReturnsValue)
                        g.Assign(value, g.GetStackValueOperand(ExpectedType));

                    g.While(g.StaticFactory.Invoke(typeof(ProtoReader), nameof(ProtoReader.TryGetNextLateReference), typeKey, obj, expectedRefKey, g.ArgReaderWriter()));
                    {
                        g.DoWhile();
                        {
                            g.Assign(actualRefKey, g.ReaderFunc.ReadFieldHeader_int() - 1);
                            g.If(actualRefKey.AsOperand != expectedRefKey.AsOperand);
                            {
                                g.If(actualRefKey.AsOperand <= -1);
                                {
                                    g.ThrowProtoException("Expected field for late reference");
                                }
                                g.End();
                                g.If(actualRefKey.AsOperand > expectedRefKey.AsOperand);
                                {
                                    g.ThrowProtoException("Mismatched order of late reference objects");
                                }
                                g.End();
                                g.Reader.SkipField();
                            }
                            g.End();
                        }
                        g.EndDoWhile(actualRefKey.AsOperand < expectedRefKey.AsOperand);
                        g.If(!g.StaticFactory.InvokeReferenceEquals(g.ReaderFunc.ReadObject(obj, typeKey), obj));
                        {
                            g.ThrowProtoException("Late reference changed during deserializing");
                        }
                        g.End();
                    }
                    g.End();
                    g.Reader.EndSubItem(rootToken);

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