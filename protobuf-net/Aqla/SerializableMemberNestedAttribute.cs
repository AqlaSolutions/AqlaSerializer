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
    public class SerializableMemberNestedAttribute : SerializableMemberAttributeBase
    {
        public SerializableMemberNestedAttribute(int level, ValueFormat enhancedFormat = 0)
            : base(level, enhancedFormat)
        {
            if (level <= 0) throw new ArgumentOutOfRangeException(nameof(level), "Should be > 0");
        }
    }
}