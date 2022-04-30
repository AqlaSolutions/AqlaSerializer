using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ProtoBuf.Test
{
    public class SpanReadTests
    {
#pragma warning disable CS0618
        [ProtoContract]
        public class HazMaps
        {
            [ProtoMember(1, DataFormat = DataFormat.WellKnown)]
            public Dictionary<int, DateTime> DateTimeMapMarkedPropertyLevel { get; }
                = new Dictionary<int, DateTime>();

            [ProtoMember(2)]
            [ProtoMap(ValueFormat = DataFormat.WellKnown)]
            public Dictionary<int, DateTime> DateTimeMapMarkedViaMap { get; }
                = new Dictionary<int, DateTime>();

            [ProtoMember(3, DataFormat = DataFormat.WellKnown)]
            [ProtoMap(KeyFormat = DataFormat.WellKnown, ValueFormat = DataFormat.WellKnown)]
            public Dictionary<DateTime, DateTime> DateTimeNotValidMapButMarked { get; }
                = new Dictionary<DateTime, DateTime>();

            [ProtoMember(4, DataFormat = DataFormat.WellKnown)]
            public Dictionary<string, TimeSpan> TimeSpanMapMarkedPropertyLevel { get; }
            = new Dictionary<string, TimeSpan>();

            [ProtoMember(5)]
            [ProtoMap(ValueFormat = DataFormat.WellKnown)]
            public Dictionary<string, TimeSpan> TimeSpanMapMarkedViaMap { get; }
                = new Dictionary<string, TimeSpan>();

            [ProtoMember(6, DataFormat = DataFormat.WellKnown)]
            [ProtoMap(KeyFormat = DataFormat.WellKnown, ValueFormat = DataFormat.WellKnown)]
            public Dictionary<TimeSpan, TimeSpan> TimeSpanNotValidMapButMarked { get; }
                = new Dictionary<TimeSpan, TimeSpan>();
        }
#pragma warning restore CS0618

        static readonly byte[] payload = new byte[] { 0x0A, 0x08, 0x08, 0x01, 0x12, 0x04, 0x08, 0xDA, 0x9F, 0x02, 0x12, 0x0A, 0x08, 0x02, 0x12, 0x06, 0x08, 0x80, 0xE7, 0xCB, 0xF6, 0x05, 0x1A, 0x0C, 0x0A, 0x04, 0x08, 0xDA, 0x9F, 0x02, 0x12, 0x04, 0x08, 0xDA, 0x9F, 0x02, 0x22, 0x09, 0x0A, 0x01, 0x61, 0x12, 0x04, 0x08, 0x02, 0x10, 0x01, 0x2A, 0x08, 0x0A, 0x01, 0x62, 0x12, 0x03, 0x08, 0x90, 0x1C, 0x32, 0x0C, 0x0A, 0x04, 0x08, 0x02, 0x10, 0x01, 0x12, 0x04, 0x08, 0x02, 0x10, 0x01 };

        [Test]
        public void ReadStream()
            => CheckVia<Stream>(new MemoryStream(payload));

        [Test]
        public void ReadArray()
            => CheckVia<byte[]>(payload);

        [Test]
        public void ReadMemory()
            => CheckVia<ReadOnlyMemory<byte>>(payload);

        [Test]
        public void ReadArraySegment()
            => CheckVia<ArraySegment<byte>>(new ArraySegment<byte>(payload, 0, payload.Length));

        [Test]
        public void ReadSpan()
        {
            ReadOnlySpan<byte> span = payload;
            var obj = RuntimeTypeModel.Default.Deserialize<HazMaps>(span);
            Check(obj);
        }

        [Test]
        public void ReadSpanNonGeneric()
        {
            ReadOnlySpan<byte> span = payload;
            var obj = Assert.IsType<HazMaps>(RuntimeTypeModel.Default.Deserialize(typeof(HazMaps), span));
            Check(obj);
        }

        private void CheckVia<T>(T value)
        {
            var api = Assert.IsAssignableFrom<IProtoInput<T>>(RuntimeTypeModel.Default);
            var obj = api.Deserialize<HazMaps>(value);
            Check(obj);
        }

        private void Check(HazMaps obj)
        {
/*
{
    DateTimeMapMarkedPropertyLevel = { { 1, date } },
    DateTimeMapMarkedViaMap = { { 2, date } },
    DateTimeNotValidMapButMarked = { { date, date } },
    TimeSpanMapMarkedPropertyLevel = { { "a", time } },
    TimeSpanMapMarkedViaMap = { { "b", time } },
    TimeSpanNotValidMapButMarked = { { time, time } },
};
*/

            var date = new DateTime(2020, 05, 31, 0, 0, 0, DateTimeKind.Utc);
            var time = TimeSpan.FromMinutes(60);

            var a = Assert.Single(obj.DateTimeMapMarkedPropertyLevel);
            Assert.AreEqual(1, a.Key);
            Assert.AreEqual(date, a.Value);

            var b = Assert.Single(obj.DateTimeMapMarkedViaMap);
            Assert.AreEqual(2, b.Key);
            Assert.AreEqual(date, b.Value);

            var c = Assert.Single(obj.DateTimeNotValidMapButMarked);
            Assert.AreEqual(date, c.Key);
            Assert.AreEqual(date, c.Value);

            var d = Assert.Single(obj.TimeSpanMapMarkedPropertyLevel);
            Assert.AreEqual("a", d.Key);
            Assert.AreEqual(time, d.Value);

            var e = Assert.Single(obj.TimeSpanMapMarkedViaMap);
            Assert.AreEqual("b", e.Key);
            Assert.AreEqual(time, e.Value);

            var f = Assert.Single(obj.TimeSpanNotValidMapButMarked);
            Assert.AreEqual(time, f.Key);
            Assert.AreEqual(time, f.Value);
        }


    }
}
