// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AqlaSerializer;
using NUnit.Framework;
using AqlaSerializer.Meta;
using System.IO;

namespace Examples.Issues
{
    [TestFixture]
    public class SO6671829
    {
        [Test]
        public void Execute()
        {
            var model = TypeModel.Create();
            MetaType t = model.Add(typeof (hierarchy.B), false);
            t.Add("prop1", "prop2");
            t[1].AsReference = false;
            t[2].AsReference = false;
            

            var hb = new hierarchy.B();
            hb.prop1 = "prop1";
            hb.prop2 = "prop2";

            var ms = new MemoryStream();

            model.Serialize(ms, hb);
            ms.Position = 0;
            var flatB = Serializer.Deserialize<flat.B>(ms);

            Assert.AreEqual("prop1", hb.prop1);
            Assert.AreEqual("prop2", hb.prop2);
            Assert.AreEqual("prop1", flatB.prop1);
            Assert.AreEqual("prop2", flatB.prop2);
            Assert.AreEqual("prop1=prop1, prop2=prop2", hb.ToString());
            Assert.AreEqual("prop1=prop1, prop2=prop2", flatB.ToString());
        }
        class hierarchy
        {

            [ProtoBuf.ProtoContract]
            public class A
            {
                [ProtoBuf.ProtoMember(1)]
                public string prop1 { get; set; }
            }

            [ProtoBuf.ProtoContract]
            public class B : A
            {
                public B()
                {
                }

                [ProtoBuf.ProtoMember(1)]
                public string prop2 { get; set; }

                public override string ToString()
                {
                    return "prop1=" + prop1 + ", prop2=" + prop2;
                }

            }
        }

        class flat
        {
            [ProtoBuf.ProtoContract]
            public class B
            {
                [ProtoBuf.ProtoMember(1)]
                public string prop1 { get; set; }

                [ProtoBuf.ProtoMember(2)]
                public string prop2 { get; set; }

                public override string ToString()
                {
                    return "prop1=" + prop1 + ", prop2=" + prop2;
                }
            }
        }
    }
}
