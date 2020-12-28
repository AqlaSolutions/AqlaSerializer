// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    [TestFixture]
    public class SO11317045
    {
        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(1, typeof(A), DataFormat = ProtoBuf.DataFormat.Group)]
        public class ABase
        {
        }

        [ProtoBuf.ProtoContract]
        public class A : ABase
        {
            [ProtoBuf.ProtoMember(1, DataFormat = ProtoBuf.DataFormat.Group)]
            public B B
            {
                get;
                set;
            }
        }

        [ProtoBuf.ProtoContract]
        public class B
        {
            [ProtoBuf.ProtoMember(1, DataFormat = ProtoBuf.DataFormat.Group)]
            public List<byte[]> Data
            {
                get;
                set;
            }
        }

        [Ignore("AqlaSerializer - see later, what purpose?"), Test]
        public void Execute()
        {
            var a = new A();
            var b = new B();
            a.B = b;
            
            b.Data = new List<byte[]>
            {
                Enumerable.Range(0, 1999).Select(v => (byte)v).ToArray(),
                Enumerable.Range(2000, 3999).Select(v => (byte)v).ToArray(),
            };

            var stream = new MemoryStream();
            var model = TypeModel.Create();
            model.AutoCompile = false;
#if DEBUG // this is only available in debug builds; if set, an exception is
          // thrown if the stream tries to buffer
            model.ForwardsOnly = true;
#endif
            CheckClone(model, a);
            model.CompileInPlace();
            CheckClone(model, a);
            CheckClone(model.Compile(), a);
        }
        void CheckClone(TypeModel model, A original)
        {
            int sum = original.B.Data.Sum(x => x.Sum(b => (int)b));
            var clone = (A)model.DeepClone(original);
            Assert.IsInstanceOf(typeof(A), clone);
            Assert.IsInstanceOf(typeof(B), clone.B);
            Assert.AreEqual(sum, clone.B.Data.Sum(x => x.Sum(b => (int)b)));
        }


        [Test]
        public void TestProtoIncludeWithStringKnownTypeName()
        {
            NamedProtoInclude.Foo foo = new NamedProtoInclude.Bar();
            var clone = Serializer.DeepClone(foo);

            Assert.IsInstanceOf(typeof(NamedProtoInclude.Bar), foo);
        }

    }
}

namespace Examples.Issues.NamedProtoInclude
{
    [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(1, "Examples.Issues.NamedProtoInclude.Bar")]
    public class Foo
    {

    }

    [ProtoBuf.ProtoContract]
    public class Bar : Foo
    {

    }
}