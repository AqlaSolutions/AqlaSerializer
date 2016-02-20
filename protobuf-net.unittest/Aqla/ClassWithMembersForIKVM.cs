namespace AqlaSerializer.unittest.Aqla
{
    [SerializableType]
    public class ClassWithMembersForIKVM
    {
        [SerializableMember(1)]
        public int Simple { get; set; }

        [SerializableMember(2, EnhancedMode.Reference)]
        public int Enum { get; set; }

        [SerializableMember(3, EnhancedMode.Reference, DefaultValue = 5, CollectionFormat = CollectionFormat.Google)]
        public int Named { get; set; }

        [SerializableMember(4, true)]
        public int Bool { get; set; }

    }
}