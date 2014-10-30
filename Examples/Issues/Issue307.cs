// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System.IO;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using AqlaSerializer.ServiceModel;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue307
    {
        public enum Foo
        {
            A,
            B,
            C
        }
        [ProtoBuf.ProtoContract]
        public class FooWrapper
        {
            [ProtoBuf.ProtoMember(1)]
            public Foo Foo { get; set; }
        }
        [Test]
        public void TestRoundTripWrappedEnum()
        {
            var ser = new XmlProtoSerializer(RuntimeTypeModel.Default, typeof(FooWrapper));
            var ms = new MemoryStream();
            ser.WriteObject(ms, new FooWrapper { Foo = Foo.B });
            ms.Position = 0;
            var clone = (FooWrapper)ser.ReadObject(ms);

            Assert.AreEqual(Foo.B, clone.Foo);
        }
        [Test]
        public void TestRoundTripNakedEnum()
        {
            var ser = new XmlProtoSerializer(RuntimeTypeModel.Default, typeof (Foo));
            var ms = new MemoryStream();
            ser.WriteObject(ms, Foo.B);
            ms.Position = 0;
            var clone = (Foo)ser.ReadObject(ms);

            Assert.AreEqual(Foo.B, clone);

        }
    }
}
