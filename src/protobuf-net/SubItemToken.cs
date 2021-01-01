// Modified by Vladyslav Taranov for AqlaSerializer, 2016

using System;

namespace AqlaSerializer
{
    /// <summary>
    /// Used to hold particulars relating to nested objects. This is opaque to the caller - simply
    /// give back the token you are given at the end of an object.
    /// </summary>
    public struct SubItemToken
    {
        internal readonly long Value64;
        /// <summary>
        /// Used to seek back if written group ended up empty
        /// </summary>
        internal SeekOnEndOrMakeNullFieldCondition? SeekOnEndOrMakeNullField;
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
