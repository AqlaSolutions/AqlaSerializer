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
        public bool? EnhancedFormat;

        /// <summary>
        /// Has value if != NotSpecified
        /// </summary>
        public EnhancedMode EnhancedWriteMode;

        /// <summary>
        /// Embeds the type information into the stream, allowing usage with types not known in advance.
        /// Has value if != null.
        /// </summary>
        public bool? WriteAsDynamicType;

        /// <summary>
        /// Specifies the rules used to process the field; this is used to determine the most appropriate
        /// wite-type, but also to describe subtypes <i>within</i> that wire-type (such as SignedVariant).
        /// Has value if != null.
        /// </summary>
        public BinaryDataFormat? ContentBinaryFormatHint;
        
        /// <summary>
        /// For abstract types (IList etc), the type of concrete object to create (if required). Has value if != null.
        /// </summary>
        public Type CollectionConcreteType;

        public CollectionSettingsValue Collection;

        public static MemberLevelSettingsValue Merge(MemberLevelSettingsValue baseValue, MemberLevelSettingsValue derivedValue)
        {
            var r = derivedValue;
            if (r.EnhancedFormat == null) r.EnhancedFormat = baseValue.EnhancedFormat;
            if (r.EnhancedWriteMode == EnhancedMode.NotSpecified) r.EnhancedWriteMode = baseValue.EnhancedWriteMode;
            if (r.ContentBinaryFormatHint == null) r.ContentBinaryFormatHint = baseValue.ContentBinaryFormatHint;
            if (r.WriteAsDynamicType == null) r.WriteAsDynamicType = baseValue.WriteAsDynamicType;
            if (r.CollectionConcreteType == null) r.CollectionConcreteType = baseValue.CollectionConcreteType;
            r.Collection = CollectionSettingsValue.Merge(baseValue.Collection, derivedValue.Collection);
            return r;
        }
    }
}