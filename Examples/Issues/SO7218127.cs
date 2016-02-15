// Modified by Vladyslav Taranov for AqlaSerializer, 2016

using System.Diagnostics;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Examples.Issues
{

    [TestFixture]
    public class SO7218127
    {
        [Test]
        public void Test()
        {
            var orig = new SomeWrapper {Value = new SubType { Foo = 123, Bar = "abc"}};
            var tm = TypeModel.Create();
            tm.SkipCompiledVsNotCheck = true;
            tm.AutoCompile = false;
            Trace.WriteLine("1");
            var clone = tm.DeepClone(orig);
            Assert.AreEqual(123, orig.Value.Foo);
            Assert.AreEqual("abc", ((SubType) clone.Value).Bar);
            Trace.WriteLine("2");
            tm.CompileInPlace();
            Trace.WriteLine("3");
            clone = tm.DeepClone(orig);
            Trace.WriteLine("4");
            Assert.AreEqual(123, orig.Value.Foo);
            Assert.AreEqual("abc", ((SubType) clone.Value).Bar);
        }
        [ProtoBuf.ProtoContract]
        public class SomeWrapper
        {
            [ProtoBuf.ProtoMember(1, DynamicType = true)]
            public BaseType Value { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class BaseType
        {
            [ProtoBuf.ProtoMember(1)]
            public int Foo { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class SubType : BaseType
        {
            [ProtoBuf.ProtoMember(2)]
            public string Bar { get; set; }
        }
    }
}
