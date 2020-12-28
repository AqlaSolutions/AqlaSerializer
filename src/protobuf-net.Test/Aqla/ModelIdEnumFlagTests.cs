using System;
using System.IO;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class ModelIdEnumFlagTests
    {
        [Flags]
        public enum FlagEnum
        {
            A = 1 << 0,
            B = 1 << 1,
            C = 1 << 2,
            D = 1 << 3,
            E = 1 << 4,
            F = 1 << 5
        }
        
        [Test]
        public void ShouldIncludePartialEnumMatch()
        {
            Assert.That(new EnumFlagModelId<FlagEnum>(FlagEnum.A).Equals(FlagEnum.A | FlagEnum.B | FlagEnum.C));
            Assert.That(new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.B | FlagEnum.C).Equals(FlagEnum.A));
            Assert.That(new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.B | FlagEnum.C).Equals(FlagEnum.A | FlagEnum.D));
            Assert.That(new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.D).Equals(FlagEnum.A | FlagEnum.B | FlagEnum.C));
        }

        [Test]
        public void ShouldExcludeWhenNoMatch()
        {
            Assert.That(!new EnumFlagModelId<FlagEnum>(FlagEnum.A).Equals(FlagEnum.B | FlagEnum.C));
            Assert.That(!new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.B | FlagEnum.C).Equals(FlagEnum.D));
            Assert.That(!new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.B | FlagEnum.C).Equals(FlagEnum.E | FlagEnum.D));
            Assert.That(!new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.D).Equals(FlagEnum.E | FlagEnum.B | FlagEnum.C));
        }

        [Test]
        public void ShouldIncludeExactEnumMatch()
        {
            Assert.That(new EnumFlagModelId<FlagEnum>(FlagEnum.B).Equals(FlagEnum.B));
        }

        [Test]
        public void ShouldIncludeExactNumericMatch()
        {
            Assert.That(new EnumFlagModelId<FlagEnum>(FlagEnum.B).Equals((int)FlagEnum.B));
            Assert.That(new EnumFlagModelId<FlagEnum>(FlagEnum.B | FlagEnum.C).Equals((int)(FlagEnum.B | FlagEnum.C)));
        }

        [Test]
        public void ShouldExcludeTargetPartialNumericMatch()
        {
            // target can't select any of multiple
            Assert.That(!new EnumFlagModelId<FlagEnum>(FlagEnum.A).Equals((int)(FlagEnum.A | FlagEnum.B | FlagEnum.C)));
            Assert.That(!new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.B | FlagEnum.C).Equals((int)(FlagEnum.A | FlagEnum.D)));
            Assert.That(!new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.D).Equals((int)(FlagEnum.A | FlagEnum.B | FlagEnum.C)));
        }

        [Test]
        public void ShouldIncludeSourcePartialNumericMatch()
        {
            // source can select multiple
            // target still can specify AND combination
            Assert.That(new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.B | FlagEnum.C).Equals((int)FlagEnum.A));
            Assert.That(new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.B | FlagEnum.C).Equals((int)(FlagEnum.A | FlagEnum.B)));
        }

        [Test]
        public void ShouldExcludeNumericWhenNoMatch()
        {
            Assert.That(!new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.B | FlagEnum.C).Equals(123));
        }

        [Test]
        public void ShouldExcludeZero()
        {
            Assert.That(!new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.B | FlagEnum.C).Equals(0));
            Assert.That(!new EnumFlagModelId<FlagEnum>(0).Equals(FlagEnum.A | FlagEnum.B | FlagEnum.C));
            Assert.That(!new EnumFlagModelId<FlagEnum>(0).Equals(0));
            Assert.That(!new EnumFlagModelId<FlagEnum>(0).Equals((FlagEnum)0));
            Assert.That(!new EnumFlagModelId<FlagEnum>(0).Equals(123));
            Assert.That(!new EnumFlagModelId<FlagEnum>((FlagEnum)123).Equals(0));
        }

        [Test]
        public void ShouldExcludeNonConvertable()
        {
            Assert.That(!new EnumFlagModelId<FlagEnum>(FlagEnum.A).Equals(new object()));
            Assert.That(!new EnumFlagModelId<FlagEnum>(FlagEnum.A).Equals(new MemoryStream()));
        }

        [Test]
        public void ShouldBeEqualToSimilarInstance()
        {
            Assert.That(new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.B | FlagEnum.C).Equals(new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.B | FlagEnum.C)));
            Assert.That(new EnumFlagModelId<FlagEnum>(0).Equals(new EnumFlagModelId<FlagEnum>(0)));
        }

        [Test]
        public void ShouldBeNotEqualToNotSimilarInstance()
        {
            Assert.That(!new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.B | FlagEnum.C).Equals(new EnumFlagModelId<FlagEnum>(FlagEnum.A | FlagEnum.B)));
            Assert.That(!new EnumFlagModelId<FlagEnum>(FlagEnum.A).Equals(new EnumFlagModelId<FlagEnum>(0)));
            Assert.That(!new EnumFlagModelId<FlagEnum>(0).Equals(new EnumFlagModelId<FlagEnum>(FlagEnum.A)));
        }
    }
}