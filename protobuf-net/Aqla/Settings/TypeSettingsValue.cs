using System;
using AqlaSerializer.Settings;
#if FEAT_IKVM
using AqlaSerializer;
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
using AqlaSerializer;

#endif

namespace AqlaSerializer.Settings
{
    public struct TypeSettingsValue
    {
        public string Name;
        public bool EnumPassthru;
        public bool SkipConstructor;
        public bool? PrefixLength;
        public Type ConcreteType;
        public MemberLevelSettingsValue Member;
    }
}