using System.Collections;
using System.Reflection;
using AqlaSerializer.Internal;
using AqlaSerializer.unittest.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class AccessorsEmitTest
    {
        public struct TestStruct
        {
            public int Value { get; set; }
            public object Ref { get; set; }
        }

        public struct TestStructField
        {
            public int Value;
            public object Ref;
        }

        [Test]
        public void StructValue()
        {
            var s = new TestStruct() { Value = 123 };
            var getter = AccessorsCache.GetAccessors(typeof(TestStruct).GetProperty("Value")).Get;
            object v = getter(s);
            Assert.That(v, Is.EqualTo(s.Value));
        }

        [Test]
        public void StructRef()
        {
            var s = new TestStruct() { Ref = new object() };
            var getter = AccessorsCache.GetAccessors(typeof(TestStruct).GetProperty("Ref")).Get;
            object v = getter(s);
            Assert.That(v, Is.SameAs(s.Ref));
        }

        [Test]
        public void StructValueField()
        {
            var s = new TestStructField() { Value = 123 };
            var getter = AccessorsCache.GetAccessors(typeof(TestStructField).GetField("Value")).Get;
            object v = getter(s);
            Assert.That(v, Is.EqualTo(s.Value));
        }

        [Test]
        public void StructRefField()
        {
            var s = new TestStructField() { Ref = new object() };
            var getter = AccessorsCache.GetAccessors(typeof(TestStructField).GetField("Ref")).Get;
            object v = getter(s);
            Assert.That(v, Is.SameAs(s.Ref));
        }

        [Test]
        public void PocoStruct()
        {
            var s = new PocoStruct.Company();
            s.Employees.Add(new PocoStruct.Employee());
            PropertyInfo prop = s.GetType().GetProperty("Employees");
            var getter = AccessorsCache.GetAccessors(prop).Get;
            object v = getter(s);
            s.Employees.Add(new PocoStruct.Employee());
            Assert.That(((IList)v).Count, Is.EqualTo(s.Employees.Count));
        }
    }
}