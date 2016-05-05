using System;
using System.Collections.Generic;
using System.Globalization;

namespace AqlaSerializer.Meta
{
    public class EnumFlagModelId<T>
        where T : struct
    {
        public EnumFlagModelId(T value)
        {
            var t = value.GetType();
            if (!Helpers.IsEnum(t)) throw new ArgumentException("Expected Enum as a generic argument");
            Value = value;
            _modelIdMask = ExtractEnumLongValue(Value);
        }

        readonly long _modelIdMask;

        public T Value { get; }

        public override bool Equals(object compareTo)
        {
            if (compareTo == null) return false;
            var other = compareTo as EnumFlagModelId<T>;
            if (other != null)
                return other.Value.Equals(Value);

            if (_modelIdMask == 0) return false;

            if (compareTo is Enum)
            {
                long otherMask = ExtractEnumLongValue(compareTo);
                return (_modelIdMask & otherMask) != 0; // just intersection
            }
            try
            {
                object changeType = Convert.ChangeType(compareTo, typeof(long), CultureInfo.InvariantCulture);
                if (changeType == null) return false;
                long otherValue = (long)changeType;
                if (otherValue == 0) return false;
                return (_modelIdMask & otherValue) == otherValue; // modelId contains full other value
            }
            catch (FormatException)
            {
                return false;
            }
            catch (InvalidCastException)
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        static long ExtractEnumLongValue(object value)
        {
            return (long)Convert.ChangeType(Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()), CultureInfo.InvariantCulture), typeof(long), CultureInfo.InvariantCulture);
        }
    }
}