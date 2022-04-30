// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;

#if PLAT_BINARYFORMATTER && !(PHONE8)
using System.Runtime.Serialization;
#endif
namespace AqlaSerializer
{
    /// <summary>
    /// Indicates an error during serialization/deserialization of a proto stream.
    /// </summary>
#if PLAT_BINARYFORMATTER && !(PHONE8)
    [Serializable]
#endif
    public class ProtoException : Exception
    {
        /// <summary>Creates a new ProtoException instance.</summary>
        public ProtoException() { }

        /// <summary>Creates a new ProtoException instance.</summary>
        public ProtoException(string message) : base(message) { }

        /// <summary>Creates a new ProtoException instance.</summary>
        public ProtoException(string message, Exception innerException) : base(message, innerException) { }

#if PLAT_BINARYFORMATTER && !(PHONE8)
        /// <summary>Creates a new ProtoException instance.</summary>
        protected ProtoException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }
}
