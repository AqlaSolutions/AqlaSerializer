using AqlaSerializer;
using NUnit.Framework;
using AqlaSerializer.Meta;

namespace AqlaSerializer.unittest.AqlaAttributes
{
    [TestFixture]
    public class ImplicitFallback
    {
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

        public class TestInherited : TestClass
        {
            public int PublicProperty3 { get; set; }
            [NonSerializableMember]
            public int PublicProperty4 { get; set; }
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
            _model.AutoAddStrategy = new AutoAddStrategy(_model) { ImplicitFallbackMode = ImplicitFieldsMode.PublicProperties };
            var t = _model.Add(typeof(TestClass), true);
            var fields = t.GetFields();
            Assert.AreEqual(2, fields.Length);
            Assert.AreEqual("PublicProperty", fields[0].Member.Name);
            Assert.AreEqual("PublicProperty2", fields[1].Member.Name);
        }

        [Test]
        public void ShouldAddMissingAsImplicitProperties()
        {
            _model.AutoAddStrategy = new AutoAddStrategy(_model) { ImplicitFallbackMode = ImplicitFieldsMode.PublicProperties };
            _model.AutoAddMissingTypes = true;
            Check();
        }

        [ExpectedException]
        [Test]
        public void ShouldNotWorkWithoutFallback()
        {
            Check();
        }

        private void Check()
        {
            var obj = new TestInherited() { PublicProperty = 1, PublicProperty2 = 2, PublicProperty3 = 3, PublicProperty4 = 4};
            var clone = (TestInherited)_model.DeepClone(obj);

            Assert.AreEqual(0, clone.PublicField);
            Assert.AreEqual(obj.PublicProperty, clone.PublicProperty);
            Assert.AreEqual(obj.PublicProperty2, clone.PublicProperty2);
            Assert.AreEqual(obj.PublicProperty3, clone.PublicProperty3);
            Assert.AreEqual(0, clone.PublicProperty4);
            clone.CheckPrivateValues(0, 0);
        }
    }
}