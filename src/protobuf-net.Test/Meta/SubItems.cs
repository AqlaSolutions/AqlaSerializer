// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer.Meta;
using System.IO;

namespace AqlaSerializer.unittest.Meta
{
    [TestFixture]
    public class SubItems
    {
        static RuntimeTypeModel CreateModel(bool comp)
        {
            var model = TypeModel.Create(false, comp ? ProtoCompatibilitySettingsValue.FullCompatibility : ProtoCompatibilitySettingsValue.Incompatible);
            model.Add(typeof(OuterRef), false)
                .Add(1, "Int32")
                .Add(2, "String")
                .Add(3, "InnerVal")
                .Add(4, "InnerRef");
            model.Add(typeof(InnerRef), false)
                .Add(1, "Int32")
                .Add(2, "String");
            model.Add(typeof(OuterVal), false)
                .Add(1, "Int32")
                .Add(2, "String")
                .Add(3, "InnerVal")
                .Add(4, "InnerRef");
            model.Add(typeof(InnerVal), false)
                .Add(1, "Int32")
                .Add(2, "String");
            return model;
        }

        [Test]
        public void BuildModel([Values(false, true)] bool comp)
        {
            Assert.IsNotNull(CreateModel(comp));
        }

        [Test]
        public void TestCanDeserialierAllFromEmptyStream()
        {
            var model = CreateModel(true);
            Assert.IsInstanceOf(typeof(OuterRef), model.Deserialize(Stream.Null, null, typeof(OuterRef)));
            Assert.IsInstanceOf(typeof(OuterVal), model.Deserialize(Stream.Null, null, typeof(OuterVal)));
            Assert.IsInstanceOf(typeof(InnerRef), model.Deserialize(Stream.Null, null, typeof(InnerRef)));
            Assert.IsInstanceOf(typeof(InnerVal), model.Deserialize(Stream.Null, null, typeof(InnerVal)));

            model.CompileInPlace();
            Assert.IsInstanceOf(typeof(OuterRef), model.Deserialize(Stream.Null, null, typeof(OuterRef)));
            Assert.IsInstanceOf(typeof(OuterVal), model.Deserialize(Stream.Null, null, typeof(OuterVal)));
            Assert.IsInstanceOf(typeof(InnerRef), model.Deserialize(Stream.Null, null, typeof(InnerRef)));
            Assert.IsInstanceOf(typeof(InnerVal), model.Deserialize(Stream.Null, null, typeof(InnerVal)));

            var compiled = model.Compile("SubItems","SubItems.dll");
            PEVerify.Verify("SubItems.dll");
            Assert.IsInstanceOf(typeof(OuterRef), compiled.Deserialize(Stream.Null, null, typeof(OuterRef)));
            Assert.IsInstanceOf(typeof(OuterVal), compiled.Deserialize(Stream.Null, null, typeof(OuterVal)));
            Assert.IsInstanceOf(typeof(InnerRef), compiled.Deserialize(Stream.Null, null, typeof(InnerRef)));
            Assert.IsInstanceOf(typeof(InnerVal), compiled.Deserialize(Stream.Null, null, typeof(InnerVal)));

        }



        [Test]
        public void TestRoundTripOuterRef([Values(false, true)] bool comp)
        {
            OuterRef outer = new OuterRef
            {
                InnerRef = new InnerRef { Int32 = 123, String = "abc" },
                InnerVal = new InnerVal { Int32 = 456, String = "def" }
            }, clone;
            
            var model = CreateModel(comp);
            clone = (OuterRef)model.DeepClone(outer);
            Assert.AreNotSame(outer, clone);
            Assert.AreEqual(123, clone.InnerRef.Int32);
            Assert.AreEqual("abc", clone.InnerRef.String);
            Assert.AreEqual(456, clone.InnerVal.Int32);
            Assert.AreEqual("def", clone.InnerVal.String);

            model.CompileInPlace();
            clone = (OuterRef)model.DeepClone(outer);
            Assert.AreNotSame(outer, clone);
            Assert.AreEqual(123, clone.InnerRef.Int32);
            Assert.AreEqual("abc", clone.InnerRef.String);
            Assert.AreEqual(456, clone.InnerVal.Int32);
            Assert.AreEqual("def", clone.InnerVal.String);

            clone = (OuterRef)model.Compile().DeepClone(outer);
            Assert.AreNotSame(outer, clone);
            Assert.AreEqual(123, clone.InnerRef.Int32);
            Assert.AreEqual("abc", clone.InnerRef.String);
            Assert.AreEqual(456, clone.InnerVal.Int32);
            Assert.AreEqual("def", clone.InnerVal.String);
        }

        [Test]
        public void TestRoundTripOuterVal([Values(false, true)] bool comp)
        {
            OuterVal outer = new OuterVal
            {
                InnerRef = new InnerRef { Int32 = 123, String = "abc" },
                InnerVal = new InnerVal { Int32 = 456, String = "def" }
            }, clone;

            var model = CreateModel(comp);
            clone = (OuterVal)model.DeepClone(outer);
            Assert.AreNotSame(outer, clone);
            Assert.AreEqual(123, clone.InnerRef.Int32);
            Assert.AreEqual("abc", clone.InnerRef.String);
            Assert.AreEqual(456, clone.InnerVal.Int32);
            Assert.AreEqual("def", clone.InnerVal.String);
            
            model.CompileInPlace();
            clone = (OuterVal)model.DeepClone(outer);
            
            Assert.AreNotSame(outer, clone);
            Assert.AreEqual(123, clone.InnerRef.Int32);
            Assert.AreEqual("abc", clone.InnerRef.String);
            Assert.AreEqual(456, clone.InnerVal.Int32);
            Assert.AreEqual("def", clone.InnerVal.String);
            
            clone = (OuterVal)model.Compile().DeepClone(outer);
            Assert.AreNotSame(outer, clone);
            Assert.AreEqual(123, clone.InnerRef.Int32);
            Assert.AreEqual("abc", clone.InnerRef.String);
            Assert.AreEqual(456, clone.InnerVal.Int32);
            Assert.AreEqual("def", clone.InnerVal.String);
        }

        public class OuterRef
        {
            public int Int32 { get; set; }
            public string String{ get; set; }
            public InnerRef InnerRef { get; set; }
            public InnerVal InnerVal { get; set; }
        }
        public class InnerRef
        {
            public int Int32 { get; set; }
            public string String { get; set; }
        }

        public struct OuterVal
        {
            public int Int32 { get; set; }
            public string String { get; set; }
            public InnerRef InnerRef { get; set; }
            public InnerVal InnerVal { get; set; }
        }
        public struct InnerVal
        {
            public int Int32 { get; set; }
            public string String { get; set; }
        }

        [Test]
        public void TestTypeWithNullableProps()
        {
            var model = TypeModel.Create();
            TypeWithNulls obj = new TypeWithNulls { First = 123, Second = 456.789M };
            
            var clone1 = (TypeWithNulls)model.DeepClone(obj);
            
            model.CompileInPlace();
            var clone2 = (TypeWithNulls)model.DeepClone(obj);

            
            TypeModel compiled = model.Compile("TestTypeWithNullableProps", "TestTypeWithNullableProps.dll");
            PEVerify.Verify("TestTypeWithNullableProps.dll");
            var clone3 = (TypeWithNulls)compiled.DeepClone(obj);
            Assert.AreEqual(123, clone1.First);
            Assert.AreEqual(456.789, clone1.Second);

            Assert.AreEqual(123, clone2.First);
            Assert.AreEqual(456.789, clone2.Second);

            Assert.AreEqual(123, clone3.First);
            Assert.AreEqual(456.789, clone3.Second);

        }

        [Test]
        public void TestTypeWithNullablePropsComplex()
        {
            var model = TypeModel.Create();
            var obj = new TypeWithNullsComplex() { First = new Foo(123) };
            
            var clone1 = model.DeepClone(obj);
            
            model.CompileInPlace();
            var clone2 = model.DeepClone(obj);
            
            TypeModel compiled = model.Compile("TestTypeWithNullablePropsComplex", "TestTypeWithNullablePropsComplex.dll");
            PEVerify.Verify("TestTypeWithNullablePropsComplex.dll");
            var clone3 = compiled.DeepClone(obj);

            Assert.AreEqual(123, clone1.First.Value.Value);
            Assert.AreEqual(null, clone1.Second);
            
            Assert.AreEqual(123, clone2.First.Value.Value);
            Assert.AreEqual(null, clone2.Second);

            Assert.AreEqual(123, clone3.First.Value.Value);
            Assert.AreEqual(null, clone3.Second);

        }

        [ProtoBuf.ProtoContract]
        public class TypeWithNulls
        {
            [ProtoBuf.ProtoMember(1)]
            public int? First { get; set; }

            [ProtoBuf.ProtoMember(2)]
            public decimal? Second { get; set; }
        }

        [ProtoBuf.ProtoContract]
        public class TypeWithNullsComplex
        {
            [ProtoBuf.ProtoMember(1)]
            public Foo? First { get; set; }
            
            [ProtoBuf.ProtoMember(2)]
            public Foo? Second { get; set; }
            
        }

        [ProtoBuf.ProtoContract]
        public struct Foo
        {
            [ProtoBuf.ProtoMember(1)]
            public int Value { get; set; }

            public Foo(int value)
            {
                Value = value;
            }
        }
    }
}
