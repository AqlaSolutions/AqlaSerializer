using System.Collections.Generic;

namespace AqlaSerializer.Settings
{
    [SerializableType(DefaultMemberFormat = MemberFormat.Aqla, WriteLateReferenceDefault = true)]
    class X
    {
        [SerializableMember(1, ForModel=, MemberFormat.NotSpecified, Nested = 0, WriteContentFormat = BinaryDataFormat.Default, WriteAsLateReference = true, PrefixLength)]
        [SerializableMember(MemberFormat.Compact, Nested = 1, WritePacked, ConcreteType)]
        [SerializableCollection(CollectionFormat.NotSpecified, Nested = 1, Append)] // NO IS PACKED
        public int[][] Member { get; set; }
    }
}