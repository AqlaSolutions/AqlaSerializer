// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using AqlaSerializer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    [TestFixture]
    public class SO16756104
    {
        [Test]
        public void TestNullableDoubleList()
        {
            var tm = TypeModel.Create(true, ProtoCompatibilitySettingsValue.Default);
            var list = new List<double?> { 1, null, 2 };
            Assert.That(tm.DeepClone(list), Is.EqualTo(list));
        }

        [Test]
        public void TestNullableInt32List()
        {
            var tm = TypeModel.Create(true, ProtoCompatibilitySettingsValue.Default);
            var list = new List<int?> { 1, null, 2 };
            Assert.That(tm.DeepClone(list), Is.EqualTo(list));
        }
        
        [Test]
        public void TestNullableStringList()
        {
            var tm = TypeModel.Create(true, ProtoCompatibilitySettingsValue.Default);
            var list = new List<string> { "abc", null, "def" };
            Assert.That(tm.DeepClone(list), Is.EqualTo(list));
        }
    }
}
