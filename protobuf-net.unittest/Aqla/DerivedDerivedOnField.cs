using AqlaSerializer;
using NUnit.Framework;
using ProtoBuf.Meta;

namespace ProtoBuf.unittest.Aqla
{
    [TestFixture]
    public class DerivedDerivedOnField
    {
        [SerializableType]
        public class ContainerClass
        {
            public Base Contained { get; set; }
        }

        [SerializableType]
        public class Base
        {
            public int PublicProperty { get; set; }
            public int PublicProperty2 { get; set; }
        }

        [SerializableType]
        public class MidInherited : Base
        {
            public int PublicProperty5 { get; set; }
        }

        [SerializableType]
        public class Inherited : MidInherited
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
        public void Execute()
        {
            var obj = new Inherited() { PublicProperty = 1, PublicProperty2 = 2, PublicProperty3 = 3, PublicProperty4 = 4, PublicProperty5 = 5 };
            var clone = (Inherited)_model.DeepClone(obj);

            Assert.AreEqual(obj.PublicProperty, clone.PublicProperty);
            Assert.AreEqual(obj.PublicProperty2, clone.PublicProperty2);
            Assert.AreEqual(obj.PublicProperty3, clone.PublicProperty3);
            Assert.AreEqual(obj.PublicProperty5, clone.PublicProperty5);
            Assert.AreEqual(0, clone.PublicProperty4);
        }
    }
}