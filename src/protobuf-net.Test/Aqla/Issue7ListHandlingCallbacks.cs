using System.Collections.Generic;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class Issue7ListHandlingCallbacks
    {
        [SerializableType]
        public class Container
        {
            public TestList Member { get; set; }
        }

        [SerializableType(IgnoreListHandling = true)]
        public class TestList : List<int>
        {
            [NonSerializableMember]
            public int SomeValue { get; set; }

            [AfterDeserializationCallback]
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