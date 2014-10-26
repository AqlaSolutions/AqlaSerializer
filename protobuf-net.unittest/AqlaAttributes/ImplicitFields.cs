using System;
using System.Reflection;
using AqlaSerializer;
using NUnit.Framework;
using ProtoBuf.Meta;

namespace ProtoBuf.unittest.AqlaAttributes
{
    [TestFixture]
    public class ImplicitFields
    {
        [SerializableType]
        public class TestClass
        {
            public int PublicProperty { get; set; }
            public int PublicProperty2 { get; set; }
            public int PublicField;
            int _privateProperty { get; set; }
            int _privateField;

            public void SetPrivateValues(int property, int field)
            {
                _privateProperty = property;
                _privateField = field;
            }

            public void CheckPrivateValues(int property, int field)
            {
                Assert.AreEqual(property, _privateProperty);
                Assert.AreEqual(field, _privateField);
            }
        }
        
        RuntimeTypeModel _model;

        [SetUp]
        public void Setup()
        {
            _model = TypeModel.Create();
        }

        [Test]
        public void ShouldBeImplicitPublicPropertiesByDefault()
        {
            var t = _model.Add(typeof(TestClass), true);
            var fields = t.GetFields();
            Assert.AreEqual(2, fields.Length);
            Assert.AreEqual("PublicProperty", fields[0].Member.Name);
            Assert.AreEqual("PublicProperty2", fields[1].Member.Name);
        }

        [Test]
        public void ShouldAddMissingAsImplicitProperties()
        {
            var obj = new TestClass() { PublicProperty = 1, PublicProperty2 = 2, PublicField = 3 };
            obj.SetPrivateValues(4, 5);
            var clone = (TestClass)_model.DeepClone(obj);

            Assert.AreEqual(clone.PublicField, 0);
            Assert.AreEqual(clone.PublicProperty, obj.PublicProperty);
            Assert.AreEqual(clone.PublicProperty2, obj.PublicProperty2);
            clone.CheckPrivateValues(0, 0);
        }
    }
}