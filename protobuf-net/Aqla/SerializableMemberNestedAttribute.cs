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

namespace AqlaSerializer
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class SerializableMemberNestedAttribute : SerializableMemberBaseAttribute
    {
        public SerializableMemberNestedAttribute(int level, MemberFormat format = 0)
            : base(level, format)
        {
        }
    }
}