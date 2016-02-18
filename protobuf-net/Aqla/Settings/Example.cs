using System.Collections.Generic;

namespace AqlaSerializer.Settings
{
    //[SerializableType(DefaultMemberFormat = MemberFormat.Aqla, WriteLateReferenceDefault = true)]
    class X
    {
        [SerializableMember(1, ModelId = 0, EnhancedWriteAs = EnhancedMode.LateReference, ContentBinaryFormatHint = BinaryDataFormat.Default)]
        [SerializableMemberNested(2, MemberFormat.Compact, ModelId = 0, Level = 1, CollectionFormat = CollectionFormat.Google, CollectionAppend = true)]
        public int[][] Member { get; set; }
    }
}