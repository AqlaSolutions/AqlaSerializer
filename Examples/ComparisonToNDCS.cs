// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NETCOREAPP
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples
{
    [TestFixture]
    public class ComparisonToNDCS
    {
        static List<BasicDto> GetTestData()
        {
            // just make up some gibberish
            var rand = new Random(12345);
            List<BasicDto> list = new List<BasicDto>(30000);
#if DEBUG
            const int max = 100;
#else
            const int max = 30000;
#endif
            for (int i = 0 ; i < max ; i++)
            {
                var basicDto = new BasicDto();
                basicDto.Foo = new DateTime(rand.Next(1980, 2020), rand.Next(1, 13),
                    rand.Next(1, 29), rand.Next(0, 24),
                    rand.Next(0, 60), rand.Next(0, 60));
                basicDto.Bar = (float)rand.NextDouble();
                list.Add(basicDto);
            }
            return list;
        }
        [Test]
        public void CompareBasicTypeForBandwidth()
        {
            var list = GetTestData();
            long pb, ndcs;
            using(var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, list);
                pb = ms.Length;
                //Debug.WriteLine(pb);
            }
            using (var ms = new MemoryStream())
            {
                new NetDataContractSerializer().Serialize(ms, list);
                ndcs = ms.Length;
                //Debug.WriteLine(ndcs);
            }
            Assert.That(0, Is.LessThan(1)); // double check! (at least one test API has this reversed)
            // sorry, we are going up in size, some core features were broken and now their fixed version requires more size
            Assert.That(pb, Is.LessThan(ndcs / 3));
        }
        [DataContract]
        public class BasicDto
        {
            [DataMember(Order = 1)]
            public DateTime Foo; //{ get;set;}
            [DataMember(Order = 2)]
            public float Bar; //{get;set;}
        }
    }
}

#endif