// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using NUnit.Framework;
using AqlaSerializer;

namespace Examples.Issues
{

    [TestFixture]
    public class SO7218127
    {
        [Test]
        public void Test()
        {
            var orig = new SomeWrapper {Value = new SubType { Foo = 123, Bar = "abc"}};
            var clone = Serializer.DeepClone(orig);
            Assert.AreEqual(123, orig.Value.Foo);
            Assert.AreEqual("abc", ((SubType) clone.Value).Bar);
        }
        [ProtoBuf.ProtoContract]
        public class SomeWrapper
        {
            [ProtoBuf.ProtoMember(1, DynamicType = true)]
            public BaseType Value { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class BaseType
        {
            [ProtoBuf.ProtoMember(1)]
            public int Foo { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class SubType : BaseType
        {
            [ProtoBuf.ProtoMember(2)]
            public string Bar { get; set; }
        }
    }
}
