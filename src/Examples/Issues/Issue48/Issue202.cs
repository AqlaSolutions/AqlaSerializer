// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Examples.Issues.Issue48
{
    [TestFixture]
    public class Issue202
    {
        [Test]
        public void TestListsAsFields()
        { 
            var model = RuntimeTypeModel.Create();
            model.AutoCompile = false;
            ExecuteTest(model, "runtime");

            model.CompileInPlace();
            ExecuteTest(model, "CompileInPlace");

            ExecuteTest(model.Compile(), "Compile");
        }
        void ExecuteTest(TypeModel model, string test)
        {
            A a = new A { flags = new List<string> { "abc", "def" } }, c;

            Assert.IsNotNull(a.flags.Count, test);
            Assert.AreEqual(2, a.flags.Count, test);
            Assert.AreEqual("abc", a.flags[0], test);
            Assert.AreEqual("def", a.flags[1], test);

            B b;
            using (var ms = new MemoryStream())
            {
                model.Serialize(ms, a);
                ms.Position = 0;
                b = (B)model.Deserialize(ms, null, typeof(B));
            }
            Assert.IsNotNull(b.flags.Count, test);
            Assert.AreEqual(2, b.flags.Count, test);
            Assert.AreEqual("abc", b.flags[0], test);
            Assert.AreEqual("def", b.flags[1], test);

            using (var ms = new MemoryStream())
            {
                model.Serialize(ms, b);
                ms.Position = 0;
                c = (A)model.Deserialize(ms, null, typeof(A));
            }
            Assert.IsNotNull(c.flags.Count, test);
            Assert.AreEqual(2, c.flags.Count, test);
            Assert.AreEqual("abc", c.flags[0], test);
            Assert.AreEqual("def", c.flags[1], test);
        }

        [ProtoBuf.ProtoContract]
        public class A //property version
        {
            [ProtoBuf.ProtoMember(3)]
            public List<string> flags { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class B //field version
        {
            [ProtoBuf.ProtoMember(3)]
            public List<string> flags;
        }

    }
}
