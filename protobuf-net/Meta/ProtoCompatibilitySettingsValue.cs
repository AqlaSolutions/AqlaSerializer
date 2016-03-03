using System;

namespace AqlaSerializer.Meta
{
    /// <summary>
    /// Settings of a Google Protocol Buffers compatibility mode; mind that compatibility modes are not compatible between themselves so changing these settings will make you unable to read your previously written data
    /// </summary>
    public struct ProtoCompatibilitySettingsValue : ICloneable
    {
        /// <summary>
        /// Own root format is required for LateReference write mode
        /// </summary>
        public bool SuppressOwnRootFormat;
        
        /// <summary>
        /// Enhanced value format is necessary for null handling and reference tracking
        /// </summary>
        public bool SuppressValueEnhancedFormat;

        /// <summary>
        /// See <see cref="CollectionFormat"/>
        /// </summary>
        public bool SuppressCollectionEnhancedFormat;

        public static readonly ProtoCompatibilitySettingsValue Default = new ProtoCompatibilitySettingsValue();

        public static readonly ProtoCompatibilitySettingsValue Incompatible = new ProtoCompatibilitySettingsValue();

        public static readonly ProtoCompatibilitySettingsValue FullCompatibility = new ProtoCompatibilitySettingsValue()
        {
            SuppressOwnRootFormat = true,
            SuppressCollectionEnhancedFormat = true,
            SuppressValueEnhancedFormat = true
        };

        object ICloneable.Clone()
        {
            return Clone();
        }

        public ProtoCompatibilitySettingsValue Clone()
        {
            return this;
        }
    }
}