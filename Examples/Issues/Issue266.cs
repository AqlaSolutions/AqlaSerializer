// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;
using System.IO;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue266
    {
        [Test]
        public void TestNakedNullableInt32Deserialize()
        {
            int? i = Serializer.Deserialize<int?>(Stream.Null);
            Assert.IsNull(i);
        }
        [Test]
        public void TestWrappedNullableEnumDeserialize()
        {
            Bar bar = Serializer.Deserialize<Bar>(Stream.Null);
            Assert.IsNull(bar);
        }

        [Ignore("AqlaSerializer: not relevant - produces different results when compiling but nulls will be fully replaced soon")]
        [Test]
        public void TestNakedNullableEnumDeserialize()
        {
            RuntimeTypeModel.Default.AutoCompile = false;
            Foo? foo = Serializer.Deserialize<Foo?>(Stream.Null);
            Assert.IsNull(foo);
        }

        [Ignore("AqlaSerializer: not relevant - produces different results when compiling but nulls will be fully replaced soon")]
        [Test]
        public void TestNakedNullableEnumDeserializeCompile()
        {
            RuntimeTypeModel.Default.AutoCompile = true;
            Foo? foo = Serializer.Deserialize<Foo?>(Stream.Null);
            Assert.IsNull(foo);
        }
        [Test]
        public void TestNakedDirectFoo()
        {
            Foo orig = Foo.B, result;
            using(var ms = new MemoryStream())
            {
                RuntimeTypeModel.Default.Serialize(ms, Foo.B);
                ms.Position = 0;
                result = (Foo) RuntimeTypeModel.Default.Deserialize(ms, null, typeof (Foo));
            }
            Assert.AreEqual(orig, result);
        }

        public enum Foo
        {
            A = 2, B = 3
        }
        [ProtoBuf.ProtoContract]
        public class Bar
        {
            [ProtoBuf.ProtoMember(1)]
            public Foo? Foo { get; set; }
        }
    }
}
