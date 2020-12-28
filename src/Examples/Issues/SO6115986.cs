// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer.Meta;
using System.IO;
using AqlaSerializer;

namespace Examples.Issues
{
    [TestFixture]
    public class SO6115986
    {
        public interface IYObject
        {
            string X { get; }
            int Z { get; set; }
        }

        public class YObject : IYObject
        {
            public string X { get; set; }

            int z;
            int IYObject.Z { get { return z; } set { z = value; } }
        }

        public class D
        {
            public IYObject Y { get; set; }
        }
        [Test]
        public void Execute()
        {
            var m = TypeModel.Create();
            m.Add(typeof(D), false).Add("Y");
            m.Add(typeof(IYObject), false).AddSubType(1, typeof(YObject)).Add(2, "Z");
            m.Add(typeof(YObject), false).Add("X");
            var d = new D { Y = new YObject { X = "a" } };
            d.Y.Z = 123;
            using (var ms = new MemoryStream())
            {
                m.Serialize(ms, d);
                ms.Position = 0;
                var d2 = (D)m.Deserialize(ms, null, typeof(D));
                Assert.AreEqual("a",d2.Y.X);
                Assert.AreEqual(123, d2.Y.Z);
            }
        }
    }

    [TestFixture]
    public class SO6115986_WithAttributes
    {
        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(1, typeof(YObject))]
        public interface IYObject
        {
            string X { get; }
            [ProtoBuf.ProtoMember(2)]
            int Z { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class YObject : IYObject
        {
            [ProtoBuf.ProtoMember(1)]
            public string X { get; set; }

            int z;
            int IYObject.Z { get { return z; } set { z = value; } }
        }
        [ProtoBuf.ProtoContract]
        public class D
        {
            [ProtoBuf.ProtoMember(1)]
            public IYObject Y { get; set; }
        }
        [Test]
        public void Execute()
        {
            var m = TypeModel.Create();
            var d = new D { Y = new YObject { X = "a" } };
            d.Y.Z = 123;
            using (var ms = new MemoryStream())
            {
                m.Serialize(ms, d);
                ms.Position = 0;
                var d2 = (D)m.Deserialize(ms, null, typeof(D));
                Assert.AreEqual("a", d2.Y.X);
                Assert.AreEqual(123, d2.Y.Z);
            }
        }
    }
}
