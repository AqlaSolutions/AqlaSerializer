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
    public struct CollectionSettingsValue
    {
        /// <summary>
        /// Has value if != NotSpecified
        /// </summary>
        public CollectionFormat Format;

        /// <summary>
        /// Within a list / array / etc, the type of object for each item in the list (especially useful with ArrayList).
        /// Has value if != null.
        /// </summary>
        public Type ItemType;

        /// <summary>
        /// Has value if != null
        /// </summary>
        public WireType? PackedWireTypeForRead;
        
        /// /// <summary>
        /// Indicates whether this field should *repace* existing values (the default is false, meaning *append*).
        /// This option only applies to list/array data.
        /// Has value if != null
        /// </summary>
        public bool? Append;

        public static CollectionSettingsValue Merge(CollectionSettingsValue baseValue, CollectionSettingsValue derivedValue)
        {
            var r = derivedValue;
            if (r.Format == CollectionFormat.NotSpecified) r.Format = baseValue.Format;
            if (r.ItemType == null) r.ItemType = baseValue.ItemType;
            if (r.PackedWireTypeForRead == null) r.PackedWireTypeForRead = baseValue.PackedWireTypeForRead;
            if (r.Append == null) r.Append = baseValue.Append;
            return r;
        }
    }
}