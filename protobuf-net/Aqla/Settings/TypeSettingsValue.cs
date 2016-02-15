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
        public string Name { get; set; }
        public bool EnumPassthru { get; set; }
        public bool SkipConstructor { get; set; }
        public bool? PrefixLength { get; set; }
        public Type ConcreteType { get; set; }
        public MemberLevelSettingsValue Member;

    }
}