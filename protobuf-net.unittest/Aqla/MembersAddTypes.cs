using AqlaSerializer;
using NUnit.Framework;
using AqlaSerializer.Meta;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class MembersAddTypes
    {
        [ProtoBuf.ProtoContract]
        public class ProtoClassWithMember
        {
            [ProtoBuf.ProtoMember(1)]
            public ProtoMemberClass ProtoMember { get; set; }
        }

        [ProtoBuf.ProtoContract]
        public class ProtoMemberClass
        {
            [ProtoBuf.ProtoMember(1)]
            public int X { get; set; }
        }

        // enabled for everything now
        //[ExpectedException("System.InvalidOperationException", ExpectedMessage = "No serializer defined for type: AqlaSerializer.unittest.AqlaAttributes.MembersAddTypes+ProtoMemberClass")]
        //[Test]
        //public void ShouldNotSerializeMemberContentForProto()
        //{
        //    var m = TypeModel.Create();
        //    m.Add(typeof(ProtoClassWithMember), true);
        //    m.AutoAddMissingTypes = false;
        //    var original = new ProtoClassWithMember() { ProtoMember = new ProtoMemberClass() { X = 12345 } };
        //    var clone = (ProtoClassWithMember)m.DeepClone(original);
        //    Assert.AreEqual(original.ProtoMember.X, clone.ProtoMember.X);
        //}

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