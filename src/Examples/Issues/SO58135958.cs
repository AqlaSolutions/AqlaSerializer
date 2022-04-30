using System.ComponentModel;
using NUnit.Framework;

namespace ProtoBuf.Issues
{
    public class SO58135958
    {
        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class Foo
        {
            public PricingFlags Flags {get;set;}
        }

        public enum PricingFlags : long
        {
            [Description("Aggregate")]
            Aggregate = 1L << 7,

            Something = 4296080913,
        }

        [Test]
        [TestCase(PricingFlags.Aggregate)]
        [TestCase(PricingFlags.Something)]
        [TestCase((PricingFlags)0)]
        [TestCase((PricingFlags)10)]
        [TestCase((PricingFlags)(-10))]
        [TestCase((PricingFlags)long.MinValue)]
        [TestCase((PricingFlags)(long.MinValue + 10))]
        [TestCase((PricingFlags)(long.MaxValue - 10))]
        public void CheckLongEnumRoundTrips(PricingFlags value)
        {
            var obj = new Foo { Flags = value };
            var clone = Serializer.DeepClone(obj);
            Assert.AreNotSame(obj, clone);
            Assert.AreEqual(value, clone.Flags);
        }
    }
}
