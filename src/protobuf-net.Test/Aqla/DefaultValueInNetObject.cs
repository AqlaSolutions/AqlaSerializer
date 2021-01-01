using AqlaSerializer.Meta;
using NUnit.Framework;
using System;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class DefaultValueInNetObject
    {
        [SerializableType]
        public class WithDefaultS
        {
            [SerializableMember(1, DefaultValue = "http://abc", Format = ValueFormat.Compact)]
            public string Compact { get; set; } = "http://abc";
            [SerializableMember(2, DefaultValue = "http://abc", Format = ValueFormat.Reference)]
            public string Reference { get; set; } = "http://abc";
            [SerializableMember(3, DefaultValue = "http://abc", Format = ValueFormat.MinimalEnhancement)]
            public string MinimalEnchancement { get; set; } = "http://abc";
            [SerializableMember(4, DefaultValue = "http://abc", Format = ValueFormat.LateReference)]
            public string LateReference { get; set; } = "http://abc";
            [SerializableMember(5, DefaultValue = "http://abc", Format = ValueFormat.Reference)]
            public string SameReference { get; set; }

            public WithDefaultS()
            {
                SameReference = Reference;
            }
        }

        // Uri implements equals for strings
        [SerializableType]
        public class WithDefaultUri
        {
            [SerializableMember(1, DefaultValue = "http://abc", Format = ValueFormat.Compact)]
            public Uri Compact { get; set; } = new Uri("http://abc");
            [SerializableMember(2, DefaultValue = "http://abc", Format = ValueFormat.Reference)]
            public Uri Reference { get; set; } = new Uri("http://abc");
            [SerializableMember(3, DefaultValue = "http://abc", Format = ValueFormat.MinimalEnhancement)]
            public Uri MinimalEnchancement { get; set; } = new Uri("http://abc");
            [SerializableMember(4, DefaultValue = "http://abc", Format = ValueFormat.LateReference)]
            public Uri LateReference { get; set; } = new Uri("http://abc");
            [SerializableMember(5, DefaultValue = "http://abc", Format = ValueFormat.Reference)]
            public Uri SameReference { get; set; }

            public WithDefaultUri()
            {
                SameReference = Reference;
            }
        }

        [Test]
        public void ShouldWorkForStringDefault()
        {
            Check(new WithDefaultS());
        }

        [Test]
        public void ShouldWorkForStringNonDefault()
        {
            WithDefaultS obj = new() { Compact = "a234234", LateReference = "b35345", Reference = "c2342", MinimalEnchancement = "d354345" };
            obj.SameReference = obj.Reference;
            Check(obj);
        }

        [Test]
        public void ShouldWorkForUriDefault()
        {
            Check(new WithDefaultUri());
        }

        [Test]
        public void ShouldWorkForUriNonDefault()
        {
            WithDefaultUri obj = new() { Compact = new Uri("http://a"), LateReference = new Uri("http://b"), Reference = new Uri("http://c"), MinimalEnchancement = new Uri("http://d") };
            obj.SameReference = obj.Reference;
            Check(obj);
        }

        private static void Check(WithDefaultS obj)
        {
            var m = TypeModel.Create();
            var clone = m.DeepClone(obj);

            Assert.That(clone.Compact, Is.EqualTo(obj.Compact));
            Assert.That(clone.MinimalEnchancement, Is.EqualTo(obj.MinimalEnchancement));
            Assert.That(clone.Reference, Is.EqualTo(obj.Reference));
            Assert.That(clone.LateReference, Is.EqualTo(obj.LateReference));
            Assert.That(clone.SameReference, Is.SameAs(clone.Reference));
        }

        private static void Check(WithDefaultUri obj)
        {
            var m = TypeModel.Create();
            m.SkipCompiledVsNotCheck = true;
            var clone = m.DeepClone(obj);

            Assert.That(clone.Compact, Is.EqualTo(obj.Compact));
            Assert.That(clone.MinimalEnchancement, Is.EqualTo(obj.MinimalEnchancement));
            Assert.That(clone.Reference, Is.EqualTo(obj.Reference));
            Assert.That(clone.LateReference, Is.EqualTo(obj.LateReference));
            Assert.That(clone.SameReference, Is.SameAs(clone.Reference));
        }
        
    }
}