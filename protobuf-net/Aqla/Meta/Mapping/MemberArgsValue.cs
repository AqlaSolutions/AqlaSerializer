// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AltLinq; using System.Linq;
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
        /// <summary>
        /// Static field on enum type
        /// </summary>
        public bool IsEnumValueMember { get; set; }
        public bool InferTagByName { get; set; }
        public IEnumerable<AttributeMap> PartialMembers { get; set; }
        public int DataMemberOffset { get; set; }
        public RuntimeTypeModel Model { get; set; }
        public AttributeMap[] Attributes { get; set; }
        public AttributeType AcceptableAttributes { get; set; }
        
        public bool CanUse(AttributeType type)
        {
            return (AcceptableAttributes & type) == type;
        }

        public bool HasFamily(MetaType.AttributeFamily value)
        {
            return (Family & value) == value;
        }

        public MemberArgsValue(MemberInfo member, RuntimeTypeModel model)
            : this(member, new AttributeMap[0], AttributeType.None, model)
        {
        }

        public MemberArgsValue(MemberInfo member, AttributeMap[] attributes, AttributeType acceptableAttributes, RuntimeTypeModel model)
            : this()
        {
            Attributes = attributes;
            Member = member;
            Model = model;
            AcceptableAttributes = acceptableAttributes;
        }
    }
}
#endif