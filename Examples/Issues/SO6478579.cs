// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace test
{
    [DataContract]
    public class Coordinates
    {
        [DataContract]
        public struct CoOrd
        {
            public CoOrd(int x, int y, int z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }
            [ProtoBuf.ProtoMember(1)]
            int x;
            [ProtoBuf.ProtoMember(2)]
            int y;
            [ProtoBuf.ProtoMember(3)]
            int z;
        }
        [DataMember(Order = 1)]
        public List<CoOrd> Coords = new List<CoOrd>();

        public void SetupTestArray(int max)
        {
            Random r = new Random(123456);
            for (int i = 0; i < max; i++)
            {
                Coords.Add(new CoOrd(r.Next(10000), r.Next(10000), r.Next(10000)));
            }
        }
    }

    [TestFixture]
    public class SO6478579
    {
#if DEBUG
        public const int Size = 1092;
        public const int Max = 100;
#else
        public const int Size = 10960823;
        public const int Max = 1000000;
#endif

        [Test]
        public void TestMethod()
        {
            Coordinates c = new Coordinates();
            c.SetupTestArray(Max);

            // Serialize to memory stream
            MemoryStream mStream = new MemoryStream();
            var tm = TypeModel.Create(false, ProtoCompatibilitySettings.FullCompatibility);
            tm.Serialize(mStream, c);

            Assert.AreEqual(Size, mStream.Length); 
        }
    }
}

