using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class TooManyLevels
    {
        [SerializableType]
        public class Foo
        {
            [SerializableMember(1)]
            [SerializableMemberNested(1)]
            public int Member { get; set; }
        }

        [SerializableType]
        public class Bar
        {
            [SerializableMember(1)]
            [SerializableMemberNested(1)]
            public int[] Member { get; set; }
        }

        [SerializableType]
        public class Baz
        {
            [SerializableMember(1)]
            public int[] Member { get; set; }
        }

        [Test]
        public void TestTooMany()
        {
            var tm = TypeModel.Create();
            Assert.That(
                () => tm.DeepClone(new Foo() { Member = 123 }).Member,
                Throws.TypeOf<ProtoException>()
                    .With.Message.Contains("Found unused specified nested level settings, maximum possible nested level is 0-Int32, no more nested type detected"));
        }

        [Test]
        public void TestEnough()
        {
            var tm = TypeModel.Create();
            Assert.That(tm.DeepClone(new Bar() { Member = new[] { 123 } }).Member, Is.EqualTo(new[] { 123 }));
        }

        [Test]
        public void TestLess()
        {
            var tm = TypeModel.Create();
            Assert.That(tm.DeepClone(new Baz() { Member = new[] { 123 } }).Member, Is.EqualTo(new[] { 123 }));
        }
    }
}