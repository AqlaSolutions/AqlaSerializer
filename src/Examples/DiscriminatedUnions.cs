using NUnit.Framework;

namespace ProtoBuf
{
    public class DiscriminatedUnions
    {
        [Test]
        public void BasicUsage()
        {
            DiscriminatedUnion32 union;
            
            union = new DiscriminatedUnion32(4, 42);
            Assert.True(union.Is(4));
            Assert.AreEqual(4, union.Discriminator);
            Assert.AreEqual(42, union.Int32);

            DiscriminatedUnion32.Reset(ref union, 3); // should do nothing
            Assert.True(union.Is(4));
            Assert.AreEqual(4, union.Discriminator);
            Assert.AreEqual(42, union.Int32);

            DiscriminatedUnion32.Reset(ref union, 4); // should reset
            Assert.False(union.Is(4));
            Assert.True(union.Is(0));
            //Assert.AreEqual(0, union.Discriminator);

            //union = new DiscriminatedUnion32(4, 42);

            //union = default;
            //Assert.True(union.Is(0));
            //Assert.AreEqual(0, union.Discriminator);

        }
    }
}
