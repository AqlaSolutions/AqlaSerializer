using ProtoBuf.Meta;
using NUnit.Framework;

namespace ProtoBuf.Issues
{
    public class Issue402
    {
        [Test]
        public void ZeroWithoutScaleShouldRoundtrip() => CheckZero(0m);

        [Test]
        public void MinusZeroWithoutScaleShouldRoundtrip() => CheckZero(new decimal(0,0,0,true,0));

        [Test]
        public void ZeroWithScaleShouldRoundtrip() => CheckZero(0.0000000m);

        static RuntimeTypeModel Serializer;
        static Issue402()
        {
            Serializer = RuntimeTypeModel.Create();
            Serializer.UseImplicitZeroDefaults = false;
        }
        private void CheckZero(decimal value)
        {
            var clone = ((Foo)Serializer.DeepClone(new Foo { Value = value })).Value;
            
            var origBits = decimal.GetBits(value);
            var cloneBits = decimal.GetBits(clone);

            Assert.AreEqual(string.Join(",", origBits), string.Join(",", cloneBits));
        }
        [ProtoContract]
        class Foo
        {
            [ProtoMember(1)]
            public decimal Value { get; set; }
        }
    }
}
