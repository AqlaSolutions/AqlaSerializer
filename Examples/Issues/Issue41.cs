﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.IO;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Issue41
{
    [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(3, typeof(B))]
    public class A
    {
        [ProtoBuf.ProtoMember(1, Name = "PropA")]
        public string PropA { get; set; }

        [ProtoBuf.ProtoMember(2, Name = "PropB")]
        public string PropB { get; set; }
    }

    [ProtoBuf.ProtoContract]
    public class B : A
    {
        [ProtoBuf.ProtoMember(1, Name = "PropAB")]
        public string PropAB { get; set; }
        [ProtoBuf.ProtoMember(2, Name = "PropBB")]
        public string PropBB { get; set; }
    }

    [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(2, typeof(B_Orig))]
    public class A_Orig
    {
        [ProtoBuf.ProtoMember(1, Name = "PropA")]
        public string PropA { get; set; }

        [ProtoBuf.ProtoMember(2, Name = "PropB")]
        public string PropB { get; set; }
    }

    [ProtoBuf.ProtoContract]
    public class B_Orig : A_Orig
    {
        [ProtoBuf.ProtoMember(1, Name = "PropAB")]
        public string PropAB { get; set; }
        [ProtoBuf.ProtoMember(2, Name = "PropBB")]
        public string PropBB { get; set; }
    }
    [TestFixture]
    public class Issue41Rig
    {
        [Test]
        public void Issue41TestOriginalSubClassShouldComplainAboutDuplicateTags()
        {
            var tm = TypeModel.Create();
            Assert.That(() => tm.Serialize(Stream.Null, new B_Orig()), Throws.TypeOf<ArgumentException>().With.Message.Contains("Prop"));
        }

        [Test]
        public void Issue41TestOriginalBaseClassShouldComplainAboutDuplicateTags()
        {
            var tm = TypeModel.Create();
            Assert.That(() => tm.Serialize(Stream.Null, new A_Orig()), Throws.TypeOf<ArgumentException>().With.Message.Contains("Prop"));
        }

        [Test]
        public void Issue41InheritedPropertiesAsBaseClass()
        {
            B b = new B {PropA = "a", PropB = "b", PropAB = "ab", PropBB = "bb"};
            using (var s = new MemoryStream ())
            {
              Serializer.Serialize<A>(s, b);
              s.Position = 0;
              B bb = (B)Serializer.Deserialize<A>(s);
              Assert.AreEqual(b.PropA, bb.PropA, "PropA");
              Assert.AreEqual(b.PropB, bb.PropB, "PropB");
              Assert.AreEqual(b.PropAB, bb.PropAB, "PropAB");
              Assert.AreEqual(b.PropBB, bb.PropBB, "PropBB");
            }
        }
        [Test]
        public void Issue41InheritedPropertiesAsSubClass()
        {
            B b = new B { PropA = "a", PropB = "b", PropAB = "ab", PropBB = "bb" };
            using (var s = new MemoryStream())
            {
                Serializer.Serialize<B>(s, b);
                s.Position = 0;
                B bb = Serializer.Deserialize<B>(s);
                Assert.AreEqual(b.PropA, bb.PropA, "PropA");
                Assert.AreEqual(b.PropB, bb.PropB, "PropB");
                Assert.AreEqual(b.PropAB, bb.PropAB, "PropAB");
                Assert.AreEqual(b.PropBB, bb.PropBB, "PropBB");
            }
        }
    }
}