using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class NestedDebugSchema
    {
        [SerializableType]
        public class Container
        {
            public Contained Value { get; set; }
        }

        [SerializableType]
        public class Contained
        {
            public Nested Value { get; set; }
        }

        [SerializableType]
        public class Nested
        {
        }

        [Test]
        public void Execute()
        {
            var tm = TypeModel.Create();
            var s = tm.GetDebugSchema(typeof(Container));
            Assert.That(s, Is.EqualTo(@"Root : Container
 -> NetObject : Container = AsReference, UseConstructor
 -> ModelType : Container
 -> LinkTo [AqlaSerializer.unittest.Aqla.NestedDebugSchema+Container]

AqlaSerializer.unittest.Aqla.NestedDebugSchema+Container:
Type : Container
{
    #1
     -> Container.Value
     -> NetObject : Contained = AsReference, UseConstructor, WithNullWireType
     -> ModelType : Contained
     -> LinkTo [AqlaSerializer.unittest.Aqla.NestedDebugSchema+Contained]
}


AqlaSerializer.unittest.Aqla.NestedDebugSchema+Contained:
Type : Contained
{
    #1
     -> Contained.Value
     -> NetObject : Nested = AsReference, UseConstructor, WithNullWireType
     -> ModelType : Nested
     -> LinkTo [AqlaSerializer.unittest.Aqla.NestedDebugSchema+Nested]
}


AqlaSerializer.unittest.Aqla.NestedDebugSchema+Nested:
Type : Nested
{
}


"));
        }
    }
}