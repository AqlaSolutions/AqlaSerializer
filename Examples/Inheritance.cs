// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples
{
    [TestFixture]
    public class Inheritance
    {

        [ProtoBuf.ProtoContract]
        public class A { }

        public class B : A { }
        [ProtoBuf.ProtoContract]
        public class C
        {
            [ProtoBuf.ProtoMember(1)]
            public A A { get; set; }
        }

        [Test]
        public void UnknownSubtypeMessage()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => {
                var c = new C { A = new B() };
                Serializer.DeepClone(c);
            });
            Assert.That(ex.Message, Is.EqualTo("Unexpected sub-type: Examples.Inheritance+B"));
        }

        [Test]
        public void TestFooAsFoo()
        {
            Foo foo = new Foo { Value = 1 };
            Assert.AreEqual(foo.Value, Serializer.DeepClone(foo).Value);
        }
        [Test]
        public void TestBarAsBar()
        {
            Bar bar = new Bar { Value = 1 };
            Assert.AreEqual(bar.Value, Serializer.DeepClone<Foo>(bar).Value);
        }

        [Test]
        public void TestBarAsFoo()
        {
            Foo foo = new Bar { Value = 1 };
            Foo clone = Serializer.DeepClone(foo);
            Assert.AreEqual(foo.Value, clone.Value);
            Assert.IsInstanceOf(typeof(Bar), clone);
        }
    }

    [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(2, typeof(Bar))]
    public class Foo
    {
        [ProtoBuf.ProtoMember(1)]
        public int Value { get; set; }
    }

    [ProtoBuf.ProtoContract]
    public class Bar : Foo
    {

    }
}
