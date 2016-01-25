// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.IO;
using NUnit.Framework;
using AqlaSerializer;
using System;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue284
    {
        [Test]
        public void Execute()
        {
            MyArgs test = new MyArgs
            {
                Value = 12,
            };

            byte[] buffer = new byte[256];
            using (MemoryStream ms = new MemoryStream(buffer))
            {
                Serializer.Serialize(ms, test);
                ms.SetLength(ms.Position);
                buffer = ms.ToArray();
            }

            using (MemoryStream ms = new MemoryStream(buffer))
            {
                Serializer.Deserialize<MyArgs>(ms);
            }
        }

        [ProtoBuf.ProtoContract]
        public class MyArgs
        {
            [ProtoBuf.ProtoMember(1, DynamicType = true)]
            public object Value;
        }
    }
}
