// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using Examples.SimpleStream;
using NUnit.Framework;
using AqlaSerializer;
using System.ComponentModel;
using AqlaSerializer.Meta;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System;

namespace Examples
{
    [TestFixture]
    public class ProtoGeneration
    {
        [Test]
        public void GetProtoTest1()
        {
            var model = TypeModel.Create();
            model.UseImplicitZeroDefaults = false;

            string proto = model.GetSchema(typeof(Test1));

            Xunit.Assert.Equal(
@"package Examples.SimpleStream;

message Test1 {
   required int32 a = 1;
}
", proto, ignoreLineEndingDifferences: true);
        }

        [Test]
        public void GetProtoTest2()
        {
            var model = TypeModel.Create();
            model.UseImplicitZeroDefaults = false;

            string proto = model.GetSchema(typeof(Test2));

            Xunit.Assert.Equal(
@"package Examples;

message abc {
   required uint32 ghi = 2;
   required bytes def = 3;
}
", proto, ignoreLineEndingDifferences: true);
        }

        [DataContract(Name="abc")]
        public class Test2
        {
            [DataMember(Name = "def", IsRequired = true, Order = 3)]
            public byte[] X { get; set; }

            [DataMember(Name = "ghi", IsRequired = true, Order = 2)]
            public char Y { get; set; }
        }

        [Test]
        public void TestProtoGenerationWithDefaultString()
        {

            string proto = Serializer.GetProto<MyClass>();

            Xunit.Assert.Equal(@"
message MyClass {
   optional string TestString = 1 [default = ""Test Test TEst""];
}
", proto, ignoreLineEndingDifferences: true);
        }

        [Test]
        public void GenericsWithoutExplicitNamesShouldUseTheTypeName()
        {
            string proto = Serializer.GetProto<ProtoGenerationTypes.BrokenProto.ExampleContract>();
        }

        [Test]
        public void SelfReferntialGenericsShouldNotExplode()
        {
            string proto = Serializer.GetProto<ProtoGenerationTypes.SelfGenericProto.EvilParent>();

            Xunit.Assert.Equal(ignoreLineEndingDifferences: true, expected: @"package ProtoGenerationTypes.SelfGenericProto;

message EvilGeneric_EvilParent {
   optional int32 X = 1 [default = 0];
}
message EvilParent {
   optional EvilGeneric_EvilParent X = 1;
}
", actual: proto);
        }

        [Test]
        public void ProtoForContractListsShouldGenerateSchema()
        {
            string proto = GetSurrogateModel().GetSchema(typeof(List<MySurrogate>));
            Xunit.Assert.Equal(ignoreLineEndingDifferences: true, expected: @"package Examples;

message List_MySurrogate {
   repeated MySurrogate items = 1;
}
message MySurrogate {
}
", actual: proto);
        }

        [Test]
        public void ProtoForContractViaSurrogateListsShouldGenerateSchema()
        {
            string proto = GetSurrogateModel().GetSchema(typeof(List<MyNonSurrogate>));
            Xunit.Assert.Equal(ignoreLineEndingDifferences: true, expected: @"package Examples;

message List_MyNonSurrogate {
   repeated MySurrogate items = 1;
}
message MySurrogate {
}
", actual: proto);
        }

        [Test]
        public void ProtoForPrimitiveListsShouldGenerateSchema()
        {
            string proto = Serializer.GetProto<List<int>>();
            Xunit.Assert.Equal(@"
message List_Int32 {
   repeated int32 items = 1;
}
", actual: proto, ignoreLineEndingDifferences: true);
        }

        [Test]
        public void ProtoForPrimitiveShouldGenerateSchema()
        {
            string proto = Serializer.GetProto<int>();
            Xunit.Assert.Equal(@"
message Int32 {
   optional int32 value = 1;
}
", actual: proto, ignoreLineEndingDifferences: true);
        }
        [Test]
        public void ProtoForNullablePrimitiveShouldGenerateSchema()
        {
            string proto = Serializer.GetProto<int?>();
            Xunit.Assert.Equal(@"
message Int32 {
   optional int32 value = 1;
}
", actual: proto, ignoreLineEndingDifferences: true);
        }
        [Test]
        public void ProtoForDictionaryShouldGenerateSchema()
        {
            string proto = Serializer.GetProto<Dictionary<string,int>>();
            Xunit.Assert.Equal(@"
message Dictionary_String_Int32 {
   repeated KeyValuePair_String_Int32 items = 1;
}
message KeyValuePair_String_Int32 {
   optional string Key = 1;
   optional int32 Value = 2;
}
", actual: proto, ignoreLineEndingDifferences: true);
        }
        [Test]
        public void ProtoForDictionaryShouldIncludeSchemasForContainedTypes()
        {
            string proto = Serializer.GetProto<Dictionary<string, MySurrogate>>();
            Xunit.Assert.Equal(ignoreLineEndingDifferences: true, expected: @"package Examples;

message Dictionary_String_MySurrogate {
   repeated KeyValuePair_String_MySurrogate items = 1;
}
message KeyValuePair_String_MySurrogate {
   optional string Key = 1;
   optional MySurrogate Value = 2;
}
message MySurrogate {
}
", actual: proto);
        }

        [Test]
        public void InheritanceShouldCiteBaseType()
        {
            string proto = Serializer.GetProto<Dictionary<string, Cat>>();
            Xunit.Assert.Equal(ignoreLineEndingDifferences: true, expected: @"package Examples;

message Animal {
   // the following represent sub-types; at most 1 should have a value
   optional Cat Cat = 1;
}
message Cat {
}
message Dictionary_String_Cat {
   repeated KeyValuePair_String_Cat items = 1;
}
message KeyValuePair_String_Cat {
   optional string Key = 1;
   optional Animal Value = 2;
}
", actual: proto);
        }

        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(1, typeof(Cat))] public class Animal {}
        [ProtoBuf.ProtoContract] public class Cat : Animal {}

        [Ignore("Parameter name - localization")]
        [Test]
        public void ProtoForNonContractTypeShouldThrowException()
        {
            var ex = Assert.Throws<ArgumentException>(() => {
                var model = TypeModel.Create();
                model.AutoAddMissingTypes = false;
                model.GetSchema(typeof(ProtoGenerationTypes.BrokenProto.Type2));
            });
            Assert.That(ex.Message, Is.EqualTo(@"The type specified is not a contract-type
Parameter name: type"));
        }

        [Test]
        public void BclImportsAreAddedWhenNecessary()
        {
            string proto = Serializer.GetProto<ProtoGenerationTypes.BclImports.HasPrimitives>();

            Xunit.Assert.Equal(ignoreLineEndingDifferences: true, expected: @"package ProtoGenerationTypes.BclImports;
import ""bcl.proto""; // schema for protobuf-net's handling of core .NET types

message HasPrimitives {
   optional bcl.DateTime When = 1;
}
", actual: proto);
        }

        static TypeModel GetSurrogateModel() {

            var model = TypeModel.Create();
            model.AutoAddMissingTypes = false;
            model.Add(typeof(MySurrogate), true).AsReferenceDefault = false;
            MetaType t = model.Add(typeof(MyNonSurrogate), false);
            t.SetSurrogate(typeof(MySurrogate));
            t.AsReferenceDefault = false;
            model.Add(typeof(UsesSurrogates), true);
            model.Add(typeof(List<MySurrogate>), true);
            model.Add(typeof(List<MyNonSurrogate>), true);
            return model;
        }
        [Test]
        public void SchemaNameForSurrogateShouldBeSane()
        {

            string proto = GetSurrogateModel().GetSchema(typeof(MySurrogate));

            Xunit.Assert.Equal(ignoreLineEndingDifferences: true, expected: @"package Examples;

message MySurrogate {
}
", actual: proto);
        }
        [Test]
        public void SchemaNameForNonSurrogateShouldBeSane()
        {
            string proto = GetSurrogateModel().GetSchema(typeof(MyNonSurrogate));

            Xunit.Assert.Equal(ignoreLineEndingDifferences: true, expected: @"package Examples;

message MySurrogate {
}
", actual: proto);
        }
        [Test]
        public void SchemaNameForTypeUsingSurrogatesShouldBeSane()
        {
            string proto = GetSurrogateModel().GetSchema(typeof(UsesSurrogates));

            Xunit.Assert.Equal(ignoreLineEndingDifferences: true, expected: @"package Examples;

message MySurrogate {
}
message UsesSurrogates {
   optional MySurrogate A = 1;
   optional MySurrogate B = 2;
}
", actual: proto);
        }
        [Test]
        public void EntireSchemaShouldNotIncludeNonSurrogates()
        {
            string proto = GetSurrogateModel().GetSchema(null);

            Xunit.Assert.Equal(ignoreLineEndingDifferences: true, expected: @"package Examples;

message MySurrogate {
}
message UsesSurrogates {
   optional MySurrogate A = 1;
   optional MySurrogate B = 2;
}
", actual: proto);
        }


        [ProtoBuf.ProtoContract]
        public class UsesSurrogates
        {
            [ProtoBuf.ProtoMember(1)]
            public MySurrogate A { get; set; }

            [ProtoBuf.ProtoMember(2)]
            public MyNonSurrogate B { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class MySurrogate
        {
            public static implicit operator MyNonSurrogate(MySurrogate value)
            {
                return value == null ? null : new MyNonSurrogate();
            }
            public static implicit operator MySurrogate(MyNonSurrogate value)
            {
                return value == null ? null : new MySurrogate();
            }
        }
        public class MyNonSurrogate { }
    }

    [TestFixture]
    public class InheritanceGeneration
    {
        [ProtoBuf.ProtoContract]
        [ProtoBuf.ProtoInclude(15, typeof(B))]
        public class A
        {
            [ProtoBuf.ProtoMember(1)]
            public int DataA { get; set; }
        }
        [ProtoBuf.ProtoContract]
        [ProtoBuf.ProtoInclude(16, typeof(C))]
        public class B : A
        {
            [ProtoBuf.ProtoMember(2)]
            public int DataB { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class C : B
        {
            [ProtoBuf.ProtoMember(3)]
            public int DataC { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class TestCase
        {
            [ProtoBuf.ProtoMember(10)]
            public C Data;
        }

        [Test]
        public void InheritanceShouldListBaseType()
        {
            // all done on separate models in case of order dependencies, etc
            var model = TypeModel.Create();
            Assert.IsNull(model[typeof(A)].BaseType);

            model = TypeModel.Create();
            Assert.IsNull(model[typeof(TestCase)].BaseType);

            model = TypeModel.Create();
            Assert.AreEqual(typeof(A), model[typeof(B)].BaseType.Type);

            model = TypeModel.Create();
            Assert.AreEqual(typeof(B), model[typeof(C)].BaseType.Type);

            model = TypeModel.Create();
            string s = model.GetSchema(typeof(TestCase));
            Xunit.Assert.Equal(ignoreLineEndingDifferences: true, expected: @"package Examples;

message A {
   optional int32 DataA = 1 [default = 0];
   // the following represent sub-types; at most 1 should have a value
   optional B B = 15;
}
message B {
   optional int32 DataB = 2 [default = 0];
   // the following represent sub-types; at most 1 should have a value
   optional C C = 16;
}
message C {
   optional int32 DataC = 3 [default = 0];
}
message TestCase {
   optional A Data = 10;
}
", actual: s);
        }
    }
}

[ProtoBuf.ProtoContract]
public class MyClass
{
    [ProtoBuf.ProtoMember(1), DefaultValue("Test Test TEst")]
    public string TestString { get; set; }
}
namespace ProtoGenerationTypes.BclImports
{
    [ProtoBuf.ProtoContract]
    public class HasPrimitives
    {
        [ProtoBuf.ProtoMember(1)]
        public DateTime When { get; set; }
    }
}
namespace ProtoGenerationTypes.SelfGenericProto
{
    [ProtoBuf.ProtoContract]
    public class EvilParent
    {
        [ProtoBuf.ProtoMember(1)]
        public EvilGeneric<EvilParent> X { get; set; }
    }
    [ProtoBuf.ProtoContract]
    public class EvilGeneric<T>
    {
        [ProtoBuf.ProtoMember(1)]
        public int X { get; set; }
    }
}

namespace ProtoGenerationTypes.BrokenProto
{
	[ProtoBuf.ProtoContract]
	public class ExampleContract
	{
		[ProtoBuf.ProtoMember(1)]
		public List<Info> ListOfInfo { get; set; }
	}

	[ProtoBuf.ProtoContract]
	[ProtoBuf.ProtoInclude(2, typeof(Info<Type1>))]
	[ProtoBuf.ProtoInclude(3, typeof(Info<Type2>))]
	public abstract class Info
	{
		[ProtoBuf.ProtoMember(1)]
		public string Name { get; set; }
	}

	[ProtoBuf.ProtoContract]
	public class Info<T> : Info
		where T : DetailsBase, new()
	{
		public Info()
		{
			Details = new T();
		}

		[ProtoBuf.ProtoMember(2)]
		public T Details { get; set; }
	}

	public abstract class DetailsBase
	{
	}

	[ProtoBuf.ProtoContract]
	public class Type1 : DetailsBase
	{
		[ProtoBuf.ProtoMember(1)]
		public string Value1 { get; set; }

		[ProtoBuf.ProtoMember(2)]
		public string Value2 { get; set; }
	}

	[ProtoBuf.ProtoContract]
	public class Type2 : DetailsBase
	{
		[ProtoBuf.ProtoMember(1)]
		public string Value3 { get; set; }

		[ProtoBuf.ProtoMember(2)]
		public string Value4 { get; set; }
	}
}