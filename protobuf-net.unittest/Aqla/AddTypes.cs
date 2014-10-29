using System;
using System.Reflection;
using AqlaSerializer;
using NUnit.Framework;
using ProtoBuf.Meta;

namespace ProtoBuf.unittest.Aqla
{
    [TestFixture]
    public class AddTypes
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

        [SerializableType]
        public class TestInherited : TestClass
        {
            public int PublicProperty3 { get; set; }
        }

        RuntimeTypeModel _model;

        [SetUp]
        public void Setup()
        {
            _model = TypeModel.Create();
        }


        [Test]
        public void ShouldAutoAddAllBaseTypesCorrectly()
        {
            _model.Add(Assembly.GetExecutingAssembly(), true, true, true);
            CheckInherited();
        }

        [Test]
        public void ShouldAutoAddBaseTypesCorrectlyEvenIfNotSpecified()
        {
            _model.Add(new Type[] { typeof(TestInherited) }, true);
            CheckInherited();
        }

        [Test]
        public void ShouldNotAutoAddDerrivedTypesIfNotSpecified()
        {
            _model.Add(new Type[] { typeof(TestClass) }, true);
            _model.AutoAddMissingTypes = false;
            try
            {
                CheckInherited();
            }
            catch { return; }
            Assert.Fail();
        }

        [Test]
        public void ShouldAutoAddDerrivedTypesIfSpecified()
        {
            _model.Add(new Type[] { typeof(TestInherited), typeof(TestClass) }, true);
            CheckInherited();
        }

        private void CheckInherited()
        {
            var obj = new TestInherited() { PublicProperty = 1, PublicProperty2 = 2, PublicProperty3 = 3 };
            var clone = (TestInherited)_model.DeepClone(obj);

            Assert.AreEqual(clone.PublicProperty, obj.PublicProperty);
            Assert.AreEqual(clone.PublicProperty2, obj.PublicProperty2);
            Assert.AreEqual(clone.PublicProperty3, obj.PublicProperty3);
            clone.CheckPrivateValues(0, 0);
        }
    }
}