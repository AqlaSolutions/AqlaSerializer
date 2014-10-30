// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;
using System.IO;

namespace Examples
{
    [ProtoBuf.ProtoContract]
    public class TraceErrorData
    {
        [ProtoBuf.ProtoMember(1)]
        public int Foo { get; set; }

        [ProtoBuf.ProtoMember(2)]
        public string Bar { get; set; }

    }

    [TestFixture]
    public class TraceError
    {
        [Ignore("AqlaSerializer changed format")]
        [Test]
        public void TestTrace()
        {
            TraceErrorData ed = new TraceErrorData {Foo = 12, Bar = "abcdefghijklmnopqrstuvwxyz"};
            MemoryStream ms = new MemoryStream();
            Serializer.Serialize(ms, ed);
            byte[] buffer = ms.GetBuffer();
            Assert.AreEqual(30, ms.Length);
            MemoryStream ms2 = new MemoryStream();
            ms2.Write(buffer, 0, (int)ms.Length - 5);
            ms2.Position = 0;
            try
            {
                Serializer.Deserialize<TraceErrorData>(ms2);
                Assert.Fail("Should have errored");
            } catch(EndOfStreamException ex)
            {
                Assert.IsTrue(ex.Data.Contains("protoSource"), "Missing protoSource");
                Assert.AreEqual("tag=2; wire-type=String; offset=4; depth=0", ex.Data["protoSource"]);
            } catch(Exception ex)
            {
                Assert.Fail("Unexpected exception: " + ex);
            }
        }
    }
}
