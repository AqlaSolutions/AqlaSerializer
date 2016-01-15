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
        public int Value
        {
            get { return enumValue; }
            set { this.enumValue = value; hasValue = true; }
        }

        /// <summary>
        /// Indicates whether this instance has a customised value mapping
        /// </summary>
        /// <returns>true if a specific value is set</returns>
        public bool HasValue() { return hasValue; }

        private bool hasValue;
        private int enumValue;
    }
}
