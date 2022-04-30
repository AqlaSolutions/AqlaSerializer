// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using AqlaSerializer.Serializers;

namespace AqlaSerializer.Meta
{
    /// <summary>
    /// Represents an inherited type in a type hierarchy.
    /// </summary>
    public sealed class SubType
    {
        internal sealed class Comparer : System.Collections.IComparer, System.Collections.Generic.IComparer<SubType>
        {
            public static readonly Comparer Default = new Comparer();
            public int Compare(object x, object y)
            {
                return Compare(x as SubType, y as SubType);
            }
            public int Compare(SubType x, SubType y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                return x.FieldNumber.CompareTo(y.FieldNumber);
            }
        }

        /// <summary>
        /// The field-number that is used to encapsulate the data (as a nested
        /// message) for the derived dype.
        /// </summary>
        public int FieldNumber { get; }

        /// <summary>
        /// The sub-type to be considered.
        /// </summary>
        public MetaType DerivedType { get; }

        /// <summary>
        /// Creates a new SubType instance.
        /// </summary>
        /// <param name="fieldNumber">The field-number that is used to encapsulate the data (as a nested
        /// message) for the derived dype.</param>
        /// <param name="derivedType">The sub-type to be considered.</param>
        public SubType(int fieldNumber, MetaType derivedType)
        {
            if (derivedType == null) throw new ArgumentNullException(nameof(derivedType));
            if (fieldNumber <= 0) throw new ArgumentOutOfRangeException(nameof(fieldNumber));
            this.FieldNumber = fieldNumber;
            this.DerivedType = derivedType;
        }
        
        private IProtoSerializerWithWireType _serializer;
        internal IProtoSerializerWithWireType GetSerializer(RuntimeTypeModel model) => _serializer ?? (_serializer = BuildSerializer(model));

        private IProtoSerializerWithWireType BuildSerializer(RuntimeTypeModel model)
        {
            // note the caller here is MetaType.BuildSerializer, which already has the sync-lock
            return new ModelTypeSerializer(DerivedType.Type, DerivedType.GetKey(false, false), DerivedType, model);
        }
    }
}
#endif