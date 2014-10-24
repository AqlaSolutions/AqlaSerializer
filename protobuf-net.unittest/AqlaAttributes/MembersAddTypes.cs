using AqlaSerializer;
using NUnit.Framework;
using ProtoBuf.Meta;

namespace ProtoBuf.unittest.AqlaAttributes
{
    [TestFixture]
    public class MembersAddTypes
    {
        [ProtoContract]
        public class ProtoClassWithMember
        {
            [ProtoMember(1)]
            public ProtoMemberClass ProtoMember { get; set; }
        }

        [ProtoContract]
        public class ProtoMemberClass
        {
            [ProtoMember(1)]
            public int X { get; set; }
        }

        [ExpectedException("System.InvalidOperationException", ExpectedMessage = "No serializer defined for type: ProtoBuf.unittest.AqlaAttributes.MembersAddTypes+ProtoMemberClass")]
        [Test]
        public void ShouldNotSerializeMemberContentForProto()
        {
            var m = TypeModel.Create();
            m.Add(typeof(ProtoClassWithMember), true);
            m.AutoAddMissingTypes = false;
            var original = new ProtoClassWithMember() { ProtoMember = new ProtoMemberClass() { X = 12345 } };
            var clone = (ProtoClassWithMember)m.DeepClone(original);
            Assert.AreEqual(original.ProtoMember.X, clone.ProtoMember.X);
        }

        [SerializableType]
        public class AqlaClassWithMember
        {
            [SerializableMember(1)]
            public AqlaMemberClass AqlaMember { get; set; }
        }

        [SerializableType]
        public class AqlaMemberClass
        {
            [SerializableMember(1)]
            public int X { get; set; }
        }

        [Test]
        public void ShouldSerializeMemberContentForAqla()
        {
            var m = TypeModel.Create();
            m.Add(typeof(AqlaClassWithMember), true);
            m.AutoAddMissingTypes = false;
            var original = new AqlaClassWithMember() { AqlaMember = new AqlaMemberClass() { X = 12345 } };
            var clone = (AqlaClassWithMember)m.DeepClone(original);
            Assert.AreEqual(original.AqlaMember.X, clone.AqlaMember.X);
        }
    }
}