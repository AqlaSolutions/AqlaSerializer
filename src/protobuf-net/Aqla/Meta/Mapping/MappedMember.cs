﻿// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AltLinq; using System.Linq;
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
    public class MappedMember : IComparable<MappedMember>
    {
        public bool ForcedTag { get; set; }
        public MemberState MappingState { get; set; }

        public MappedMember(MemberState mappingState)
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

        public FieldInfo BackedField => MappingState.Input.BackingField;
        public MemberInfo Member => MappingState.Input.Member;

        public string Name => MappingState.MainValue.Name;
        
        /// <summary>
        /// Compare with another NormalizedMappedMember for sorting purposes
        /// </summary>
        int IComparable<MappedMember>.CompareTo(MappedMember other)
        {
            if (other == null) return -1;
            if ((object)this == (object)other) return 0;
            int result = Tag.CompareTo(other.Tag);
            if (result == 0) result = string.CompareOrdinal(Name, other.Name);
            return result;
        }

        public override string ToString()
        {
            return MappingState?.ToString() ?? base.ToString();
        }
    }
}

#endif