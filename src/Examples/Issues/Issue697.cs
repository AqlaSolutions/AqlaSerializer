using ProtoBuf.Meta;
using System;
using System.IO;
using NUnit.Framework;

namespace ProtoBuf.Issues
{
    public class Issue697
    {
        [Test]
        public void SkipFailsWhenNoStream()
        {
            var bytes = new byte[] {
                18, 209, 180, 9, 164, 51, 235, 15, 208, 245, 70, 233, 227, 170, 79, 135, 203, 158, 107, 30, 244, 111, 35, 0, 60, 73, 117, 227, 122, 147, 19, 38
            };
            using var ms = new MemoryStream(bytes);
            using var reader = new ProtoReader(ms, RuntimeTypeModel.Default);

            Assert.True(reader.ReadFieldHeader() > 0);
            try
            {   // this was throwing NullReferenceException
                reader.SkipField();
                Assert.True(false); // force fail, this should not have worked
            }
            catch (EndOfStreamException) { } // success (note: can't use Assert.Throws here because of ref-struct)
        }

        [Test]
        public void DetectInvalidEndGroupAcceptably()
        {
            var bytes = new byte[] {
                18, 209, 180, 9, 164, 51, 235, 15, 208, 245, 70, 233, 227, 170, 79, 135, 203, 158, 107, 30, 244, 111, 35, 0, 60, 73, 117, 227, 122, 147, 19, 38
            };
            using var ms = new MemoryStream(bytes);
            using var reader = new ProtoReader(ms, RuntimeTypeModel.Default);

            Assert.AreEqual(2, reader.ReadFieldHeader());
            Assert.AreEqual(WireType.String, reader.WireType);

            var tok = reader.StartSubItem();
            Assert.AreEqual(0, reader.ReadFieldHeader());
            Assert.AreEqual(WireType.EndGroup, reader.WireType);
            Assert.AreEqual(820, reader.FieldNumber);
        }
        [ProtoContract]
        public class TestContract
        {
            [ProtoMember(1)] public int Id { get; set; }
            [ProtoMember(2)] public CustomClass Value { get; set; }
        }

        [ProtoContract]
        public class CustomClass
        {
            [ProtoMember(1)] public int Id { get; set; }
            [ProtoMember(2)] public string Value { get; set; }
        }
        [Test]
        public void DeserializeShouldFailReasonable()
        {
            var bytes = new byte[] {
                18, 209, 180, 9, 164, 51, 235, 15, 208, 245, 70, 233, 227, 170, 79, 135, 203, 158, 107, 30, 244, 111, 35, 0, 60, 73, 117, 227, 122, 147, 19, 38
            };
            using var ms = new MemoryStream(bytes);
            var ex = Assert.Throws<ProtoException>(() => Serializer.Deserialize<TestContract>(ms));
            Assert.AreEqual("A length-based message was terminated via end-group; this indicates data corruption", ex.Message);
            Assert.AreEqual("tag=820; wire-type=EndGroup; offset=6; depth=1", ex.Data["protoSource"]);
        }
    }
}
