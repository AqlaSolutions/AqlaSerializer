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

using System.Collections.Generic;

namespace AqlaSerializer.Meta.Mapping
{
    public class MemberState
    {
        public MemberArgsValue Input { get; }
        public int MinAcceptFieldNumber => Input.InferTagByName ? -1 : 1;
        public bool TagIsPinned { get; set; }
        public MemberMainSettingsValue MainValue { get; set; } = new MemberMainSettingsValue();
        public List<MemberLevelSettingsValue?> LevelValues { get; set; } = new List<MemberLevelSettingsValue?>();

        public MemberState(MemberArgsValue input)
        {
            Input = input;
        }
    }
}
#endif