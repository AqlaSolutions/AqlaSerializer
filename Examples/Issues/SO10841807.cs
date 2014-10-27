// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ProtoBuf.ServiceModel;

namespace Examples.Issues
{
    [TestFixture]
    public class SO10841807
    {
        [Test]
        public void Execute()
        {
            string aqn = typeof (ProtoBehaviorExtension).AssemblyQualifiedName;
            Assert.IsTrue(Regex.IsMatch(aqn, @"ProtoBuf\.ServiceModel\.ProtoBehaviorExtension, protobuf\-net, Version=[0-9.]+, Culture=neutral, PublicKeyToken=7fa5d10c26ef1210"));
            Console.WriteLine("WCF AQN: " + aqn);
        }
    }
}
