// Modified by Vladyslav Taranov for AqlaSerializer, 2016

using System.Diagnostics;
using NUnit.Framework;
using AqlaSerializer;
using System.ComponentModel;
using System.IO;
using AqlaSerializer.Meta;

namespace Examples
{
    [TestFixture]
    public class OptionalData
    {
        [Test]
        public void TestImplicitDefaultZero()
        {
            Test<ImplicitDefaultZero>(0F, 0);
            Test<ImplicitDefaultZero>(3F, 5);
            Test<ImplicitDefaultZero>(5F, 5);
        }
        [Test]
        public void TestExplicitDefaultZero()
        {
            Test<ExplicitDefaultZero>(0F, 0);
            Test<ExplicitDefaultZero>(3F, 5);
            Test<ExplicitDefaultZero>(5F, 5);
        }
        [Test]
        public void TestExplicitDefaultFive()
        {
            Test<ExplicitDefaultFive>(0F, 5);
            Test<ExplicitDefaultFive>(3F, 5);
            Test<ExplicitDefaultFive>(5F, 0);
        }
        [Test]
        public void ExplicitDefaultFivePrivateField()
        {
            Test<ExplicitDefaultFivePrivateField>(0F, 5);
            Test<ExplicitDefaultFivePrivateField>(3F, 5);
            Test<ExplicitDefaultFivePrivateField>(5F, 0);
        }
        [Test]
        public void TestRequiredImplicitZero()
        {
            Test<RequiredImplicitZero>(0F,5);
            Test<RequiredImplicitZero>(3F, 5);
            Test<RequiredImplicitZero>(5F, 5);
        }
        [ExpectedException(typeof(ProtoException), ExpectedMessage = "Can't use default value \"0\" on Required member Single Value")]
        [Test]
        public void TestRequiredExplicitZero()
        {
            Test<RequiredExplicitZero>(0F, 5);
            Test<RequiredExplicitZero>(3F, 5);
            Test<RequiredExplicitZero>(5F, 5);
        }
        [Test]
        [ExpectedException(typeof(ProtoException), ExpectedMessage = "Can't use default value \"5\" on Required member Single Value")]
        public void TestRequiredExplicitFive()
        {
            Test<RequiredExplicitFive>(0F, 5);
            Test<RequiredExplicitFive>(3F, 5);
            Test<RequiredExplicitFive>(5F, 5);
        }


        static void Test<T>(float value, int expectedSize) where T : class, IOptionalData, new()
        {
            var tm = TypeModel.Create(false, ProtoCompatibilitySettings.FullCompatibility);
            T orig = new T { Value = value }, clone = tm.DeepClone(orig);
            Assert.AreEqual(value, orig.Value, "Original");
            Assert.AreNotSame(orig, clone, "Different objects");
            Assert.AreEqual(value, clone.Value, "Clone");

            using(var ms = new MemoryStream()) {
                tm.Serialize(ms, orig);
                Assert.AreEqual(expectedSize, ms.Length, "Length");
            }
        }
    }

    


    interface IOptionalData
    {
        float Value { get; set; }
    }
    [ProtoBuf.ProtoContract]
    public class ImplicitDefaultZero : IOptionalData
    {
        [ProtoBuf.ProtoMember(1)]
        public float Value { get; set; }
    }
    [ProtoBuf.ProtoContract]
    public class ExplicitDefaultZero : IOptionalData
    {
        [ProtoBuf.ProtoMember(1), DefaultValue(0F)]
        public float Value { get; set; }
    }
    [ProtoBuf.ProtoContract]
    public class RequiredImplicitZero : IOptionalData
    {
        [ProtoBuf.ProtoMember(1, IsRequired = true)]
        public float Value { get; set; }
    }
    [ProtoBuf.ProtoContract]
    public class RequiredExplicitZero : IOptionalData
    {
        [ProtoBuf.ProtoMember(1, IsRequired = true), DefaultValue(0F)]
        public float Value { get; set; }
    }
    [ProtoBuf.ProtoContract]
    public class ExplicitDefaultFive : IOptionalData
    {
        public ExplicitDefaultFive() { Value = 5F; }
        [ProtoBuf.ProtoMember(1), DefaultValue(5F)]
        public float Value { get; set; }
    }
    [ProtoBuf.ProtoContract]
    class ExplicitDefaultFivePrivateField : IOptionalData
    {
        public ExplicitDefaultFivePrivateField() { value = 5F; }
        [ProtoBuf.ProtoMember(1), DefaultValue(5F)]
        private float value;

        float IOptionalData.Value
        {
            get { return value; }
            set { this.value = value; }
        }
    }
    
    [ProtoBuf.ProtoContract]
    public class RequiredExplicitFive : IOptionalData
    {
        public RequiredExplicitFive() {Value = 5F; }
        [ProtoBuf.ProtoMember(1, IsRequired = true), DefaultValue(5F)]
        public float Value { get; set; }
    }
}
