﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
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
    public class SO9398578
    {
        [Test]
        public void TestRandomDataWithString()
        {
            Assert.Throws<ProtoException>(() => {
                var input = File.ReadAllBytes("aqlaserializer.dll");
                var stream = new MemoryStream(input);
                stream.Seek(0, SeekOrigin.Begin);
                Assert.Greater(3, 0); // I always double-check the param order
                Assert.Greater(stream.Length, 0);
                Serializer.Deserialize<string>(stream);
            });
        }
        [Test]
        public void TestRandomDataWithContractType()
        {
            Assert.Throws<ProtoException>(() => {
                var input = File.ReadAllBytes("aqlaserializer.dll");
                var stream = new MemoryStream(input);
                stream.Seek(0, SeekOrigin.Begin);
                Assert.Greater(3, 0); // I always double-check the param order
                Assert.Greater(stream.Length, 0);
                Serializer.Deserialize<Foo>(stream);
            });
        }

        [Ignore("Last changes in ProtoReader allow this")]
        [Test]
        public void TestRandomDataWithReader()
        {
            Assert.Throws<ProtoException>(() => {
                var input = File.ReadAllBytes("aqlaserializer.dll");
                var stream = new MemoryStream(input);
                stream.Seek(0, SeekOrigin.Begin);
                Assert.Greater(3, 0); // I always double-check the param order
                Assert.Greater(stream.Length, 0);

                using (var reader = new ProtoReader(stream, null, null))
                {
                    while (reader.ReadFieldHeader() > 0)
                    {
                        reader.SkipField();
                    }
                }
            });
        }

        [ProtoBuf.ProtoContract]
        public class Foo
        {
        }
    }
}
