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
        public CollectionFormat Format { get; set; }

        /// <summary>
        /// Has value if != null
        /// </summary>
        public Type ItemType { get; set; }

        /// <summary>
        /// Has value if != null
        /// </summary>
        public WireType? PackedWireTypeForRead { get; set; }
        
        /// <summary>
        /// Has value if != null
        /// </summary>
        public bool? Append { get; set; }

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