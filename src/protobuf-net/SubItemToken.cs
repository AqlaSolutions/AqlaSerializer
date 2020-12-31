// Modified by Vladyslav Taranov for AqlaSerializer, 2016

using System;

namespace AqlaSerializer
{
    /// <summary>
    /// Used to hold particulars relating to nested objects. This is opaque to the caller - simply
    /// give back the token you are given at the end of an object.
    /// </summary>
    public readonly struct SubItemToken
    {
        /// <summary>
        /// See object.ToString()
        /// </summary>
        public override string ToString()
        {
            if (value64 < 0) return $"Group {-value64}";
            if (value64 == long.MaxValue) return "Message (restores to end when ended)";
            return $"Message (restores to value64 when ended)";
        }
        internal readonly long Value64;
        /// <summary>
        /// Used to seek back if written group ended up empty
        /// </summary>
        internal SeekOnEndOrMakeNullFieldCondition? SeekOnEndOrMakeNullField;

        // note: can't really display value64 - it is usually confusing, since
        // it is the *restore* value (previous), not the *current* value

        /// <summary>
        /// See object.GetHashCode()
        /// </summary>
        public override int GetHashCode() => value64.GetHashCode();
        /// <summary>
        /// See object.Equals()
        /// </summary>
        public override bool Equals(object obj) => obj is SubItemToken tok && tok.value64 == value64;
        internal readonly long value64;
        internal SubItemToken(int value) : this((long)value) { }

        internal SubItemToken(long value) : this()
        {
            this.Value64 = value;
        }
    }

    public struct SeekOnEndOrMakeNullFieldCondition
    {
        public long? ThenTrySeekToPosition;
        public long PositionShouldBeEqualTo;
        public int? NullFieldNumber;
    }
}
