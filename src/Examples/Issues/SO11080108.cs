// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    [TestFixture]
    public class SO11080108
    {
        [Test]
        public void Execute()
        {
            byte[] buffer = { 9, 8, 5, 26, 5, 24, 238, 98, 32, 1 };
            var model = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
            model.AutoCompile = false;
            using (var ms = new MemoryStream(buffer))
            {

                int len = ProtoReader.DirectReadVarintInt32(ms);
                var resp = (Response)model.Deserialize(ms, null, typeof(Response), len);

                Assert.AreEqual(5, resp.Type);
                Assert.AreEqual(1, resp.v3dDelta.Count);
                Assert.AreEqual(12654, resp.v3dDelta[0].ask);
                Assert.AreEqual(1, resp.v3dDelta[0].askSize);
            }
        }
        [ProtoBuf.ProtoContract]
        public class V3DDelta
        {
            [ProtoBuf.ProtoMember(1)]
            public int bid { get; set; }
            [ProtoBuf.ProtoMember(2)]
            public int bidSize { get; set; }
            [ProtoBuf.ProtoMember(3)]
            public int ask { get; set; }
            [ProtoBuf.ProtoMember(4)]
            public int askSize { get; set; }
        }

        [ProtoBuf.ProtoContract]
        public class Request
        {
            [ProtoBuf.ProtoMember(1)]
            public int Type { get; set; }
            [ProtoBuf.ProtoMember(2)]
            public string Rq { get; set; }
        }

        [ProtoBuf.ProtoContract]
        public class Response
        {
            [ProtoBuf.ProtoMember(1)]
            public int Type { get; set; }
            [ProtoBuf.ProtoMember(2)]
            public string Rsp { get; set; }
            [ProtoBuf.ProtoMember(3)]
            public List<V3DDelta> v3dDelta { get; set; }
            public Response()
            {
                v3dDelta = new List<V3DDelta>();
            }
        }
    }
}
