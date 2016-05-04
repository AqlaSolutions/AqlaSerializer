using System.Collections.Generic;
using AqlaSerializer.Meta;
using NUnit.Framework;
using ProtoBuf;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class LegacyTupleMode
    {
        [ProtoContract]
        public class Element
        {
        }

        [ProtoContract(AsReferenceDefault = true)]
        public class ElementDef
        {
        }

        [ProtoContract]
        public class Container
        {
            [ProtoMember(1)]
            public Dictionary<string, List<LegacyTupleMode.Element>> Foo { get; set; }

            [ProtoMember(2)]
            public Dictionary<Element, ElementDef> Bar { get; set; }
        }

        [Test]
        public void Legacy()
        {
            var tm = TypeModel.Create();
            tm.SkipForcedAdvancedVersioning = true;
            ((AutoAddStrategy)tm.AutoAddStrategy).UseLegacyTupleFields = true;
            var s = tm.GetDebugSchema(typeof(Container));
            Assert.That(s, Is.EqualTo(@"Root : Container
 -> NetObject : Container = AsReference, UseConstructor
 -> Type : Container
{
    #1
     -> Container.Foo
     -> NetObject : Dictionary`2 = UseConstructor, WithNullWireType
     -> List : Dictionary`2 = NewPacked, Append
     -> ModelType : KeyValuePair`2
     -> LinkTo [System.Collections.Generic.KeyValuePair`2[System.String,System.Collections.Generic.List`1[AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element]]]
    ,
    #2
     -> Container.Bar
     -> NetObject : Dictionary`2 = UseConstructor, WithNullWireType
     -> List : Dictionary`2 = NewPacked, Append
     -> ModelType : KeyValuePair`2
     -> LinkTo [System.Collections.Generic.KeyValuePair`2[AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element,AqlaSerializer.unittest.Aqla.LegacyTupleMode+ElementDef]]
}

System.Collections.Generic.KeyValuePair`2[System.String,System.Collections.Generic.List`1[AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element]]:
Tuple : KeyValuePair`2
{
    #1: Field 
     -> NetObject : String = AsReference, UseConstructor, LateSet, WithNullWireType
     -> WireType : String = String
     -> String
    ,
    #2: Field 
     -> NoNull : List`1
     -> List : List`1 = NewPacked, Append
     -> NetObject : Element = UseConstructor
     -> ModelType : Element
     -> LinkTo [AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element]
}


AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element:
Type : Element
{
}


System.Collections.Generic.KeyValuePair`2[AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element,AqlaSerializer.unittest.Aqla.LegacyTupleMode+ElementDef]:
Tuple : KeyValuePair`2
{
    #1: Field 
     -> NetObject : Element = UseConstructor, WithNullWireType
     -> ModelType : Element
     -> LinkTo [AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element]
    ,
    #2: Field 
     -> NetObject : ElementDef = AsReference, UseConstructor, WithNullWireType
     -> ModelType : ElementDef
     -> LinkTo [AqlaSerializer.unittest.Aqla.LegacyTupleMode+ElementDef]
}


AqlaSerializer.unittest.Aqla.LegacyTupleMode+ElementDef:
Type : ElementDef
{
}


"));
        }

        [Test]
        public void Normal()
        {
            var tm = TypeModel.Create();
            var s = tm.GetDebugSchema(typeof(Container));
            Assert.That(s, Is.EqualTo(@"Root : Container
 -> NetObject : Container = AsReference, UseConstructor
 -> Type : Container
{
    #1
     -> Container.Foo
     -> NetObject : Dictionary`2 = UseConstructor, WithNullWireType
     -> List : Dictionary`2 = NewPacked, Append
     -> NetObject : KeyValuePair`2 = UseConstructor
     -> ModelType : KeyValuePair`2
     -> LinkTo [System.Collections.Generic.KeyValuePair`2[System.String,System.Collections.Generic.List`1[AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element]]]
    ,
    #2
     -> Container.Bar
     -> NetObject : Dictionary`2 = UseConstructor, WithNullWireType
     -> List : Dictionary`2 = NewPacked, Append
     -> NetObject : KeyValuePair`2 = UseConstructor
     -> ModelType : KeyValuePair`2
     -> LinkTo [System.Collections.Generic.KeyValuePair`2[AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element,AqlaSerializer.unittest.Aqla.LegacyTupleMode+ElementDef]]
}

System.Collections.Generic.KeyValuePair`2[System.String,System.Collections.Generic.List`1[AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element]]:
Tuple : KeyValuePair`2
{
    #1: Field 
     -> NetObject : String = AsReference, UseConstructor, LateSet, WithNullWireType
     -> WireType : String = String
     -> String
    ,
    #2: Field 
     -> NetObject : List`1 = AsReference, UseConstructor, WithNullWireType
     -> List : List`1 = NewPacked, Append
     -> NetObject : Element = UseConstructor
     -> ModelType : Element
     -> LinkTo [AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element]
}


AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element:
Type : Element
{
}


System.Collections.Generic.KeyValuePair`2[AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element,AqlaSerializer.unittest.Aqla.LegacyTupleMode+ElementDef]:
Tuple : KeyValuePair`2
{
    #1: Field 
     -> NetObject : Element = UseConstructor, WithNullWireType
     -> ModelType : Element
     -> LinkTo [AqlaSerializer.unittest.Aqla.LegacyTupleMode+Element]
    ,
    #2: Field 
     -> NetObject : ElementDef = AsReference, UseConstructor, WithNullWireType
     -> ModelType : ElementDef
     -> LinkTo [AqlaSerializer.unittest.Aqla.LegacyTupleMode+ElementDef]
}


AqlaSerializer.unittest.Aqla.LegacyTupleMode+ElementDef:
Type : ElementDef
{
}


"));
        }
    }
}