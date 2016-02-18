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
        /// Still has null support
        /// </summary>
        Nullable,
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