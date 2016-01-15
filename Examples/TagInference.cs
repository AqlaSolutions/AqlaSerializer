// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AqlaSerializer;
using System.Runtime.Serialization;
using NUnit.Framework;

namespace Examples
{
    [ProtoBuf.ProtoContract(InferTagFromName=true)]
    [DataContract]
    class TagData
    {
        [DataMember(Order = 1)]
        public int Bravo { get; set; }

        [DataMember(Order = 1, Name="Alpha")]
        public int Delta { get; set; }

        [DataMember]
        public int Zulu { get; set; }

        [DataMember(Order = 2)]
        public int Charlie { get; set; }
    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    class TagDataWithoutInfer
    {
        [DataMember(Order = 1)]
        public int Bravo { get; set; }

        [DataMember(Order = 1, Name = "Alpha")]
        public int Delta { get; set; }

        [DataMember]
        public int Zulu { get; set; }

        [DataMember(Order = 2)]
        public int Charlie { get; set; }
    }

    [ProtoBuf.ProtoContract]
    class TagDataExpected
    {
        [ProtoBuf.ProtoMember(3)]
        public int Bravo { get; set; }

        [ProtoBuf.ProtoMember(2)]
        public int Delta { get; set; }

        [ProtoBuf.ProtoMember(1)]
        public int Zulu { get; set; }

        [ProtoBuf.ProtoMember(4)]
        public int Charlie { get; set; }
    }

    [TestFixture]
    public class TagInference
    {
        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void TestTagWithoutInference()
        {
            TagDataWithoutInfer data = new TagDataWithoutInfer();
            Serializer.DeepClone(data);
        }

        [Test]
        public void TestTagWithInferenceRoundtrip()
        {
            TagData data = new TagData
            {
                Bravo = 15,
                Charlie = 17,
                Delta = 4,
                Zulu = 9
            };
            TagData clone = Serializer.DeepClone(data);
            Assert.AreEqual(data.Bravo, clone.Bravo, "Bravo");
            Assert.AreEqual(data.Charlie, clone.Charlie, "Charlie");
            Assert.AreEqual(data.Delta, clone.Delta, "Delta");
            Assert.AreEqual(data.Zulu, clone.Zulu, "Zulu");
        }

        [Test]
        public void TestTagWithInferenceBinary()
        {
            TagData data = new TagData
            {
                Bravo = 15,
                Charlie = 17,
                Delta = 4,
                Zulu = 9
            };
            TagDataExpected clone = Serializer.ChangeType<TagData, TagDataExpected>(data);
            Assert.AreEqual(data.Bravo, clone.Bravo, "Bravo");
            Assert.AreEqual(data.Charlie, clone.Charlie, "Charlie");
            Assert.AreEqual(data.Delta, clone.Delta, "Delta");
            Assert.AreEqual(data.Zulu, clone.Zulu, "Zulu");
        }

        [Test]
        public void RoundTripWithImplicitFields()
        {
            var obj = new WithImplicitFields {X = 123, Y = "abc"};
            var clone = Serializer.DeepClone(obj);
            Assert.AreEqual(123, clone.X);
            Assert.AreEqual("abc", clone.Y);
        }

        [ProtoBuf.ProtoContract(ImplicitFields = ProtoBuf.ImplicitFields.AllPublic)]
        public class WithImplicitFields
        {
            public int X { get; set; }
            public string Y { get; set; }
        }
    }




}
