// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;
using System.IO;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    [TestFixture]
    public class SO8093623
    {
        [ProtoBuf.ProtoContract]
        public class A_generated
        {
            [ProtoBuf.ProtoMember(1)]
            public int Age;

            [ProtoBuf.ProtoMember(10)]
            public B_generated b;
        }

        [ProtoBuf.ProtoContract]
        public class B_generated
        {
            [ProtoBuf.ProtoMember(2)]
            public int Balls;
        }
        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(10, typeof(B))]
        public class A
        {
            [ProtoBuf.ProtoMember(1)]
            public int Age;
        }
        [ProtoBuf.ProtoContract]
        public class B : A
        {
            [ProtoBuf.ProtoMember(2)]
            public int Balls;
        }

        [Test]
        public void TestExpectedResultFromGeneratedTypes()
        {
            var model = RuntimeTypeModel.Create();
            model.AutoCompile = false;
            model.Add(typeof(A_generated), true);
            model.Add(typeof(B_generated), true);

            TestGeneratedModel(model, "Runtime");
            model.CompileInPlace();
            TestGeneratedModel(model, "CompileInPlace");
            TestGeneratedModel(model.Compile(), "Compile");
        }
        private static void TestGeneratedModel(TypeModel model, string message)
        {
            var a = new A_generated() { Age = 10, b = new B_generated { Balls = 23 } };
            using (var ms = new MemoryStream())
            {
                model.Serialize(ms, a);
                Debug.WriteLine("AqlaSerializer changed format");
                //Assert.IsTrue(ms.ToArray().SequenceEqual(new byte[] { 08, 10, 82, 2, 16, 23 }), message);
                ms.Position = 0;
                var clone = (A_generated)model.Deserialize(ms, null, typeof(A_generated));
                Assert.AreEqual(10, clone.Age, message);
                Assert.AreEqual(23, clone.b.Balls, message);
            }
        }
        [Ignore("AqlaSerializer changed format")]
        [Test]
        public void TestSubclassDeserializes()
        {
            var model = RuntimeTypeModel.Create();
            model.AutoCompile = false;
            model.Add(typeof (A), true);
            model.Add(typeof (B), true);

            TestInheritanceModel(model, "Runtime");
            model.CompileInPlace();
            TestInheritanceModel(model, "CompileInPlace");
            TestInheritanceModel(model.Compile(), "Compile");
        }

        private static void TestInheritanceModel(TypeModel model, string message)
        {
            using (var ms = new MemoryStream(new byte[] {08, 10, 82, 2, 16, 23}))
            {
                var clone = (A)model.Deserialize(ms, null, typeof(A));
                Assert.AreEqual(10, clone.Age, message);
                B b = (B) clone;
                Assert.AreEqual(23, b.Balls, message);
            }
        }
    }
}
