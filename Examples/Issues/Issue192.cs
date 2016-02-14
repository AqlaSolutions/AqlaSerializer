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
    public class Issue192
    {
        [ProtoBuf.ProtoContract]
        public class SomeType { }
        [ProtoBuf.ProtoContract]
        public class Wrapper
        {
            [ProtoBuf.ProtoMember(1)]
            public List<SomeType>[] List { get; set; }
        }
        
        [Test]
        public void SerializeWrappedDeepListEmpy()
        {
            var wrapped = new Wrapper() {List = new List<SomeType>[0]};
            Assert.That(Serializer.DeepClone(wrapped).List, Is.Not.Null);
        }
        [Test]
        public void SerializeWrappedDeepListNull()
        {
            var wrapped = new Wrapper();
            Assert.That(Serializer.DeepClone(wrapped).List, Is.Null);
        }

    }
}
