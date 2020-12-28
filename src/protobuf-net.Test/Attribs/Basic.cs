// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer.Meta;

namespace AqlaSerializer.unittest.Attribs
{
    [TestFixture]
    public class Basic
    {
        [ProtoBuf.ProtoContract]
        public class BasicContract
        {
            [ProtoBuf.ProtoMember(1)]
            public int Expected { get; set; }

            [ProtoBuf.ProtoIgnore]
            [ProtoBuf.ProtoMember(2)]
            public int Ignored { get; set; }

            public int NotExpected { get; set; }
        }

        [Test]
        public void CheckThatBasicAttributesAreRespected()
        {
            var model = RuntimeTypeModel.Create();
            model.Add(typeof(BasicContract), true);
            BasicContract obj = new BasicContract { Expected = 123, Ignored = 456, NotExpected = 789 },
                clone = (BasicContract)model.DeepClone(obj);

            Assert.AreNotSame(obj, clone);
            Assert.AreEqual(123, clone.Expected);
            Assert.AreEqual(0, clone.Ignored);
            Assert.AreEqual(0, clone.NotExpected);

 
        }
    }
}
