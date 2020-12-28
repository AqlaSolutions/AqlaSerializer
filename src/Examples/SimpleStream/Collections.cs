// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Examples.SimpleStream
{
    [TestFixture]
    public class Collections
    {

        [ProtoBuf.ProtoContract]
        public class FooContainer
        {
            [ProtoBuf.ProtoMember(1)]
            public Foo Foo { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class Foo
        {
            [ProtoBuf.ProtoMember(1)]
            public List<Bar> Bars { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class Bar
        {
            [ProtoBuf.ProtoMember(1)]
            public int Value { get; set; }
        }

        static Foo GetFullFoo()
        {
            Foo foo = new Foo();
            foo.Bars = new List<Bar>();
            for (int i = Int16.MinValue; i <= Int16.MaxValue; i++)
            {
                foo.Bars.Add(new Bar { Value = i });
            }
            return foo;
        }

        [Test]
        public void RunWrappedCollectionTest()
        {
            FooContainer fooContainer = new FooContainer(), cloneContainer;
            Foo foo = GetFullFoo(), clone;
            fooContainer.Foo = foo;

            cloneContainer = Serializer.DeepClone(fooContainer);
            clone = cloneContainer.Foo;
            Assert.IsNotNull(cloneContainer, "Clone Container");
            Assert.IsTrue(CompareFoos(foo, clone));
        }

        [Test]
        public void RunNakedCollectionTest()
        {
            var tm = TypeModel.Create();
            Foo foo = GetFullFoo(), clone = tm.DeepClone(foo);
            Assert.IsTrue(CompareFoos(foo, clone));            
        }

        private static bool CompareFoos(Foo original, Foo clone) {
            Assert.IsNotNull(original, "Original");
            Assert.IsNotNull(clone, "Clone");
            Assert.AreEqual(original.Bars.Count, clone.Bars.Count, "Item count");
            if (original == null || clone == null ||
                original.Bars.Count != clone.Bars.Count) return false;
            int count = clone.Bars.Count;
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(original.Bars[i].Value, clone.Bars[i].Value, "Value mismatch");
                if (original.Bars[i].Value != clone.Bars[i].Value) return false;
            }
            return true;
        }
    }
}
