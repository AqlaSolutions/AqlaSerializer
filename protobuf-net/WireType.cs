// Modified by Vladyslav Taranov for AqlaSerializer, 2016
namespace AqlaSerializer
{
    /// <summary>
    /// Indicates the encoding used to represent an individual value in a protobuf stream
    /// </summary>
    public enum WireType
    {
        /// <summary>
        /// Represents an error condition
        /// </summary>
        None = -1,

        /// <summary>
        /// Base-128 variant-length encoding
        /// </summary>
        Variant = 0,

        /// <summary>
        /// Fixed-length 8-byte encoding
        /// </summary>
        Fixed64 = 1,

        /// <summary>
        /// Length-variant-prefixed encoding (may be used for nested or root object to add length prefix)
        /// </summary>
        String = 2,

        /// <summary>
        /// Indicates the start of a group (nested object without length prefix but stream rewriting required; use it for small "wrappers" around real objects to minimize data movement in stream)
        /// </summary>
        StartGroup = 3,

        /// <summary>
        /// Indicates the end of a group
        /// </summary>
        EndGroup = 4,

        /// <summary>
        /// Fixed-length 4-byte encoding, may be used as root length prefix but not for nested objects
        /// </summary>10
        Fixed32 = 5,

        /// <summary>
        /// This is not a formal wire-type in the "protocol buffers" spec, but
        /// denotes a variant integer that should be interpreted using
        /// zig-zag semantics (so -ve numbers aren't a significant overhead)
        /// </summary>
        SignedVariant = WireType.Variant | 1 << 3, // [1000], [1] is not written, so [000] is read as variant

        /// <summary>
        /// No field content expected
        /// </summary>
        Null = 7,

        
    }
}
