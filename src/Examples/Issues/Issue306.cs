// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue306
    {
        [Test]
        public void TestTuple()
        {
            var model = TypeModel.Create();
            model.Add(typeof (Foo), true);

            string schema = model.GetSchema(typeof (Foo));

        }

        [ProtoBuf.ProtoContract]
        public class Foo
        {
            [ProtoBuf.ProtoMember(1)]
            public Dictionary<int, string> Lookup { get; set; }
        }
    }
}
