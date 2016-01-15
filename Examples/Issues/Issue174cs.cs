// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;
using System.IO;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue174cs
    {
        [Test, ExpectedException(ExpectedMessage = "Dynamic type is not a contract-type: Boolean")]
        public void TestDynamic()
        {
            var myVal = new TestProto { Value = true };
            byte[] serialized;
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, myVal);
                serialized = ms.ToArray();
            }
            Assert.That(serialized, Is.Not.Null);
        }

        [ProtoBuf.ProtoContract]
        public class TestProto
        {
            [ProtoBuf.ProtoMember(1, DynamicType = true)]
            public object Value { get; internal set; }
        }
    }
}
