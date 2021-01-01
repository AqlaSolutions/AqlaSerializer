﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
namespace Examples.Issues
{
    [TestFixture]
    public class SO6476958
    {
        [ProtoBuf.ProtoContract]
        public class A
        {
            [ProtoBuf.ProtoMember(1, AsReference = true)]
            public string Id { get; set; }

            public override bool Equals(object obj) { return Id == ((A)obj).Id; }
            public override int GetHashCode() { return Id.GetHashCode(); }
            public override string ToString() { return Id; }
        }
        [ProtoBuf.ProtoContract]
        public class B
        {
            [ProtoBuf.ProtoMember(1)]
            public string Id { get; set; }

            public override bool Equals(object obj) { return Id == ((B)obj).Id; }
            public override int GetHashCode() { return Id.GetHashCode(); }
            public override string ToString() { return Id; }
        }

        [ProtoBuf.ProtoContract]
        public class BasicDuplicatedString
        {
            [ProtoBuf.ProtoMember(1, AsReference = true)]
            public string A {get;set;}
            [ProtoBuf.ProtoMember(2, AsReference = true)]
            public string B { get; set; }

        }
        [Test]
        public void TestBasicDuplicatedString()
        {
            BasicDuplicatedString foo = new BasicDuplicatedString(), clone;
            foo.A = new string('a', 40);
            foo.B = new string('a', 40);
            Assert.AreNotSame(foo.A, foo.B); // different string refs

            using (var ms = new MemoryStream())
            {
                var tm = TypeModel.Create();
                tm.Serialize(ms, foo);
                //Assert.AreEqual(50, ms.Length);
                ms.Position = 0;
                clone = tm.Deserialize<BasicDuplicatedString>(ms);
            }
            Assert.AreEqual(foo.A, clone.A);
            Assert.AreEqual(foo.B, clone.B);
            Assert.AreSame(clone.A, clone.B);
        }

        [Test]
        public void Execute()
        {
            var m = TypeModel.Create();
            m.AutoCompile = false;
            m.Add(typeof(object), false).AddSubType(1, typeof(A)).AddSubType(2, typeof(B));

            Test(m);
            m.CompileInPlace();
            Test(m);
            Test(m.Compile());
        }

        private static void Test(TypeModel m)
        {
            var list = new List<object> { new A { Id = "Abracadabra" }, new B { Id = "Focuspocus" }, new A { Id = "Abracadabra" }, };
            using (var ms = new MemoryStream())
            {
                m.Serialize(ms, list);
                ms.Position = 0;
                var list2 = (List<object>)m.Deserialize(ms, null, typeof(List<object>));
                Debug.Assert(list.SequenceEqual(list2));
                File.WriteAllBytes(@"output.dump", ms.ToArray());
            }
        }
    }
}
