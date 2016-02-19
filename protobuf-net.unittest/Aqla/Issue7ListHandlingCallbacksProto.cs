
using System.Collections.Generic;
using AqlaSerializer.Meta;
using NUnit.Framework;
using ProtoBuf;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class Issue7ListHandlingCallbacksProto
    {
        [ProtoContract]
        public class Container
        {
            [ProtoMember(1)]
            public TestList Member { get; set; }
        }

        [ProtoContract(IgnoreListHandling = true)]
        public class TestList : List<int>
        {
            [ProtoIgnore]
            public int SomeValue { get; set; }

            [ProtoAfterDeserialization]
            public void Callback()
            {
                SomeValue = 12345;
            }
        }

        [Test]
        public void Execute()
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = true;
            tm.SkipCompiledVsNotCheck = true;
            var v = tm.DeepClone(new Container() { Member = new TestList() });
            Assert.That(v.Member.SomeValue, Is.EqualTo(12345));
        }
    }
}