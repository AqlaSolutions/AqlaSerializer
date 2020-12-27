// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer
{
    [TestFixture]
    public class PartialClasses
    {


        [Test]
        public void TestPartial()
        {
            PartialData orig = new PartialData {
                Name = "abcdefghijklmnopqrstuvwxyz",
                Number = 1234,
                When = new DateTime(2008,1,1),
                HowMuchNotSerialized = 123.456M
            },  clone = Serializer.DeepClone(orig);

            Assert.IsNotNull(orig, "original");
            Assert.IsNotNull(clone, "clone");
            Assert.AreEqual(orig.Name, clone.Name, "name");
            Assert.AreEqual(orig.Number, clone.Number, "number");
            Assert.AreEqual(orig.When, clone.When, "when");
            Assert.AreEqual(0.0M, clone.HowMuchNotSerialized, "how much");
        }

        [Test]
        public void TestSubClass()
        {
            var tm = TypeModel.Create();
            //tm.SkipCompiledVsNotCheck = true;
            //tm.AutoCompile = false;
            SubClassData orig = new SubClassData
            {
                Name = "abcdefghijklmnopqrstuvwxyz",
                Number = 1234,
                When = new DateTime(2008, 1, 1),
                HowMuchNotSerialized = 123.456M
            }, clone = (SubClassData)tm.DeepClone<PartialData>(orig);

            Assert.IsNotNull(orig, "original");
            Assert.IsNotNull(clone, "clone");
            Assert.AreEqual(orig.Name, clone.Name, "name");
            Assert.AreEqual(orig.Number, clone.Number, "number");
            Assert.AreEqual(orig.When, clone.When, "when");
            Assert.AreEqual(0.0M, clone.HowMuchNotSerialized, "how much");
        }
    }

    public partial class PartialData
    {
        public int Number { get; set; }
        public string Name { get; set; }
        public DateTime When { get; set; }
        public decimal HowMuchNotSerialized { get; set; }
    }


    [ProtoBuf.ProtoPartialMember(1, "Number"), ProtoBuf.ProtoPartialMember(2, "Name"), ProtoBuf.ProtoPartialMember(3, "When"), ProtoBuf.ProtoContract,
     ProtoBuf.ProtoInclude(4, typeof(SubClassData))]
    public partial class PartialData
    {
    }

    [ProtoBuf.ProtoContract]
    public class SubClassData : PartialData
    {
    }

}
