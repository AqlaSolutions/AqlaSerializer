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
        public bool? EnumPassthru;
        public bool SkipConstructor;
        public bool IgnoreListHandling;
        public bool? PrefixLength;
        public Type ConcreteType;
        public bool IsAutoTuple;
        public MemberLevelSettingsValue Member;

        public TypeSettingsValue GetInitializedToValueOrDefault()
        {
            var x = this;
            x.PrefixLength = x.PrefixLength.GetValueOrDefault(true);
            x.EnumPassthru = x.EnumPassthru.GetValueOrDefault();
            x.Member = x.Member.GetInitializedToValueOrDefault();
            return x;
        }
    }
}