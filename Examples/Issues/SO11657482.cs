// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.IO;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples.Issues
{
    [TestFixture]
    public class SO11657482
    {
        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(1, typeof(Derived))]
        public abstract class Base { }

        [ProtoBuf.ProtoContract]
        public class Derived : Base
        {
            [ProtoBuf.ProtoMember(1)]
            public int SomeProperty { get; set; }
        }

        [ProtoBuf.ProtoContract]
        public class Aggregate
        {
            [ProtoBuf.ProtoMember(1, AsReference = true)]
            public Base Base { get; set; }
        }

        [Test]
        public void TestMethod1()
        {
            var value = new Aggregate { Base = new Derived() };
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, value);
                stream.Position = 0;

                var obj = Serializer.Deserialize<Aggregate>(stream);
                Assert.AreEqual(typeof(Derived), obj.Base.GetType());
            }
        }
    }
}
