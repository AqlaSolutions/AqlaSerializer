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
    public struct MemberMainSettingsValue
    {
        public int Tag;
        public string Name;
        public bool IsRequiredInSchema;
        public object DefaultValue;
#if !NO_RUNTIME
        public MemberInfo Member;
#endif
    }
}