// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Threading;
using AltLinq; using System.Linq;
using AqlaSerializer;
using AqlaSerializer.Internal;
using AqlaSerializer.Meta.Mapping;
using AqlaSerializer.Serializers;
using AqlaSerializer.Settings;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#if FEAT_COMPILER
using IKVM.Reflection.Emit;
#endif
#else
using System.Reflection;
#if FEAT_COMPILER
using System.Reflection.Emit;
#endif
#endif


namespace AqlaSerializer.Meta
{
    partial class MetaType
    {
        private BasicList _subTypes;
        private BasicList _subTypesSimple;

        /// <summary>
        /// Gets the base-type for this type
        /// </summary>
        public MetaType BaseType { get; private set; }

        public Type GetBaseType()
        {
#if WINRT
            return _typeInfo.BaseType;
#else
            return Type.BaseType;
#endif
        }

        /// <summary>
        /// Returns the SubType instances associated with this type
        /// </summary>
        public SubType[] GetSubtypes()
        {
            if (_subTypes == null || _subTypes.Count == 0) return new SubType[0];
            SubType[] arr = new SubType[_subTypes.Count];
            _subTypes.CopyTo(arr, 0);
            Array.Sort(arr, SubType.Comparer.Default);
            return arr;
        }

        public bool IsValidSubType(Type subType)
        {
#if WINRT
            if (!CanHaveSubType(_typeInfo)) return false;
#else
            if (!CanHaveSubType(Type)) return false;
#endif
#if WINRT
            return _typeInfo.IsAssignableFrom(subType.GetTypeInfo());
#else
            return Type.IsAssignableFrom(subType);
#endif
        }
#if WINRT
        public static bool CanHaveSubType(Type type)
        {
            return CanHaveSubType(type.GetTypeInfo());
        }

        public static bool CanHaveSubType(TypeInfo type)
#else
        public static bool CanHaveSubType(Type type)
#endif
        {
#if WINRT
            if ((type.IsClass || type.IsInterface) && !type.IsSealed)
            {
#else
            if ((type.IsClass || type.IsInterface) && !type.IsSealed)
            {
                return true;
#endif

            }
            return false;
        }
        
        /// <summary>
        /// Adds a known sub-type to the inheritance model
        /// </summary>
        public MetaType AddSubType(int fieldNumber, Type derivedType)
        {
            return AddSubType(fieldNumber, derivedType, BinaryDataFormat.Default);
        }
        /// <summary>
        /// Adds a known sub-type to the inheritance model
        /// </summary>
        public MetaType AddSubType(int fieldNumber, Type derivedType, BinaryDataFormat dataFormat)
        {
            if (derivedType == null) throw new ArgumentNullException(nameof(derivedType));
            if (fieldNumber < 1) throw new ArgumentOutOfRangeException(nameof(fieldNumber));

            if (Type.IsArray)
                throw new ArgumentException("An array has inbuilt behaviour and cannot be subclassed");

            if (derivedType.IsArray)
                throw new ArgumentException("An array has inbuilt behaviour and cannot be as used as a subclass");

#if WINRT
            if (!CanHaveSubType(_typeInfo)) {
#else
            if (!CanHaveSubType(Type))
            {
#endif
                throw new InvalidOperationException("Sub-types can only be added to non-sealed classes");
            }
            if (!IsValidSubType(derivedType))
            {
                throw new ArgumentException(derivedType.Name + " is not a valid sub-type of " + Type.Name, nameof(derivedType));
            }

            if (_subTypesSimple != null && _subTypesSimple.Contains(derivedType)) return this; // already exists

            if (!IsFieldFree(fieldNumber))
                throw new ArgumentException(string.Format("FieldNumber {0} was already taken in type {1}, can't add sub-type {2}", fieldNumber, Type.Name, derivedType.Name), nameof(fieldNumber));

            if (_subTypesSimple == null) _subTypesSimple = new BasicList();
            _subTypesSimple.Add(derivedType);

            MetaType derivedMeta = _model[derivedType];
            ThrowIfFrozen();
            derivedMeta.ThrowIfFrozen();
            SubType subType = new SubType(fieldNumber, derivedMeta, dataFormat);
            ThrowIfFrozen();

            derivedMeta.SetBaseType(this); // includes ThrowIfFrozen
            if (_subTypes == null) _subTypes = new BasicList();
            _subTypes.Add(subType);

            return this;
        }
        private void SetBaseType(MetaType baseType)
        {
            if (baseType == null) throw new ArgumentNullException("baseType");
            if (this.BaseType == baseType) return;
            if (this.BaseType != null) throw new InvalidOperationException("A type can only participate in one inheritance hierarchy");

            MetaType type = baseType;
            while (type != null)
            {
                if (ReferenceEquals(type, this)) throw new InvalidOperationException("Cyclic inheritance is not allowed");
                type = type.BaseType;
            }
            this.BaseType = baseType;
        }

    }
}
#endif