using AqlaSerializer.Meta;

namespace AqlaSerializer
{
    public enum EnhancedMode
    {
        /// <summary>
        /// Peek mode based on member type and <see cref="RuntimeTypeModel"/> settings
        /// </summary>
        NotSpecified = 0,
        /// <summary>
        /// Has null support if appropriate but no reference tracking. Can deserialize data stored using any other EnhancedMode.
        /// </summary>
        Minimal,
        /// <summary>
        /// Standard mode for reference types, includes Nullable
        /// </summary>
        Reference,
        /// <summary>
        /// Indicates that the value should not be traversed recursively, includes Reference
        /// </summary>
        LateReference,
    }
}