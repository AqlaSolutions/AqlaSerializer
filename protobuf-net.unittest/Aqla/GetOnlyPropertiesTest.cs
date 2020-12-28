using System;
using System.Reflection;
using AqlaSerializer;
using NUnit.Framework;
using AqlaSerializer.Meta;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class ImplicitFields
    {
        [SerializableType(ImplicitFields = ImplicitFieldsMode.PublicProperties)]
        public class TestClass
        {
            public int PublicProperty { get; }
#if NETCOREAPP
            public int PublicProperty2 { get; init; }
#else
            public int PublicProperty2 { get; set; }
#endif
            // not applicable
            // only where !CanWrite
            // when no setter at all
            //public int PublicProperty3 { get; private set; }

            public TestClass()
            {
            }

            public TestClass(int publicProperty)
            {
                PublicProperty = publicProperty;
            }
        }
        
        [Test]
        public void ShouldUseBackingFields()
        {
            var obj = new TestClass(3) { PublicProperty2 = 2 };
            
            RuntimeTypeModel m = TypeModel.Create();
            m.SkipCompiledVsNotCheck = true;
            m.AutoCompile = false;
            ((AutoAddStrategy)m.AutoAddStrategy).UseBackingFieldsIfNoSetter = true;
            var clone = (TestClass)m.DeepClone(obj);

            Assert.AreEqual(obj.PublicProperty, clone.PublicProperty);
            Assert.AreEqual(obj.PublicProperty2, clone.PublicProperty2);
        }
    }
}