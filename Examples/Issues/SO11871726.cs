// Modified by Vladyslav Taranov for AqlaSerializer, 2016

using AqlaSerializer;
using NUnit.Framework;
using AqlaSerializer.Meta;
using System;
using System.Runtime.Serialization;

namespace Examples.Issues
{
    [TestFixture]
    public class SO11871726
    {
        [Test]
        public void ExecuteWithoutAutoAddProtoContractTypesOnlyShouldWork()
        {
            var model = TypeModel.Create();
            Assert.IsInstanceOf(typeof(Foo), model.DeepClone(new Foo()));
        }
        [Test]
        public void ExecuteWithAutoAddProtoContractTypesOnlyShouldFail()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => {
                var model = TypeModel.Create();
                model.AutoAddStrategy = new AutoAddStrategy(model) { AcceptableAttributes = AttributeType.ProtoBuf };
                Assert.IsInstanceOf(typeof(Foo), model.DeepClone(new Foo()));
            });
            Assert.That(ex.Message, Is.EqualTo("Type is not expected, and no contract can be inferred: Examples.Issues.SO11871726+Foo"));
        }

        [DataContract]
        public class Foo { }
    }
}
