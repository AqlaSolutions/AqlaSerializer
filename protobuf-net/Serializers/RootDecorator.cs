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
        readonly bool _protoCompatibility;
        readonly TypeModel _model;
        private readonly IProtoTypeSerializer _serializer;

        public RootDecorator(Type type, bool wrap, bool protoCompatibility, IProtoTypeSerializer serializer, TypeModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            _protoCompatibility = protoCompatibility;
            _model = model;
            _serializer = wrap ? new NetObjectValueDecorator(serializer, Helpers.GetNullableUnderlyingType(type) != null, !Helpers.IsValueType(type), model) : serializer;
        }

        public Type ExpectedType => _serializer.ExpectedType;
        public bool ReturnsValue => _serializer.ReturnsValue;
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
            ProtoWriter.WriteFieldHeaderBegin(ListHelpers.FieldItem, dest);
            _serializer.Write(value, dest);
            int typeKey;
            object v;
            int refKey;
            while (ProtoWriter.TryGetNextLateReference(out typeKey, out v, out refKey, dest))
            {
                ProtoWriter.WriteFieldHeaderBegin(refKey + 1, dest);
                _model.Serialize(typeKey, v, dest, false);
            }
            ProtoWriter.EndSubItem(rootToken, dest);
        }

        public object Read(object value, ProtoReader source)
        {
            ProtoReader.ExpectRoot(source);
            if (_protoCompatibility)
                return _serializer.Read(value, source);
            
            var rootToken = ProtoReader.StartSubItem(source);
            if (source.ReadFieldHeader() != ListHelpers.FieldItem) throw new ProtoException("Expected field for root object");
            var r = _serializer.Read(value, source);
            int typeKey;
            object v;
            int expectedRefKey;
            while (ProtoReader.TryGetNextLateReference(out typeKey, out v, out expectedRefKey, source))
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
                if (!ReferenceEquals(_model.Deserialize(typeKey, v, source, false), v)) throw new ProtoException("Late reference changed during deserializing");
            }
            ProtoReader.EndSubItem(rootToken, true, source);
            return r;
        }
#endif

#if FEAT_COMPILER
        public void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {

        }

        public void EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {

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