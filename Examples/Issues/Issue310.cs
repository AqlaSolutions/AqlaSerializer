// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue310
    {
        [Test]
        public void Execute()
        {
#pragma warning disable  0618
            string proto = Serializer.GetProto<Animal>();

            Assert.AreEqual(@"package Examples.Issues;

message Animal {
   optional int32 NumberOfLegs = 1 [default = 0];
   // the following represent sub-types; at most 1 should have a value
   optional Cat Cat = 2;
   optional Dog Dog = 3;
}
message Cat {
   repeated Animal AnimalsHunted = 1;
}
message Dog {
   optional string OwnerName = 1;
}
", proto);
        }

        [ProtoBuf.ProtoContract]
        [ProtoBuf.ProtoInclude(2, typeof(Cat))]
        [ProtoBuf.ProtoInclude(3, typeof(Dog))]
        public class Animal
        {
            [ProtoBuf.ProtoMember(1)]
            public int NumberOfLegs { get; set; }
        }

        [ProtoBuf.ProtoContract]
        public class Dog : Animal {
            [ProtoBuf.ProtoMember(1)]
            public string OwnerName { get; set; }
        }
        
        [ProtoBuf.ProtoContract]
        public class Cat : Animal
        {
            [ProtoBuf.ProtoMember(1)]
            public List<Animal> AnimalsHunted { get; set; }
        }
    }
}
