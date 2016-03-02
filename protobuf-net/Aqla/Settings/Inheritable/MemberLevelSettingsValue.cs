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
        internal Type EffectiveType;

        public Type TryGetEffectiveType() => EffectiveType;

        /// <summary>
        /// Has value if != NotSpecified
        /// </summary>
        public bool? EnhancedFormat;

        /// <summary>
        /// Used when no registered EffectiveType found to get default enhanced format, required to support not as reference default behavior on legacy proto members of non-contract types.
        /// </summary>
        public bool? EnhancedFormatDefaultFallback;

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
        
        public CollectionSettingsValue Collection;

        public MemberLevelSettingsValue GetInitializedToValueOrDefault()
        {
            var x = this;
            x.Collection.Append = x.Collection.Append.GetValueOrDefault();
            x.Collection.PackedWireTypeForRead = x.Collection.PackedWireTypeForRead.GetValueOrDefault();
            x.ContentBinaryFormatHint = x.ContentBinaryFormatHint.GetValueOrDefault();
            x.EnhancedFormat = x.EnhancedFormat.GetValueOrDefault();
            x.WriteAsDynamicType = x.WriteAsDynamicType.GetValueOrDefault();
            return x;
        }

        public MemberLevelSettingsValue MakeDefaultNestedLevel()
        {
            var x = this;
            x.Collection.ItemType = null;
            x.Collection.PackedWireTypeForRead = null;
            x.Collection.ConcreteType = null;
            x.EffectiveType = null;
            return x;
        }

        public static MemberLevelSettingsValue Merge(MemberLevelSettingsValue baseValue, MemberLevelSettingsValue derivedValue)
        {
            var r = derivedValue;
            if (r.EnhancedFormat == null) r.EnhancedFormat = baseValue.EnhancedFormat;
            if (r.EnhancedWriteMode == EnhancedMode.NotSpecified) r.EnhancedWriteMode = baseValue.EnhancedWriteMode;
            if (r.ContentBinaryFormatHint == null) r.ContentBinaryFormatHint = baseValue.ContentBinaryFormatHint;
            if (r.WriteAsDynamicType == null) r.WriteAsDynamicType = baseValue.WriteAsDynamicType;
            if (r.EnhancedFormatDefaultFallback == null) r.EnhancedFormatDefaultFallback = baseValue.EnhancedFormatDefaultFallback;
            r.Collection = CollectionSettingsValue.Merge(baseValue.Collection, derivedValue.Collection);
            return r;
        }

        public override string ToString()
        {
            string s = "LevelSettings of type " + EffectiveType;
            if (EnhancedFormat == true)
            {
                if (EnhancedWriteMode != EnhancedMode.NotSpecified)
                    s += ", " + EnhancedWriteMode;
                else
                    s += ", enhanced";
            }
            else if (EnhancedFormat == false)
                s += ", compact";

            if (Collection.ItemType != null)
                s += ", itemType " + Collection.ItemType;
            return s;
        }
    }
}