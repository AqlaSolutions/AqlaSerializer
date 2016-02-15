using AqlaSerializer.Meta;

namespace AqlaSerializer
{
    /// <summary>
    /// Used to specify member format which affects supported features and output size
    /// </summary>
    public enum MemberFormat
    {
        /// <summary>
        /// Peek format based on member type and <see cref="RuntimeTypeModel"/> settings. Will try to choose Aqla when its settings are enabled.
        /// </summary>
        NotSpecified = 0,
        /// <summary>
        /// Write and read as plain field without advanced features. Versioning won't support switching to <see cref="Enhanced"/> format. 
        /// Not supported settings will be ignored.
        /// </summary>
        Compact,
        /// <summary>
        /// Use reference and null support if applicable. Versioning won't support switching to <see cref="Compact"/> format.
        /// Supports <see cref="SerializableMemberAttribute.WriteAsLateReference"/> and throws exception when set but can't write this way. 
        /// Supports <see cref="SerializableMemberAttribute.DynamicType"/>. 
        /// Not supported settings will be ignored.
        /// </summary>
        /// <remarks>The reason why WriteAsLateReference and DynamicType are on properties is because they're considered compatible with </remarks>
        Enhanced,
    }
}