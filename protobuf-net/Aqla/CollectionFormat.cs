using AqlaSerializer.Meta;

namespace AqlaSerializer
{
    /// <summary>
    /// Used to specify collection member format which affects supported scenarios
    /// </summary>
    public enum CollectionFormat
    {
        /// <summary>
        /// Peek format based on member type and <see cref="RuntimeTypeModel"/> settings.
        /// </summary>
        NotSpecified = 0,
        /// <summary>
        /// Doesn't support some scenarios, use only for compatibility with Google Protocol Buffers. Versioning won't support switching to other formats. 
        /// </summary>
        /// <remarks>
        /// Writes in packed encoding when appropriate which can save lots of space for repeated primitive values but only applies 
        /// to list/array data of primitive types (int, double, etc). 
        /// </remarks>
        Protobuf,
        /// <summary>
        /// The same as <see cref="Protobuf"/> but less size efficient for primitive types (int, double, etc), use only for compatibility reasons. Versioning won't support switching to others formats. 
        /// </summary>
        /// <remarks>The reason why it's not on property is because I want to underline that it's a different non-compatible format.</remarks>
        ProtobufNotPacked,
        /// <summary>
        /// Recommended: stores list subtype information, differs null/empty state, allows referencing array from inside itself. Versioning won't support switching to other formats.
        /// </summary>
        Enhanced,
    }
}