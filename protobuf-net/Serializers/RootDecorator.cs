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
        public bool DemandWireTypeStabilityStatus() => !_protoCompatibility;
        readonly bool _protoCompatibility;
        readonly TypeModel _model;
        private readonly IProtoTypeSerializer _serializer;

        public RootDecorator(Type type, bool wrap, bool protoCompatibility, IProtoTypeSerializer serializer, RuntimeTypeModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            _protoCompatibility = protoCompatibility;
            _model = model;
            _serializer = wrap ? new NetObjectValueDecorator(serializer, Helpers.GetNullableUnderlyingType(type) != null, !Helpers.IsValueType(type), model) : serializer;
        }

        public Type ExpectedType => _serializer.ExpectedType;
        public bool ReturnsValue => _serializer.ReturnsValue;
        public bool RequiresOldValue => _serializer.RequiresOldValue;

        const int CurrentFormatVersion = 1;

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
            ProtoWriter.WriteFieldHeaderIgnored(WireType.Variant, dest);
            ProtoWriter.WriteInt32(CurrentFormatVersion, dest);
            ProtoWriter.WriteFieldHeaderBegin(ListHelpers.FieldItem, dest);
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
            if (!ProtoReader.HasSubValue(WireType.Variant, source)) throw new ProtoException("Expected format version field");
            int formatVersion = source.ReadInt32();
            if (formatVersion != CurrentFormatVersion) throw new ProtoException("Wrong format version, required " + CurrentFormatVersion + " but actual " + formatVersion);
            if (source.ReadFieldHeader() != ListHelpers.FieldItem) throw new ProtoException("Expected field for root object");
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
                if (!ReferenceEquals(ProtoReader.ReadObject(obj, typeKey, source), obj)) throw new ProtoException("Late reference changed during deserializing");
            }
            ProtoReader.EndSubItem(rootToken, true, source);
            return r;
        }
#endif

#if FEAT_COMPILER
        public void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
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
                g.Writer.WriteFieldHeaderIgnored(WireType.Variant);
                g.Writer.WriteInt32(CurrentFormatVersion);
                g.Writer.WriteFieldHeaderBegin(ListHelpers.FieldItem);
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

        public void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
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
                g.If(!g.ReaderFunc.HasSubValue_bool(WireType.Variant));
                {
                    g.ThrowProtoException("Expected format version field");
                }
                g.End();
                g.Assign(formatVersion, g.ReaderFunc.ReadInt32());
                g.If(formatVersion.AsOperand != CurrentFormatVersion);
                {
                    g.ThrowProtoException("Wrong format version, required " + CurrentFormatVersion + " but actual " + formatVersion);
                }
                g.End();
                g.If(g.ReaderFunc.ReadFieldHeader_int() != ListHelpers.FieldItem);
                {
                    g.ThrowProtoException("Expected field for root object");
                }
                g.End();
                _serializer.EmitRead(ctx, _serializer.RequiresOldValue ? value : null);
                if (_serializer.ReturnsValue)
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
                
                if (ReturnsValue)
                    ctx.LoadValue(value);
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