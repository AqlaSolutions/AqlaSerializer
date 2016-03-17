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

        /// <summary>
        /// Null wire type allows to write null values in a more compact way but it doesn't present in the official documentation of Protocol Buffers format so it can't be read by most serializers
        /// </summary>
        public bool SuppressNullWireType;

        public static readonly ProtoCompatibilitySettingsValue Default = new ProtoCompatibilitySettingsValue();

        public static readonly ProtoCompatibilitySettingsValue Incompatible = new ProtoCompatibilitySettingsValue();

        public static readonly ProtoCompatibilitySettingsValue FullCompatibility = new ProtoCompatibilitySettingsValue()
        {
            SuppressOwnRootFormat = true,
            SuppressCollectionEnhancedFormat = true,
            SuppressValueEnhancedFormat = true,
            SuppressNullWireType = true
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