﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue218
    {
        [ProtoBuf.ProtoContract]
        public class Test
        {
            [ProtoBuf.ProtoMember(1)]
            public byte[] BackgroundImageToUpload { get; set; }

            [ProtoBuf.ProtoMember(2)]
            public string Title { get; set; }
        }
        [Test]
        public void Execute()
        {

            var typeModel = TypeModel.Create();
            typeModel.AutoCompile = true;
            var obj = new Test() {Title = "MyTitle", BackgroundImageToUpload = new byte[0]};

            var clone = (Test)typeModel.DeepClone(obj);
            Assert.IsNotNull(clone.BackgroundImageToUpload, "Runtime");
            Assert.AreEqual(0, clone.BackgroundImageToUpload.Length, "Runtime");
            Assert.AreEqual("MyTitle", clone.Title, "Runtime");

            typeModel.CompileInPlace();
            clone = (Test)typeModel.DeepClone(obj);
            Assert.IsNotNull(clone.BackgroundImageToUpload, "CompileInPlace");
            Assert.AreEqual(0, clone.BackgroundImageToUpload.Length, "CompileInPlace");
            Assert.AreEqual("MyTitle", clone.Title, "CompileInPlace");

            clone = (Test)typeModel.Compile().DeepClone(obj);
            Assert.IsNotNull(clone.BackgroundImageToUpload, "Compile");
            Assert.AreEqual(0, clone.BackgroundImageToUpload.Length, "Compile");
            Assert.AreEqual("MyTitle", clone.Title, "Compile");

        }
    }
}
