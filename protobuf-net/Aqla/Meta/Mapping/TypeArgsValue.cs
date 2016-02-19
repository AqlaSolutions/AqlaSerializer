// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Text;
using AltLinq;
using AqlaSerializer;
using AqlaSerializer.Meta;
using AqlaSerializer.Serializers;
using AqlaSerializer.Settings;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#if FEAT_COMPILER
using IKVM.Reflection.Emit;
#endif
#else
using System.Reflection;
#if FEAT_COMPILER
using System.Reflection.Emit;

#endif
#endif

namespace AqlaSerializer.Meta.Mapping
{
    public struct TypeArgsValue
    {
        public Type Type { get; set; }
        public MetaType.AttributeFamily Family { get; set; }
        public RuntimeTypeModel Model { get; set; }
        public AttributeMap[] Attributes { get; set; }
        public AttributeType AcceptableAttributes { get; set; }
        public ImplicitFieldsMode ImplicitFallbackMode { get; set; }

        public bool CanUse(AttributeType type)
        {
            return (AcceptableAttributes & type) == type;
        }

        public bool HasFamily(MetaType.AttributeFamily value)
        {
            return (Family & value) == value;
        }

        public TypeArgsValue(Type type, RuntimeTypeModel model)
            : this(type, new AttributeMap[0], AttributeType.None, model)
        {
        }

        public TypeArgsValue(Type type, AttributeMap[] attributes, AttributeType acceptableAttributes, RuntimeTypeModel model)
            : this()
        {
            Attributes = attributes;
            Type = type;
            Model = model;
            AcceptableAttributes = acceptableAttributes;
        }
    }
}
#endif