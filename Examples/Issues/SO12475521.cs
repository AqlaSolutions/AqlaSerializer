// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using NUnit.Framework;
using AqlaSerializer;
using System;

namespace Examples.Issues
{
    [TestFixture]
    public class SO12475521
    {
        [Test]
        public void Execute()
        {
            var obj = new HazType { X = 1, Type = typeof(string), AnotherType = typeof(ProtoReader) };

            var clone = Serializer.DeepClone(obj);

            Assert.AreEqual(1, clone.X);
            Assert.AreEqual(typeof(string), clone.Type);
            Assert.AreEqual(typeof(ProtoReader), clone.AnotherType);
        }

        [ProtoBuf.ProtoContract]
        public class HazType
        {
            [ProtoBuf.ProtoMember(1)]
            public int X { get; set; }

            [ProtoBuf.ProtoMember(2)]
            public Type Type { get; set; }

            [ProtoBuf.ProtoMember(3)]
            public Type AnotherType { get; set; }
        }

    }
}
