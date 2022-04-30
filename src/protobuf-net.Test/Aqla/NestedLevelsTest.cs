using System.Collections.Generic;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class NestedLevelsTest
    {
        [SerializableType(ValueFormat.LateReference)]
        public class AClass
        {
        }

        [SerializableType]
        public class NestedCollectionClass : List<List<int[]>>
        {
        }

        [SerializableType]
        public class Foo
        {
            [SerializableMember(1, ValueFormat.MinimalEnhancement, CollectionFormat = CollectionFormat.Enhanced)]
            public int[] SimpleCollectionInherited { get; set; }

            [SerializableMember(2, ValueFormat.Reference, CollectionFormat = CollectionFormat.Enhanced)]
            public int[] SimpleCollectionInheritedReference { get; set; }

            [SerializableMember(3, ValueFormat.MinimalEnhancement, CollectionFormat = CollectionFormat.Enhanced)]
            [SerializableMemberNested(1, ValueFormat.Compact)]
            public int[] SimpleCollectionSpecifiedCompact { get; set; }

            [SerializableMember(4, ValueFormat.Compact)]
            [SerializableMemberNested(1, ValueFormat.Reference)]
            [SerializableMemberNested(3, ValueFormat.Compact)]
            public NestedCollectionClass[] NestedCollection { get; set; }

            [SerializableMember(5, ValueFormat.MinimalEnhancement, CollectionFormat = CollectionFormat.Enhanced)]
            [SerializableMemberNested(1, ValueFormat.MinimalEnhancement)]
            public int[] SimpleCollectionSpecifiedMinimal { get; set; }
        }

        [Test]
        public void ExecuteFoo()
        {
            var tm = TypeModel.Create();
            tm.SkipForcedAdvancedVersioning = true;
            tm.SkipForcedLateReference = true;

            var schema = tm.GetDebugSchema(typeof(Foo));

            Xunit.Assert.Equal(@"Root : Foo
 -> NetObject : Foo = AsReference, UseConstructor
 -> ModelType : Foo
 -> LinkTo [AqlaSerializer.unittest.Aqla.NestedLevelsTest+Foo]

AqlaSerializer.unittest.Aqla.NestedLevelsTest+Foo:
Type : Foo
{
    #1
     -> Foo.SimpleCollectionInherited
     -> NetObject : Int32[] = UseConstructor, WithNullWireType
     -> Array : Int32[] = NewPacked
     -> WireType : Int32 = Variant
     -> Int32
    ,
    #2
     -> Foo.SimpleCollectionInheritedReference
     -> NetObject : Int32[] = AsReference, UseConstructor, WithNullWireType
     -> Array : Int32[] = NewPacked
     -> WireType : Int32 = Variant
     -> Int32
    ,
    #3
     -> Foo.SimpleCollectionSpecifiedCompact
     -> NetObject : Int32[] = UseConstructor, WithNullWireType
     -> Array : Int32[] = NewPacked
     -> WireType : Int32 = Variant
     -> Int32
    ,
    #4
     -> Foo.NestedCollection
     -> NoNull : NestedCollectionClass[]
     -> Array : NestedCollectionClass[] = NewPacked
     -> NetObject : NestedCollectionClass = AsReference, UseConstructor
     -> List : NestedCollectionClass = NewPacked
     -> NetObject : List`1 = AsReference, UseConstructor
     -> List : List`1 = NewPacked
     -> NoNull : Int32[]
     -> Array : Int32[] = NewPacked
     -> WireType : Int32 = Variant
     -> Int32
    ,
    #5
     -> Foo.SimpleCollectionSpecifiedMinimal
     -> NetObject : Int32[] = UseConstructor, WithNullWireType
     -> Array : Int32[] = NewPacked
     -> NetObject : Int32 = UseConstructor
     -> WireType : Int32 = Variant
     -> Int32
}


", schema, ignoreLineEndingDifferences: true);

        }
    }
}