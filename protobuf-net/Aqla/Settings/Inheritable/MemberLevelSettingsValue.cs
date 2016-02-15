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
        public MemberFormat MemberFormat { get; set; }

        /// <summary>
        /// Has value if != null
        /// </summary>
        public bool? WriteAsLateReference { get; set; }
        
        /// <summary>
        /// Has value if != null
        /// </summary>
        public BinaryDataFormat? ContentBinaryFormat { get; set; }
        
        /// <summary>
        /// Has value if != null
        /// </summary>
        public Type CollectionConcreteType { get; set; }
        
        /// <summary>
        /// Not inherited
        /// </summary>
        public bool DynamicType { get; set; }

        public CollectionSettingsValue Collection;

        public static MemberLevelSettingsValue Merge(MemberLevelSettingsValue baseValue, MemberLevelSettingsValue derivedValue)
        {
            var r = derivedValue;
            if (r.MemberFormat == MemberFormat.NotSpecified) r.MemberFormat = baseValue.MemberFormat;
            if (r.WriteAsLateReference == null) r.WriteAsLateReference = baseValue.WriteAsLateReference;
            if (r.ContentBinaryFormat == null) r.ContentBinaryFormat = baseValue.ContentBinaryFormat;
            if (r.CollectionConcreteType == null) r.CollectionConcreteType = baseValue.CollectionConcreteType;
            r.Collection = CollectionSettingsValue.Merge(baseValue, derivedValue);
            return r;
        }
    }
}