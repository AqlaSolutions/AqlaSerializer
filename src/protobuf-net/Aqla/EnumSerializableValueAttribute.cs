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
        int? _value;

        /// <summary>
        /// Gets or sets the specific value to use for this enum during serialization.
        /// </summary>
        public int Value { get { return _value.Value; } set { _value = value; } }

        public bool HasValue() => _value.HasValue;

        public string Name { get; set; }

        public object ModelId { get; set; }

        // it's used on enums, no need for levels
    }
}
