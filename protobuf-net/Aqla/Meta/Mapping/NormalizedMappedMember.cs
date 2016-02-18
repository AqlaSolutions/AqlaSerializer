// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AltLinq;
using AqlaSerializer;
using AqlaSerializer.Meta;
using AqlaSerializer.Meta.Mapping;
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

namespace AqlaSerializer
{
    public class NormalizedMappedMember
    {
        public bool IsReadOnly { get; set; }
        public bool ForcedTag { get; set; }
        public MemberState MappingState { get; set; }

        public NormalizedMappedMember(MemberState mappingState)
        {
            MappingState = mappingState;
        }

        public MemberMainSettingsValue MainValue { get { return MappingState.MainValue; } set { MappingState.MainValue = value; } }

        public int Tag
        {
            get { return MappingState.MainValue.Tag; }
            set
            {
                var m = MappingState.MainValue;
                m.Tag = value;
                MappingState.MainValue = m;
            }
        }

        public MemberInfo Member => MappingState.Input.Member;
        public string Name => MappingState.MainValue.Name;

        public MemberLevelSettingsValue this[int nestedLevel]
        {
            get
            {
                if (nestedLevel >= MappingState.LevelValues.Count) return new MemberLevelSettingsValue();
                return MappingState.LevelValues[nestedLevel].GetValueOrDefault();
            }
            set
            {
                while (nestedLevel >= MappingState.LevelValues.Count)
                    MappingState.LevelValues.Add(new MemberLevelSettingsValue());

                MappingState.LevelValues[nestedLevel] = value;
            }
        }
    }
}

#endif