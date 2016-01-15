// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples.Issues
{
    [TestFixture]
    public class SO7347694
    {
        [ProtoBuf.ProtoContract]
        public class Thing
        {
            [ProtoBuf.ProtoMember(1)] private readonly string _name;

            public string Name
            {
                get { return _name; }
            }

            public Thing()
            {}

            public Thing(string name)
            {
                _name = name;
            }
        }

        [Test]
        public void SerializeTheEasyWay()
        {
            var list = GetListOfThings();

            using (var fs = File.Create(@"things.bin"))
            {
                AqlaSerializer.Serializer.Serialize(fs, list);

                fs.Close();
            }

            using (var fs = File.OpenRead(@"things.bin"))
            {
                list = AqlaSerializer.Serializer.Deserialize<MyDto>(fs);

                Assert.AreEqual(3, list.Things.Count);
                Assert.AreNotSame(list.Things[0], list.Things[1]);
                Assert.AreSame(list.Things[0], list.Things[2]);

                fs.Close();
            }
        }

        [ProtoBuf.ProtoContract]
        public class MyDto
        {
            [ProtoBuf.ProtoMember(1, AsReference = true)]
            public List<Thing> Things { get; set; }
        }

        private MyDto GetListOfThings()
        {
            var thing1 = new Thing("thing1");
            var thing2 = new Thing("thing2");

            var list = new List<Thing>();
            list.Add(thing1);
            list.Add(thing2);
            list.Add(thing1);

            return new MyDto {Things = list};
        }
    }
}
