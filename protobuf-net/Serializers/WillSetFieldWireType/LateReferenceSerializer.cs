#if !NO_RUNTIME
using System;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
#endif
using System.Diagnostics;
using AltLinq;
using AqlaSerializer.Meta;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;

#endif

namespace AqlaSerializer.Serializers
{
    /// <summary>
    /// Should be used only inside NetObjectValueDecorator with AsReference
    /// </summary>
    sealed class LateReferenceSerializer : IProtoSerializerWithWireType
    {
        readonly Type _type;
        readonly RuntimeTypeModel _model;
        readonly int _typeKey;
        public Type ExpectedType { get; }

        public LateReferenceSerializer(Type type, RuntimeTypeModel model)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (model == null) throw new ArgumentNullException(nameof(model));
            ExpectedType = type;
            _model = model;
            if (Helpers.IsValueType(type))
                throw new ArgumentException("Can't create " + this.GetType().Name + " for non-reference type " + type.Name + "!");
            _typeKey = _model.GetKey(type, true, true);
        }
        
#if !FEAT_IKVM
        public void Write(object value, ProtoWriter dest)
        {
#if DEBUG
            Debug.Assert(value != null);
#endif
            WriteSubTypeForMetaType(_model[_typeKey], value.GetType(), dest);
            ProtoWriter.NoteLateReference(_typeKey, value, dest);
        }

        public void WriteSubTypeForMetaType(MetaType metaType, Type actual, ProtoWriter dest, int recursionLevel = 0)
        {
            if (metaType.Type != actual)
                foreach (var subType in metaType.GetSubtypes())
                {
                    MetaType derivedType = subType.DerivedType;
                    if (derivedType.Type != metaType.Type && Helpers.IsAssignableFrom(derivedType.Type, actual))
                    {
                        if (recursionLevel == 0)
                        {
                            if (derivedType.Type == actual)
                            {
                                ProtoWriter.WriteFieldHeaderComplete(WireType.Variant, dest);
                                ProtoWriter.WriteInt32(subType.FieldNumber + 1, dest);
                                return;
                            }

                            var token = ProtoWriter.StartSubItem(null, true, dest);
                            ProtoWriter.WriteFieldHeaderIgnored(WireType.Variant, dest);
                            ProtoWriter.WriteInt32(subType.FieldNumber + 1, dest);
                            WriteSubTypeForMetaType(derivedType, actual, dest, 1);
                            ProtoWriter.EndSubItem(token, dest);
                        }
                        else
                        {
                            ProtoWriter.WriteFieldHeaderIgnored(WireType.Variant, dest);
                            ProtoWriter.WriteInt32(subType.FieldNumber + 1, dest);
                            WriteSubTypeForMetaType(derivedType, actual, dest, recursionLevel + 1);
                        }
                        return;
                    }
                }

            if (recursionLevel == 0)
            {
                ProtoWriter.WriteFieldHeaderComplete(WireType.Variant, dest);
                ProtoWriter.WriteInt32(0, dest);
            }
        }

        public object Read(object value, ProtoReader source)
        {
            value = ReadNewInstance(_model[_typeKey], value, value?.GetType(), source);
            // each CreateInstance notes object
            ProtoReader.NoteLateReference(_typeKey, value, source);
            return value;
        }


        public object ReadNewInstance(MetaType metaType, object oldValue, Type oldValueType,  ProtoReader source, int recursionLevel = 0)
        {
            SubType[] subTypes = metaType.GetSubtypes();
            int fieldNumber;
            if (recursionLevel == 0 && source.WireType != WireType.String)
            {
                fieldNumber = source.ReadInt32() - 1;
                if (fieldNumber == -1) return oldValue ?? metaType.Serializer.CreateInstance(source);
                MetaType derivedType = subTypes.First(st => st.FieldNumber == fieldNumber).DerivedType;
                if (derivedType.Type == oldValueType) return oldValue;
                return derivedType.Serializer.CreateInstance(source);
            }
            SubItemToken? token = null;
            if (recursionLevel == 0)
                token = ProtoReader.StartSubItem(source);

            if (!ProtoReader.HasSubValue(WireType.Variant, source))
            {
                if (metaType.Type == oldValueType) return oldValue;
                return metaType.Serializer.CreateInstance(source);
            }
            fieldNumber = source.ReadInt32() - 1;
            if (fieldNumber == -1) return metaType.Serializer.CreateInstance(source);

            var r = ReadNewInstance(subTypes.First(st => st.FieldNumber == fieldNumber).DerivedType, oldValue, oldValueType, source, recursionLevel + 1);

            if (token != null)
                ProtoReader.EndSubItem(token.Value, source);
            return r;
        }

#endif

        bool IProtoSerializer.RequiresOldValue => true;
        bool IProtoSerializer.ReturnsValue => true;
#if FEAT_COMPILER
        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
        }

        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
        }
#endif
    }
}

#endif