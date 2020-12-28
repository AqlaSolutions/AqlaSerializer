using AqlaSerializer.Meta;

namespace AqlaSerializer
{
    public enum ValueFormat
    {
        /// <summary>
        /// Peek format based on member type and <see cref="RuntimeTypeModel"/> settings
        /// </summary>
        NotSpecified = 0,
        /// <summary>
        /// Has null support if appropriate but no reference tracking. Can deserialize data stored using <see cref="Reference"/> or <see cref="LateReference"/> formats. Not compatible with <see cref="Compact"/>.
        /// </summary>
        MinimalEnhancement,
        /// <summary>
        /// Standard mode for reference types, includes and compatible with <see cref="MinimalEnhancement"/>.
        /// </summary>
        Reference,
        /// <summary>
        /// Indicates that the value should not be traversed recursively, includes and compatible with <see cref="Reference"/>.
        /// </summary>
        LateReference,
        /// <summary>
        /// Use compact format, compatible with Google Protocol Buffers, NOT compatible with <see cref="MinimalEnhancement"/>, <see cref="Reference"/>, <see cref="LateReference"/>.
        /// </summary>
        Compact,
    }
}