// Modified by Vladyslav Taranov for AqlaSerializer, 2016
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
