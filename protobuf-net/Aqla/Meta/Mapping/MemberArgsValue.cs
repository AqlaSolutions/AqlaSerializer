// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
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
    public struct MemberArgsValue
    {
        public bool IsForced { get; set; }
        public MemberInfo Member { get; set; }
        public MetaType.AttributeFamily Family { get; set; }
        public bool AsEnum { get; set; }
        public bool InferTagByName { get; set; }
        public IEnumerable<AttributeMap> PartialMembers { get; set; }
        public int DataMemberOffset { get; set; }
        public bool IgnoreNonWritableForOverwriteCollection { get; set; }
        public RuntimeTypeModel Model { get; set; }
        public AttributeMap[] Attributes { get; set; }
        public AttributeType AllowedAttributes { get; set; }

        public bool CanUse(AttributeType type)
        {
            return (AllowedAttributes & type) == type;
        }

        public bool HasFamily(MetaType.AttributeFamily value)
        {
            return (Family & value) == value;
        }

        public MemberArgsValue(MemberInfo member, AttributeMap[] attributes, AttributeType allowedAttributes, RuntimeTypeModel model)
            : this()
        {
            Attributes = attributes;
            Member = member;
            Model = model;
            AllowedAttributes = allowedAttributes;
        }
    }
}
#endif