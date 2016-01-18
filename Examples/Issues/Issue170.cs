// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using AqlaSerializer;
using System.Linq;
using System;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue170
    {

        [Test]
        public void ArrayWithoutNullContentShouldClone()
        {
            var arr = new[] { "aaa","bbb" };
            Assert.IsTrue(Serializer.DeepClone(arr).SequenceEqual(arr));
        }

        [Test]
        public void ArrayWithNullContentShouldNotThrow()
        {
            var tm = TypeModel.Create(true);
            var arr = new[] { "aaa", null, "bbb" };
            Assert.That(tm.DeepClone(arr), Is.EqualTo(arr));
        }
    }
}
