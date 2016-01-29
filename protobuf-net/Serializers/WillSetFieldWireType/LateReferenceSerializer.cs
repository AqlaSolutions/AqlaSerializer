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
    /// <summary>
    /// Should be used only inside NetObjectValueDecorator with AsReference
    /// </summary>
    sealed class LateReferenceSerializer : IProtoSerializerWithWireType
    {
        readonly Type _type;
        readonly RuntimeTypeModel _model;
        TypeSerializer _serializer;
        readonly int _typeKey;
        public Type ExpectedType => _serializer?.ExpectedType ?? _type;
        
        public LateReferenceSerializer(Type type, RuntimeTypeModel model)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (model == null) throw new ArgumentNullException(nameof(model));
            _type = type;
            _model = model;
            if (Helpers.IsValueType(type))
                throw new ArgumentException("Can't create " + this.GetType().Name + " for non-reference type " + type.Name + "!");
            _typeKey = _model.GetKey(type, true, false);
        }

        void InitSerializer()
        {
            // can't get it in ctor because recursion!
            if (_serializer == null) // TODO cast exception
                _serializer = (TypeSerializer)_model[_type].Serializer;
        }

#if !FEAT_IKVM
        public void Write(object value, ProtoWriter dest)
        {
#if DEBUG
            Debug.Assert(value != null);
#endif
            InitSerializer();
            TypeSerializer serializer = _serializer;
            IProtoSerializerWithWireType next;
            int fn;

            if (serializer.GetMoreSpecificSerializer(value, out next, out fn))
            {
                var s = next as TypeSerializer;
                if (s != null)
                {
                    serializer = s;
                    IProtoSerializerWithWireType next2;
                    int fn2;
                    if (serializer.GetMoreSpecificSerializer(value, out next2, out fn2))
                    {
                        // 2+ subtypes - packed list
                        var token = ProtoWriter.StartSubItem(value, true, dest);
                        ProtoWriter.WriteFieldHeaderIgnored(WireType.Variant, dest);
                        ProtoWriter.WriteInt32(fn + 1, dest);
                        ProtoWriter.WriteFieldHeaderIgnored(WireType.Variant, dest);
                        ProtoWriter.WriteInt32(fn2 + 1, dest);
                        while (serializer != null && serializer.GetMoreSpecificSerializer(value, out next, out fn) && next != null)
                        {
                            ProtoWriter.WriteFieldHeaderIgnored(WireType.Variant, dest);
                            ProtoWriter.WriteInt32(fn + 1, dest);
                            serializer = next as TypeSerializer;
                        }
                        ProtoWriter.EndSubItem(token, dest);
                    }
                }
                else
                {
                    // 1 subtype, no group
                    ProtoWriter.WriteFieldHeaderComplete(WireType.Variant, dest);
                    ProtoWriter.WriteInt32(fn + 1, dest);
                }
            }
            else // no subtypes
            {
                ProtoWriter.WriteFieldHeaderComplete(WireType.Variant, dest);
                ProtoWriter.WriteInt32(0, dest);
            }
            ProtoWriter.NoteLateReference(_typeKey, value, dest);
        }

        public object Read(object value, ProtoReader source)
        {
            InitSerializer();
            SubItemToken token = new SubItemToken();
            bool isGroup = source.WireType == WireType.String;
            if (isGroup)
                token = ProtoReader.StartSubItem(source);
            TypeSerializer subTypeSerializer = _serializer;
            IProtoTypeSerializer finalSerializer = subTypeSerializer;
            int subTypeNumber = source.ReadInt32();
            while (subTypeNumber > 0)
            {
                if (value == null && subTypeSerializer != null)
                {
                    finalSerializer = subTypeSerializer.GetSubTypeSerializer(subTypeNumber - 1);
                    // may be tuple serializer for an examle, it still can create instance!
                    subTypeSerializer = finalSerializer as TypeSerializer;
                }
                if (!isGroup) break;
                // packed, no headers
                if (!ProtoReader.HasSubValue(WireType.Variant, source)) break;
                subTypeNumber = source.ReadInt32();
            }
            if (isGroup)
                ProtoReader.EndSubItem(token, source);

            if (value == null)
                value = finalSerializer.CreateInstance(source);
            ProtoReader.NoteLateReference(_typeKey, value, source);
            return value;
        }
#endif

        bool IProtoSerializer.RequiresOldValue => true;
        bool IProtoSerializer.ReturnsValue => true;
#if FEAT_COMPILER
        void IProtoSerializer.EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            InitSerializer();
        }
        
        void IProtoSerializer.EmitRead(Compiler.CompilerContext ctx, Compiler.Local valueFrom)
        {
            InitSerializer();
        }
#endif
    }
}