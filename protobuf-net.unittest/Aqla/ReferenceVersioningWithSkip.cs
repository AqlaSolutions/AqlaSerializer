using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class ReferenceVersioningWithSkip
    {
        [SerializableType]
        public class SimpleFoo
        {
        }

        [SerializableType]
        public class SourceSimple
        {
            [SerializableMember(1)]
            public SimpleFoo Foo { get; set; }

            [SerializableMember(2)]
            public SimpleFoo SameFoo { get; set; }
        }

        [SerializableType]
        public class DestinationSimple
        {
            [SerializableMember(2)]
            public SimpleFoo SameFoo { get; set; }
        }

        [Test]
        public void ChangeSimple()
        {
            var tm = TypeModel.Create();
            var obj = new SourceSimple();
            obj.Foo = obj.SameFoo = new SimpleFoo();
            var changed = tm.ChangeType<SourceSimple, DestinationSimple>(obj);
            Assert.That(changed.SameFoo, Is.Not.Null);
        }
        
        [Test]
        public void ChangeComplex()
        {
            var tm = TypeModel.Create();
            var obj = new SourceComplex();
            obj.Bar = obj.SameBar = new SourceBar();
            obj.Bar.Foo = obj.SameFoo = new SimpleFoo();
            var changed = tm.ChangeType<SourceComplex, DestinationComplex>(obj);
            Assert.That(changed.SameBar, Is.Not.Null);
            Assert.That(changed.SameFoo, Is.Not.Null);
        }
        
        // recursive: removed property inside removed property
        [SerializableType]
        public class SourceBar
        {
            [SerializableMember(1)]
            public SimpleFoo Foo { get; set; }
        }

        [SerializableType]
        public class DestionationBar
        {
        }

        [SerializableType]
        public class SourceComplex
        {
            [SerializableMember(1)]
            public SourceBar Bar { get; set; }

            [SerializableMember(2)]
            public SourceBar SameBar { get; set; }

            [SerializableMember(3)]
            public SimpleFoo SameFoo { get; set; }
        }

        [SerializableType]
        public class DestinationComplex
        {
            [SerializableMember(2)]
            public DestionationBar SameBar { get; set; }

            [SerializableMember(3)]
            public SimpleFoo SameFoo { get; set; }
        }
    }
}