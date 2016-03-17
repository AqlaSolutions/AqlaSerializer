using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class SubTypeDebugSchema
    {
        [SerializableType]
        public class Container
        {
            public Nested2Base A_Value2Base { get; set; }
            public Contained SubTypeValue { get; set; }
        }

        [SerializableType]
        public class ContainedBase
        {
            public Nested1Base Value1Base { get; set; }
            public Nested1Derived Value1 { get; set; }
        }

        [SerializableType]
        public class Contained : ContainedBase
        {
            public Nested2Derived Value2 { get; set; }
        }

        [SerializableType]
        public class Nested1Base
        {
        }

        [SerializableType]
        public class Nested1Derived : Nested1Base
        {
        }

        [SerializableType]
        public class Nested2Base
        {
        }

        [SerializableType]
        public class Nested2Derived : Nested2Base
        {
        }

        [Test]
        public void Execute()
        {
            var tm = TypeModel.Create();
            var s = tm.GetDebugSchema(typeof(Container));
            Assert.That(s, Is.EqualTo(@"Root : Container
 -> NetObject : Container = AsReference, UseConstructor
 -> Type : Container
{
    #1
     -> Container.A_Value2Base
     -> NetObject : Nested2Base = AsReference, UseConstructor, WithNullWireType
     -> ModelType : Nested2Base
     -> LinkTo [AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+Nested2Base]
    ,
    #2
     -> Container.SubTypeValue
     -> NetObject : Contained = AsReference, UseConstructor, WithNullWireType
     -> ModelType : Contained
     -> LinkTo [AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+ContainedBase]
}

AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+Nested2Base:
Type : Nested2Base
{
    #200: SubType 
     -> ModelType : Nested2Derived
     -> LinkTo [AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+Nested2Derived]
}


AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+Nested2Derived:
Type : Nested2Derived
{
}


AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+ContainedBase:
Type : ContainedBase
{
    #200: SubType 
     -> ModelType : Contained
     -> LinkTo [AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+Contained]
    ,
    #1
     -> ContainedBase.Value1
     -> NetObject : Nested1Derived = AsReference, UseConstructor, WithNullWireType
     -> ModelType : Nested1Derived
     -> LinkTo [AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+Nested1Base]
    ,
    #2
     -> ContainedBase.Value1Base
     -> NetObject : Nested1Base = AsReference, UseConstructor, WithNullWireType
     -> ModelType : Nested1Base
     -> LinkTo [AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+Nested1Base]
}


AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+Contained:
Type : Contained
{
    #1
     -> Contained.Value2
     -> NetObject : Nested2Derived = AsReference, UseConstructor, WithNullWireType
     -> ModelType : Nested2Derived
     -> LinkTo [AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+Nested2Base]
}


AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+Nested1Base:
Type : Nested1Base
{
    #200: SubType 
     -> ModelType : Nested1Derived
     -> LinkTo [AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+Nested1Derived]
}


AqlaSerializer.unittest.Aqla.SubTypeDebugSchema+Nested1Derived:
Type : Nested1Derived
{
}


"));
        }
    }
}