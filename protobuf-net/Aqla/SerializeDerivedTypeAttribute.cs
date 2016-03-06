// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.ComponentModel;
using AqlaSerializer;
using AqlaSerializer.Meta;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
#endif
namespace AqlaSerializer
{
    /// <summary>
    /// Indicates the known-types to support for an individual
    /// message. This serializes each level in the hierarchy as
    /// a nested message to retain wire-compatibility with
    /// other protocol-buffer implementations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
    public sealed class SerializeDerivedTypeAttribute : Attribute
    {
        ///<summary>
        /// Creates a new instance of the ProtoIncludeAttribute.
        /// </summary>
        /// <param name="tag">The unique index (within the type) that will identify this data.</param>
        /// <param name="knownType">The additional type to serialize/deserialize.</param>
        public SerializeDerivedTypeAttribute(int tag, System.Type knownType)
            : this(tag, knownType == null ? "" : knownType.AssemblyQualifiedName) { }

        /// <summary>
        /// Creates a new instance of the ProtoIncludeAttribute.
        /// </summary>
        /// <param name="tag">The unique index (within the type) that will identify this data.</param>
        /// <param name="knownTypeName">The additional type to serialize/deserialize.</param>
        public SerializeDerivedTypeAttribute(int tag, string knownTypeName)
        {
            if (tag <= 0) throw new ArgumentOutOfRangeException(nameof(tag), "Tags must be positive integers");
            if (Helpers.IsNullOrEmpty(knownTypeName)) throw new ArgumentNullException(nameof(knownTypeName), "Known type cannot be blank");
            this.Tag = tag;
            this.KnownTypeName = knownTypeName;
        }

        public object ModelId { get; set; }

        /// <summary>
        /// Gets the unique index (within the type) that will identify this data.
        /// </summary>
        public int Tag { get; }

        /// <summary>
        /// Gets the additional type to serialize/deserialize.
        /// </summary>
        public string KnownTypeName { get; }

        /// <summary>
        /// Gets the additional type to serialize/deserialize.
        /// </summary>
        public Type KnownType
        {
            get
            {
                return TypeModel.ResolveKnownType(KnownTypeName, null, null);
            }
        }
    }
}
