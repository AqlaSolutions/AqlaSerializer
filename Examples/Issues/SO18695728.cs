// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    [TestFixture]
    public class SO18695728
    {
        [Test]
        public void Execute()
        {
             RuntimeTypeModel.Default[typeof(WebSyncedObject)].AddSubType(10, typeof(GPSReading));
             RuntimeTypeModel.Default[typeof(WebSyncedObject)].AddSubType(11, typeof(TemperatureReading));

             var list = new List<WebSyncedObject>
             {
                 new GPSReading { SpeedKM = 123.45M },
                 new TemperatureReading { Temperature = 67.89M }
             };
             var clone = Serializer.DeepClone(list);

             Assert.AreEqual(2, clone.Count);
             Assert.IsInstanceOfType(typeof(GPSReading), clone[0]);
             Assert.IsInstanceOfType(typeof(TemperatureReading), clone[1]);
        }
        [ProtoBuf.ProtoContract]
        public abstract class WebSyncedObject
        {
            [ProtoBuf.ProtoMember(1)]
            public DateTime SystemTime { get; set; }

            [ProtoBuf.ProtoMember(2)]
            public bool TimeSynchronized { get; set; }

            [ProtoBuf.ProtoMember(3)]
            public ulong RelativeTime { get; set; }

            [ProtoBuf.ProtoMember(4)]
            public Guid BootID { get; set; }

            protected WebSyncedObject()
            {
                BootID = Guid.NewGuid();
                SystemTime = DateTime.Now;
            }
        }

        [ProtoBuf.ProtoContract]
        public class GPSReading : WebSyncedObject
        {
            [ProtoBuf.ProtoMember(1)]
            public DateTime SatelliteTime { get; set; }

            [ProtoBuf.ProtoMember(2)]
            public decimal Latitude { get; set; }

            [ProtoBuf.ProtoMember(3)]
            public decimal Longitude { get; set; }

            [ProtoBuf.ProtoMember(4)]
            public int NumSatellites { get; set; }

            [ProtoBuf.ProtoMember(5)]
            public decimal SpeedKM { get; set; }
        }

        [ProtoBuf.ProtoContract]
        public class TemperatureReading : WebSyncedObject
        {
            [ProtoBuf.ProtoMember(1)]
            public decimal Temperature { get; set; }

            [ProtoBuf.ProtoMember(2)]
            public int NodeID { get; set; }

            [ProtoBuf.ProtoMember(3)]
            public string ProbeIdentifier { get; set; }
        }
    }
}
