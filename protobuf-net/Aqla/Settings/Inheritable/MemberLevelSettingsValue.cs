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
    public struct MemberLevelSettingsValue
    {
        /// <summary>
        /// Has value if != NotSpecified
        /// </summary>
        public MemberFormat MemberFormat;

        /// <summary>
        /// Has value if != NotSpecified
        /// </summary>
        public EnhancedMode EnhancedWriteMode;
        
        /// <summary>
        /// Has value if != null
        /// </summary>
        public bool? WriteAsDynamicType;

        /// <summary>
        /// Has value if != null
        /// </summary>
        public BinaryDataFormat? ContentBinaryFormatHint;

        /// <summary>
        /// Has value if != null
        /// </summary>
        public Type CollectionConcreteType;

        public CollectionSettingsValue Collection;

        public static MemberLevelSettingsValue Merge(MemberLevelSettingsValue baseValue, MemberLevelSettingsValue derivedValue)
        {
            var r = derivedValue;
            if (r.MemberFormat == MemberFormat.NotSpecified) r.MemberFormat = baseValue.MemberFormat;
            if (r.EnhancedWriteMode == EnhancedMode.NotSpecified) r.EnhancedWriteMode = baseValue.EnhancedWriteMode;
            if (r.ContentBinaryFormatHint == null) r.ContentBinaryFormatHint = baseValue.ContentBinaryFormatHint;
            if (r.WriteAsDynamicType == null) r.WriteAsDynamicType = baseValue.WriteAsDynamicType;
            if (r.CollectionConcreteType == null) r.CollectionConcreteType = baseValue.CollectionConcreteType;
            r.Collection = CollectionSettingsValue.Merge(baseValue, derivedValue);
            return r;
        }
    }
}