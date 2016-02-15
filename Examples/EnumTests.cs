// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using AqlaSerializer;
using System.Runtime.Serialization;
using AqlaSerializer.Meta;

namespace Examples.DesignIdeas
{
    /// <summary>
    /// would like to be able to specify custom values for enums;
    /// implementation note: some kind of map: Dictionary&lt;TValue, long&gt;?
    /// note: how to handle -ves? (ArgumentOutOfRangeException?)
    /// note: how to handle flags? (NotSupportedException? at least for now?
    ///             could later use a bitmap sweep?)
    /// </summary>
    [ProtoBuf.ProtoContract(Name="blah")]
    public enum SomeEnum
    {
        [ProtoBuf.ProtoEnum(Name="FOO")]
        ChangeName = 3,

        [ProtoBuf.ProtoEnum(Value = 19)]
        ChangeValue = 5,

        [ProtoBuf.ProtoEnum(Name="BAR", Value=92)]
        ChangeBoth = 7,
        
        LeaveAlone = 22,


        Default = 2
    }
    [ProtoBuf.ProtoContract]
    public class EnumFoo
    {
        public EnumFoo() { Bar = SomeEnum.Default; }
        [ProtoBuf.ProtoMember(1), DefaultValue(SomeEnum.Default)]
        public SomeEnum Bar { get; set; }
    }

    [ProtoBuf.ProtoContract]
    public class EnumNullableFoo
    {
        public EnumNullableFoo() { Bar = SomeEnum.Default; }
        [ProtoBuf.ProtoMember(1), DefaultValue(SomeEnum.Default)]
        public SomeEnum? Bar { get; set; }
    }

    public enum NegEnum
    {
        A = -1, B = 0, C = 1
    }
    [ProtoBuf.ProtoContract]
    public class NegEnumType
    {
        [ProtoBuf.ProtoMember(1)]
        public NegEnum Value { get; set; }
    }
    public enum HasConflictingKeys
    {
        [ProtoBuf.ProtoEnum(Value = 1)]
        Foo = 0,
        [ProtoBuf.ProtoEnum(Value = 2)]
        Bar = 0
    }
    public enum HasConflictingValues
    {
        [ProtoBuf.ProtoEnum(Value=2)]
        Foo = 0,
        [ProtoBuf.ProtoEnum(Value = 2)]
        Bar = 1
    }
    [ProtoBuf.ProtoContract]
    public class TypeDuffKeys
    {
        [ProtoBuf.ProtoMember(1)]
        public HasConflictingKeys Value {get;set;}
    }
    [ProtoBuf.ProtoContract]
    public class TypeDuffValues
    {
        [ProtoBuf.ProtoMember(1)]
        public HasConflictingValues Value {get;set;}
    }

    [ProtoBuf.ProtoContract]
    public class NonNullValues
    {
        [ProtoBuf.ProtoMember(1), DefaultValue(SomeEnum.Default)]
        SomeEnum Foo { get; set; }
        [ProtoBuf.ProtoMember(2)]
        bool Bar { get; set; }
    }
    [ProtoBuf.ProtoContract]
    public class NullValues
    {
        [ProtoBuf.ProtoMember(1), DefaultValue(SomeEnum.Default)]
        SomeEnum? Foo { get; set; }
        [ProtoBuf.ProtoMember(2)]
        bool? Bar { get; set; }
    }

    [TestFixture]
    public class EnumTests
    {

        [Test]
        public void EnumGeneration()
        {

            string proto = Serializer.GetProto<EnumFoo>();

            Assert.AreEqual(@"package Examples.DesignIdeas;

message EnumFoo {
   optional blah Bar = 1 [default = Default];
}
enum blah {
   Default = 2;
   FOO = 3;
   ChangeValue = 19;
   LeaveAlone = 22;
   BAR = 92;
}
", proto);
        }


        [Test]
        public void TestNonNullValues()
        {
            var model = TypeModel.Create();
            model.UseImplicitZeroDefaults = false;

            string proto = model.GetSchema(typeof (NonNullValues));

            Assert.AreEqual(@"package Examples.DesignIdeas;

message NonNullValues {
   optional blah Foo = 1 [default = Default];
   optional bool Bar = 2;
}
enum blah {
   Default = 2;
   FOO = 3;
   ChangeValue = 19;
   LeaveAlone = 22;
   BAR = 92;
}
", proto);
        }

        [Test]
        public void TestNullValues()
        {
            RuntimeTypeModel tm = TypeModel.Create();
            tm.SkipCompiledVsNotCheck = true;
            string proto = tm.GetSchema(typeof(NullValues));

            Assert.AreEqual(@"package Examples.DesignIdeas;

message NullValues {
   optional blah Foo = 1 [default = Default];
   optional bool Bar = 2;
}
enum blah {
   Default = 2;
   FOO = 3;
   ChangeValue = 19;
   LeaveAlone = 22;
   BAR = 92;
}
", proto);
        }

        [Test, ExpectedException(typeof(ProtoException))]
        public void TestConflictingKeys()
        {
            TypeModel.Create().Serialize(Stream.Null, new TypeDuffKeys { Value = HasConflictingKeys.Foo });
        }

        [Test, ExpectedException(typeof(ProtoException))]
        public void TestConflictingValues()
        {
            TypeModel.Create().Serialize(Stream.Null, new TypeDuffValues { Value = HasConflictingValues.Foo });
        }

        [Test]
        public void TestEnumNameValueMapped()
        {
            CheckValue(SomeEnum.ChangeBoth, 0x08, 92);
        }


        [Test]
        public void TestFlagsEnum()
        {
            var orig = new TypeWithFlags { Foo = TypeWithFlags.FlagsEnum.A | TypeWithFlags.FlagsEnum.B };
            var clone = Serializer.DeepClone(orig);
            Assert.AreEqual(orig.Foo, clone.Foo);
        }

        [ProtoBuf.ProtoContract]
        public class TypeWithFlags
        {
            [Flags]
            public enum FlagsEnum
            {
                None = 0, A = 1, B = 2, C = 4
            }
            [ProtoBuf.ProtoMember(1)]
            public FlagsEnum Foo { get; set; }
        }

        [Test]
        public void TestNulalbleEnumNameValueMapped()
        {
            var orig = new EnumNullableFoo { Bar = SomeEnum.ChangeBoth };
            var clone = Serializer.DeepClone(orig);
            Assert.AreEqual(orig.Bar, clone.Bar);
        }
        [Test]
        public void TestEnumNameMapped() {
            CheckValue(SomeEnum.ChangeName, 0x08, 03);
        }
        [Test]
        public void TestEnumValueMapped() {
            CheckValue(SomeEnum.ChangeValue, 0x08, 19);
        }
        [Test]
        public void TestEnumNoMap() {
            CheckValue(SomeEnum.LeaveAlone, 0x08, 22);
        }

        static void CheckValue(SomeEnum val, params byte[] expected)
        {
            EnumFoo foo = new EnumFoo { Bar = val };
            using (MemoryStream ms = new MemoryStream())
            {
                var tm = TypeModel.Create(false, ProtoCompatibilitySettings.FullCompatibility);
                tm.Serialize(ms, foo);
                ms.Position = 0;
                byte[] buffer = ms.ToArray();
                Assert.IsTrue(Program.ArraysEqual(buffer, expected), "Byte mismatch");

                EnumFoo clone = tm.Deserialize<EnumFoo>(ms);
                Assert.AreEqual(val, clone.Bar);
            }
        }

        [Test]
        public void TestNegEnum()
        {
            TestNegEnum(NegEnum.A);
            TestNegEnum(NegEnum.B);
            TestNegEnum(NegEnum.C);
        }
        [Test, ExpectedException(typeof(ProtoException))]
        public void TestNegEnumnotDefinedNeg()
        {
            TestNegEnum((NegEnum)(-2));
        }
        [Test, ExpectedException(typeof(ProtoException))]
        public void TestNegEnumnotDefinedPos()
        {
            TestNegEnum((NegEnum) 2);
        }
        [Test]
        public void ShouldBeAbleToSerializeExactDuplicatedEnumValues()
        {
            var obj = new HasDuplicatedEnumProp { Value = NastDuplicates.B };
            var clone = Serializer.DeepClone(obj);
            Assert.AreEqual(NastDuplicates.A, clone.Value);
            Assert.AreEqual(NastDuplicates.B, clone.Value);
        }
        [ProtoBuf.ProtoContract]
        public class HasDuplicatedEnumProp
        {
            [ProtoBuf.ProtoMember(1)]
            public NastDuplicates Value { get; set; }
        }
        public enum NastDuplicates
        {
            None = 0,
            A = 1,
            B = 1
        }

        private static void TestNegEnum(NegEnum value)
        {
            NegEnumType obj = new NegEnumType { Value = value },
                clone = TypeModel.Create().DeepClone(obj);
            Assert.AreEqual(obj.Value, clone.Value, value.ToString());
        }


        [ProtoBuf.ProtoContract]
        enum EnumMarkedContract : ushort
        {
            None = 0, A, B, C, D
        }
        enum EnumNoContract : ushort
        {
            None = 0, A, B, C, D
        }

        [Test]
        public void RoundTripTopLevelContract()
        {
            EnumMarkedContract value = EnumMarkedContract.C;
            Assert.IsTrue(Program.CheckBytes(value, 8, 3));
            Assert.AreEqual(value, TypeModel.Create(false, ProtoCompatibilitySettings.FullCompatibility).DeepClone(value));
        }

        [Test]
        public void RoundTripTopLevelNullableContract()
        {
            EnumMarkedContract? value = EnumMarkedContract.C;
            Assert.IsTrue(Program.CheckBytes(value, 8, 3));
            Assert.AreEqual(value, TypeModel.Create(false, ProtoCompatibilitySettings.FullCompatibility).DeepClone(value));
        }
        [Test]
        public void RoundTripTopLevelNullableContractNull()
        {
            EnumMarkedContract? value = null;
            Assert.AreEqual(value, Serializer.DeepClone(value));
        }
        [Test]
        public void RoundTripTopLevelNoContract()
        {
            EnumNoContract value = EnumNoContract.C;
            Assert.IsTrue(Program.CheckBytes(value, 8, 3));
            Assert.AreEqual(value, TypeModel.Create(false, ProtoCompatibilitySettings.FullCompatibility).DeepClone(value));
        }

        [Test]
        public void RoundTripTopLevelNullableNoContract()
        {
            EnumNoContract? value = EnumNoContract.C;
            Assert.IsTrue(Program.CheckBytes(value, 8, 3));
            Assert.AreEqual(value, TypeModel.Create(false, ProtoCompatibilitySettings.FullCompatibility).DeepClone(value));
        }
        [Test]
        public void RoundTripTopLevelNullableNoContractNull()
        {
            EnumNoContract? value = null;
            Assert.AreEqual(value, Serializer.DeepClone(value));
        }

    }
}
