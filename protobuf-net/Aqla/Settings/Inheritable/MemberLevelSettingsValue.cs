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
        public Type EffectiveType;
        
        /// <summary>
        /// Has value if != NotSpecified
        /// </summary>
        public ValueFormat Format;

        /// <summary>
        /// Used when no enhanced format specified, required to support not as reference default behavior on legacy proto members of non-contract types.
        /// </summary>
        public ValueFormat DefaultFormatFallback;
        
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
            // we don't change PackedWireTypeForRead because any non-null value would be not default
            x.ContentBinaryFormatHint = x.ContentBinaryFormatHint.GetValueOrDefault();
            x.WriteAsDynamicType = x.WriteAsDynamicType.GetValueOrDefault();
            return x;
        }
        
        public MemberLevelSettingsValue MakeDefaultNestedLevelForRootType()
        {
            var x = this;

            x.Collection.ItemType = null;
            x.Collection.PackedWireTypeForRead = null;
            x.Collection.ConcreteType = null;
            x.EffectiveType = null;

            return x;
        }

        public MemberLevelSettingsValue MakeDefaultNestedLevelForLegacyMember()
        {
            return MakeDefaultNestedLevelForRootType();
        }

        public MemberLevelSettingsValue MakeDefaultNestedLevel()
        {
            var x = this.MakeDefaultNestedLevelForRootType();

            x.Format = ValueFormat.NotSpecified;
            x.ContentBinaryFormatHint = null;
            x.WriteAsDynamicType = null;
            x.DefaultFormatFallback = ValueFormat.NotSpecified;
            
            return x;
        }

        public static MemberLevelSettingsValue Merge(MemberLevelSettingsValue baseValue, MemberLevelSettingsValue derivedValue)
        {
            var r = derivedValue;
            if (r.Format == ValueFormat.NotSpecified) r.Format = baseValue.Format;
            if (r.ContentBinaryFormatHint == null) r.ContentBinaryFormatHint = baseValue.ContentBinaryFormatHint;
            if (r.WriteAsDynamicType == null) r.WriteAsDynamicType = baseValue.WriteAsDynamicType;
            if (r.DefaultFormatFallback == null) r.DefaultFormatFallback = baseValue.DefaultFormatFallback;
            r.Collection = CollectionSettingsValue.Merge(baseValue.Collection, derivedValue.Collection);
            return r;
        }

        public override string ToString()
        {
            string s = "LevelSettings of type " + EffectiveType;
            if (Format != ValueFormat.Compact)
            {
                if (Format != ValueFormat.NotSpecified)
                    s += ", " + Format;
            }
            else if (Format == ValueFormat.Compact)
                s += ", compact";

            if (Collection.ItemType != null)
                s += ", itemType " + Collection.ItemType;
            return s;
        }
    }
}