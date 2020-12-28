// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NETCOREAPP
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using AqlaSerializer.ServiceModel;

namespace Examples.Issues
{
    [TestFixture]
    public class SO10841807
    {
        [Test]
        public void Execute()
        {
            string aqn = typeof (ProtoBehaviorExtension).AssemblyQualifiedName;
            Assert.IsTrue(Regex.IsMatch(aqn, @"AqlaSerializer\.ServiceModel\.ProtoBehaviorExtension, aqlaserializer, Version=[0-9.]+, Culture=neutral, PublicKeyToken=2f3289788dcdcc09"));
            Console.WriteLine("WCF AQN: " + aqn);
        }
    }
}

#endif