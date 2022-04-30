using ProtoBuf.Meta;
using System;
using NUnit.Framework;

namespace ProtoBuf.Issues
{
    public partial class Issue401
    {
        [Test]
        public void IsDefinedWorksWhenAddingTypes()
        {
            var type = typeof(MyClass);
            var m = RuntimeTypeModel.Create();

            Assert.False(m.IsDefined(type));

            _ = m.Add(type, true);

            Assert.True(m.IsDefined(type));
        }

        [Test]
        public void IsDefinedWorksWhenUsingIndexer()
        {
            var type = typeof(MyClass);
            var m = RuntimeTypeModel.Create();

            Assert.False(m.IsDefined(type));
            var mt = m[type];
            Assert.NotNull(mt);
            Assert.True(m.IsDefined(type));
        }

        class MyClass
        {
            public string MyProp { get; set; }
        }

        [Test]
        public void IsDefinedWorksWhenAddingSubTypes()
        {
            var baseType = typeof(MyBaseClass);
            var subType = typeof(MyDerivedClass);
            var m = RuntimeTypeModel.Create();

            Assert.False(m.IsDefined(baseType));
            Assert.False(m.IsDefined(subType));

            var protoType = m.Add(baseType, true);
            Assert.True(m.IsDefined(baseType));
            Assert.False(m.IsDefined(subType));

            protoType.AddSubType(100, subType);
            Assert.True(m.IsDefined(baseType));
            Assert.True(m.IsDefined(subType));
        }

        class MyBaseClass { }
        class MyDerivedClass : MyBaseClass { }
    }
}
