// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.ComponentModel;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue304
    {
        [Test]
        public void DefaultValuesForBoolMustBeLowerCase()
        {
            Assert.AreEqual(@"package Examples.Issues;

message Foo {
   optional bool Bar = 1 [default = true];
}
",

        Serializer.GetProto<Foo>()

        );
        }
        [ProtoBuf.ProtoContract]
        public class Foo
        {
            [DefaultValue(true), ProtoBuf.ProtoMember(1)]
            public bool Bar { get; set; }
        }
    }
}
