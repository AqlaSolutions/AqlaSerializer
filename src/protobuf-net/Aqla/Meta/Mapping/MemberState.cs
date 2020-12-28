// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
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

using System.Collections.Generic;

namespace AqlaSerializer.Meta.Mapping
{
    public class MemberState
    {
        public MemberArgsValue Input { get; }
        public RuntimeTypeModel Model => Input.Model;
        public MemberInfo Member => Input.Member;
        public int MinAcceptFieldNumber => Input.InferTagByName ? -1 : 1;
        public bool TagIsPinned { get; set; }
        public MemberMainSettingsValue MainValue { get; set; }
        public ValueSerializationSettings SerializationSettings { get; } = new ValueSerializationSettings();
        
        public MemberState(MemberArgsValue input)
        {
            Input = input;
        }

        public override string ToString()
        {
            return MainValue.ToString() + " (" + Input.Member + ")";
        }
    }
}
#endif