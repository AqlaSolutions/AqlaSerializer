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
        public int Tag { get; set; }
        public string Name { get; set; }
        public bool IsRequiredInSchema { get; set; }
        public object DefaultValue { get; set; }
#if !NO_RUNTIME
        public MemberInfo Member { get; set; }
#endif
    }
}