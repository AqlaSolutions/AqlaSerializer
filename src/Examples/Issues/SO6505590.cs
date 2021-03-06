﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;
using System.IO;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    [TestFixture]
    public class SO6505590
    {
        public class NoRelationship {}

        [ProtoBuf.ProtoContract]
        public class ParentA { }
        public class ChildA : ParentA { }


        [ProtoBuf.ProtoContract]
        public class ParentB { }
        [ProtoBuf.ProtoContract]
        public class ChildB : ParentB { }


        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(1, typeof(ChildC))]
        public class ParentC { }
        [ProtoBuf.ProtoContract]
        public class ChildC : ParentC { }

        [Test]
        public void SerializeTypeWithNoMarkersShouldFail()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => {
                var obj = new NoRelationship();
                Serializer.Serialize(Stream.Null, obj);
            });
            Assert.That(ex.Message, Is.EqualTo("Type is not expected, and no contract can be inferred: Examples.Issues.SO6505590+NoRelationship"));
        }
        [Test]
        public void DeserializeTypeWithNoMarkersShouldFail()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => {
                Serializer.Deserialize<NoRelationship>(Stream.Null);
            });
            Assert.That(ex.Message, Is.EqualTo("Type is not expected, and no contract can be inferred: Examples.Issues.SO6505590+NoRelationship"));
        }

        [Test]
        public void SerializeParentWithUnmarkedChildShouldWork()
        {
            var obj = new ParentA();
            Serializer.Serialize(Stream.Null, obj);
        }
        
        [Test]
        public void DeserializeParentWithUnmarkedChildShouldWork()
        {
            var tm = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
            Assert.AreEqual(typeof(ParentA), tm.Deserialize<ParentA>(Stream.Null).GetType());
        }

        [Test]
        public void SerializeUnmarkedChildShouldFail()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => {
                var obj = new ChildA();
                Serializer.Serialize(Stream.Null, obj);
            });
            Assert.That(ex.Message, Is.EqualTo("Type is not expected, and no contract can be inferred: Examples.Issues.SO6505590+ChildA"));
        }
        [Test]
        public void DeserializeUnmarkedChildShouldFail()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => {
                Serializer.Deserialize<ChildA>(Stream.Null);
            });
            Assert.That(ex.Message, Is.EqualTo("Type is not expected, and no contract can be inferred: Examples.Issues.SO6505590+ChildA"));
        }


        [Test]
        public void SerializeParentWithUnexpectedChildShouldWork()
        {
            var obj = new ParentB();
            Serializer.Serialize(Stream.Null, obj);
        }
        
        [Test]
        public void DeserializeParentWithUnexpectedChildShouldWork()
        {
            var tm = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
            Assert.AreEqual(typeof(ParentB), tm.Deserialize<ParentB>(Stream.Null).GetType());
        }

        [Test]
        public void SerializeParentWithExpectedChildShouldWork()
        {
            var obj = new ParentC();
            Serializer.Serialize(Stream.Null, obj);
        }
        
        [Test]
        public void DeserializeParentWithExpectedChildShouldWork()
        {
            var tm = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
            Assert.AreEqual(typeof(ParentC), tm.Deserialize<ParentC>(Stream.Null).GetType());
        }

        [Test]
        public void SerializeExpectedChildShouldWork()
        {
            var obj = new ChildC();
            Assert.AreEqual(typeof(ChildC), Serializer.DeepClone<ParentC>(obj).GetType());
        }
    }
}
