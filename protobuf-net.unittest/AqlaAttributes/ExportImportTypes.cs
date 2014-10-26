using System;
using System.Collections;
using System.Reflection;
using AqlaSerializer;
using NUnit.Framework;
using ProtoBuf.Meta;
using System.Linq;

namespace ProtoBuf.unittest.Aqla
{
    [TestFixture]
    public class ExportImportTypes
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

        [SerializableType]
        public class TestInherited2 : TestClass
        {
            public int PublicProperty4 { get; set; }
        }

        [SerializableType]
        public class Another
        {
            public int PublicProperty { get; set; }
        }

        RuntimeTypeModel _model;

        [SetUp]
        public void Setup()
        {
            _model = TypeModel.Create();
        }


        [Test]
        public void ShouldWorkForLocal()
        {
            _model.Add(new[] { typeof(TestClass), typeof(TestInherited), typeof(TestInherited2), typeof(Another) }, true);
            Check(true);
        }


        [Test]
        public void ShouldWorkForAssembly()
        {
            _model.Add(Assembly.GetExecutingAssembly(), true, true);
            Check(true); // replace with false if does not pass! it's ok!
        }

        private void Check(bool checkSubTypes)
        {
            var otherModel = TypeModel.Create();
            otherModel.InitializeWithExactTypes(_model.Types, true);

            Assert.IsTrue(otherModel.Types.SequenceEqual(_model.Types));

            // don't check this for assembly because types can be added without auto
            if (!checkSubTypes) return;
            MetaType[] modelMetaTypes = _model.MetaTypes;
            MetaType[] otherMetaTypes = otherModel.MetaTypes;
            for (int i = 0; i < otherMetaTypes.Length; i++)
            {
                SubType[] otherSubTypes = otherMetaTypes[i].GetSubtypes();
                SubType[] modelSubTypes = modelMetaTypes[i].GetSubtypes();

                Assert.AreEqual(otherSubTypes.Length, modelSubTypes.Length, "Subtypes of " + otherMetaTypes[i].Type.FullName);

                for (int j = 0; j < otherSubTypes.Length; j++)
                {
                    Assert.AreEqual(otherSubTypes[j].DerivedType.Type, modelSubTypes[j].DerivedType.Type);
                    Assert.AreEqual(otherSubTypes[j].FieldNumber, modelSubTypes[j].FieldNumber);
                }
            }
        }

        [Test]
        public void ShouldSerializeTypesList()
        {
            _model.Add(Assembly.GetExecutingAssembly(), true, true);
            Type[] types = _model.Types;
            Type[] cloned = (Type[])_model.DeepClone(types);

            Assert.IsTrue(cloned.SequenceEqual(types));
        }
    }
}