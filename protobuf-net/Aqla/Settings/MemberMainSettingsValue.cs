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

        public override string ToString()
        {
            string tag = Tag == int.MinValue ? "" : (Tag.ToString() + " ");
            return "Member " + tag + Name;
        }
    }
}