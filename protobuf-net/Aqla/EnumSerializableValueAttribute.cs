// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;

namespace AqlaSerializer
{
    /// <summary>
    /// Used to define protocol-buffer specific behavior for
    /// enumerated values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class EnumSerializableValueAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the specific value to use for this enum during serialization.
        /// </summary>
        public int Value { get; set; }

        public string Name { get; set; }

        public object ModelId { get; set; }

        // it's used on enums, no need for levels
    }
}
