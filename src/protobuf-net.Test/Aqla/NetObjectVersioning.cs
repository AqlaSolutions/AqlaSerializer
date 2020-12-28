using System;
using System.IO;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class NetObjectVersioning
    {
        [SerializableType]
        public class ContainerV1
        {
            [SerializableMember(1, ValueFormat.MinimalEnhancement)]
            public string Value1 { get; set; }

            [SerializableMember(2, ValueFormat.Reference)]
            public string Value2 { get; set; }

            [SerializableMember(3, ValueFormat.MinimalEnhancement)]
            public string Value3 { get; set; }

            [SerializableMember(4, ValueFormat.MinimalEnhancement)]
            public Custom Value4 { get; set; }

            [SerializableMember(5, ValueFormat.Reference)]
            public Custom Value5 { get; set; }

            [SerializableMember(6, ValueFormat.MinimalEnhancement)]
            public Custom Value6 { get; set; }
        }

        [SerializableType]
        public class ContainerV2
        {
            [SerializableMember(1, ValueFormat.Reference)]
            public string Value1 { get; set; }

            [SerializableMember(2, ValueFormat.Reference)]
            public string Value2 { get; set; }

            [SerializableMember(3, ValueFormat.Reference)]
            public string Value3 { get; set; }

            [SerializableMember(4, ValueFormat.Reference)]
            public Custom Value4 { get; set; }

            [SerializableMember(5, ValueFormat.Reference)]
            public Custom Value5 { get; set; }

            [SerializableMember(6, ValueFormat.Reference)]
            public Custom Value6 { get; set; }
        }

        [SerializableType]
        public class Custom : IEquatable<Custom>
        {
            public int Index { get; set; }

            public Custom()
            {
            }

            public Custom(int index)
            {
                Index = index;
            }

            public bool Equals(Custom other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Index == other.Index;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Custom)obj);
            }

            public override int GetHashCode()
            {
                return Index;
            }
        }

        [Test]
        public void ExecuteNulls()
        {
            var tm = TypeModel.Create();
            var clone = tm.ChangeType<ContainerV1, ContainerV2>(new ContainerV1());
            Assert.That(clone, Is.Not.Null);
        }

        [Test]
        public void ExecuteValues()
        {
            var tm = TypeModel.Create();
            ContainerV1 obj = MakeWithRefs();

            ContainerV2 clone = tm.ChangeType<ContainerV1, ContainerV2>(obj);

            AssertValuesEqual(clone, obj);

            // was serialized not as ref
            Assert.That(clone.Value6, Is.Not.SameAs(clone.Value4));
        }

        [Test]
        public void ExecuteNullsReversed()
        {
            var tm = TypeModel.Create();
            var clone = tm.ChangeType<ContainerV2, ContainerV1>(new ContainerV2());
            Assert.That(clone, Is.Not.Null);
        }

        [Test]
        public void ExecuteValuesReversed()
        {
            var tm = TypeModel.Create();
            ContainerV2 obj = MakeWithRefs2();

            ContainerV1 clone = tm.ChangeType<ContainerV2, ContainerV1>(obj);

            AssertValuesEqual(clone, obj);

            // was serialized as ref
            Assert.That(clone.Value6, Is.SameAs(clone.Value4));
        }

        [Test]
        public void ExecuteNullGroupCanBeReadWhenSuppressTogglingOn()
        {
            var tm = TypeModel.Create();
            var tmOld = TypeModel.Create(false, new ProtoCompatibilitySettingsValue() { SuppressNullWireType = true });
            ContainerV1 obj = new ContainerV1()
            {
                Value6 = new Custom(1)
            };

            ContainerV1 clone;

            using (var ms = new MemoryStream())
            {
                tmOld.Serialize(ms, obj);
                ms.Position = 0;
                clone = tm.Deserialize<ContainerV1>(ms);
            }

            AssertValuesEqual(clone, obj);
        }

        [Test]
        public void ExecuteNullWireTypeCanBeReadEvenWhenSuppressTogglingOff()
        {
            var tm = TypeModel.Create();
            var tmOld = TypeModel.Create(false, new ProtoCompatibilitySettingsValue() { SuppressNullWireType = true });
            ContainerV1 obj = new ContainerV1()
            {
                Value6 = new Custom(1)
            };

            ContainerV1 clone;
            using (var ms = new MemoryStream())
            {
                tmOld.Serialize(ms, obj);
                ms.Position = 0;
                clone = tm.Deserialize<ContainerV1>(ms);
            }
            AssertValuesEqual(clone, obj);
        }

        static void AssertValuesEqual(ContainerV2 clone, ContainerV1 obj)
        {
            Assert.That(clone.Value1, Is.EqualTo(obj.Value1));
            Assert.That(clone.Value2, Is.EqualTo(obj.Value2));
            Assert.That(clone.Value3, Is.EqualTo(obj.Value3));
            Assert.That(clone.Value4, Is.EqualTo(obj.Value4));
            Assert.That(clone.Value5, Is.EqualTo(obj.Value5));
            Assert.That(clone.Value6, Is.EqualTo(obj.Value6));
        }

        static void AssertValuesEqual(ContainerV1 clone, ContainerV1 obj)
        {
            Assert.That(clone.Value1, Is.EqualTo(obj.Value1));
            Assert.That(clone.Value2, Is.EqualTo(obj.Value2));
            Assert.That(clone.Value3, Is.EqualTo(obj.Value3));
            Assert.That(clone.Value4, Is.EqualTo(obj.Value4));
            Assert.That(clone.Value5, Is.EqualTo(obj.Value5));
            Assert.That(clone.Value6, Is.EqualTo(obj.Value6));
        }

        static void AssertValuesEqual(ContainerV1 clone, ContainerV2 obj)
        {
            Assert.That(clone.Value1, Is.EqualTo(obj.Value1));
            Assert.That(clone.Value2, Is.EqualTo(obj.Value2));
            Assert.That(clone.Value3, Is.EqualTo(obj.Value3));
            Assert.That(clone.Value4, Is.EqualTo(obj.Value4));
            Assert.That(clone.Value5, Is.EqualTo(obj.Value5));
            Assert.That(clone.Value6, Is.EqualTo(obj.Value6));
        }

        static ContainerV1 MakeWithRefs()
        {
            var obj = new ContainerV1()
            {
                Value1 = "abc",
                Value2 = "def",
                Value4 = new Custom(1),
                Value5 = new Custom(2),
            };
            obj.Value3 = obj.Value1;
            obj.Value6 = obj.Value4;
            return obj;
        }

        static ContainerV2 MakeWithRefs2()
        {
            var obj = new ContainerV2()
            {
                Value1 = "abc",
                Value2 = "def",
                Value4 = new Custom(1),
                Value5 = new Custom(2),
            };
            obj.Value3 = obj.Value1;
            obj.Value6 = obj.Value4;
            return obj;
        }
    }
}