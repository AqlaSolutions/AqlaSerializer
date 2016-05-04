using System.Collections.Generic;
using System.IO;
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
        public void ChangeSimpleWithoutSeekingShouldThrowKeyNotFound()
        {
            var tm = TypeModel.Create();
            tm.AllowReferenceVersioningSeeking = false;
            var obj = new SourceSimple();
            obj.Foo = obj.SameFoo = new SimpleFoo();
            Assert.That(() => tm.ChangeType<SourceSimple, DestinationSimple>(obj), Throws.TypeOf<KeyNotFoundException>());
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

        [Test]
        public void SizeShouldBeSameFor3Passes()
        {
            var tm = TypeModel.Create();
            var obj = new SourceComplex();
            obj.Bar = obj.SameBar = new SourceBar();
            obj.Bar.Foo = obj.SameFoo = new SimpleFoo();
            using (var ms = new MemoryStream())
            {
                int? previous = null;

                for (int i = 0; i < 3; i++)
                {
                    tm.Serialize(ms, obj);
                    ms.Position = 0;
                    var cloned = tm.Deserialize<SourceComplex>(ms);
                    var l = (int)ms.Length;
                    if (previous != null)
                        Assert.That(l, Is.EqualTo(previous.Value));
                    previous = l;
                    obj = cloned;
                    ms.SetLength(0);
                }

            }
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