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
    public class TypeState
    {
        public Type Type => Input.Type;
        public RuntimeTypeModel Model => Input.Model;

        public TypeArgsValue Input { get; set; }

        public TypeSettingsValue SettingsValue { get; set; }
        public ImplicitFieldsMode ImplicitFields { get; set; }
        public int DataMemberOffset { get; set; }
        public bool ImplicitAqla { get; set; }
        public bool ImplicitOnlyWriteable { get; set; }
        public int ImplicitFirstTag { get; set; } = 1;
        public bool InferTagByName { get; set; }
        public bool AsEnum { get; set; }

        public List<DerivedTypeCandidate> DerivedTypes { get; set; } = new List<DerivedTypeCandidate>();

        public List<AttributeMap> PartialMembers { get; set; } = new List<AttributeMap>();

        public TypeState(TypeArgsValue input)
        {
            Input = input;
        }
    }
}

#endif