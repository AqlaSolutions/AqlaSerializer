namespace AqlaSerializer.unittest.Aqla
{
    [SerializableType]
    public class ClassWithMembersForIKVM
    {
        [SerializableMember(1)]
        public int Simple { get; set; }

        [SerializableMember(2, ValueFormat.Reference)]
        public int Enum { get; set; }

        [SerializableMember(3, ValueFormat.Reference, DefaultValue = 5, CollectionFormat = CollectionFormat.Protobuf)]
        public int Named { get; set; }

        [SerializableMember(4, DynamicType = true)]
        public int Bool { get; set; }

    }
}