using System;
using System.Collections;
using System.IO;
using System.Reflection;
using AqlaSerializer;
using NUnit.Framework;
using AqlaSerializer.Meta;
using System.Linq;
using AqlaSerializer.Meta.Data;

namespace AqlaSerializer.unittest.Aqla
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
            _rnd = new Random(123124);
        }


        [Test]
        public void ShouldWorkForLocal()
        {
            _model.Add(new[] { typeof(TestClass), typeof(TestInherited), typeof(TestInherited2), typeof(Another) }, true);
            Check(false);
            Check(true);
        }
        
        [Test]
        public void ShouldWorkForAssembly()
        {
            _model.Add(typeof(TestSurrogate), true);
            _model.Add(typeof(Aqla.Test), false).SetSurrogate(typeof(TestSurrogate));
            _model.Add(Assembly.GetExecutingAssembly(), true, true);
            Check(false);
            Check(true);
        }
        
        Random _rnd = new Random();

        private void Check(bool shuffle)
        {
            var otherModel = TypeModel.Create();
            using (var ms = new MemoryStream())
            {
                _model.Serialize(ms, _model.ExportTypeRelations());
                ms.Position = 0;
                var rel = _model.Deserialize<ModelTypeRelationsData>(ms);
                if (shuffle) // types order should not matter (only subtypes)
                {
                    rel.Types = rel.Types.OrderBy(x => _rnd.Next(0, rel.Types.Length + 1)).ToArray();
                }
                otherModel.ImportTypeRelations(rel, true);
            }
            var newTypes = otherModel.Types.Except(_model.Types).ToArray();
            var removedTypes = _model.Types.Except(otherModel.Types).ToArray();

            Assert.AreEqual(0, newTypes.Length);
            Assert.AreEqual(0, removedTypes.Length);
            if (!shuffle)
            {
                var inequal = otherModel.Types.Where((t, i) => t != _model.Types[i]).ToArray();
                Assert.AreEqual(0, inequal.Length);
            }

            MetaType[] modelMetaTypes = _model.MetaTypes;
            for (int i = 0; i < modelMetaTypes.Length; i++)
            {
                Type type = modelMetaTypes[i].Type;
                SubType[] otherSubTypes = otherModel.FindWithoutAdd(type).GetSubtypes();
                SubType[] modelSubTypes = modelMetaTypes[i].GetSubtypes();

                Assert.AreEqual(modelSubTypes.Length, otherSubTypes.Length, "Subtypes of " + type.FullName);

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