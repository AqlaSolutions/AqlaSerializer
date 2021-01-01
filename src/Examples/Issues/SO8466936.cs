﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using System.Runtime.Serialization;

namespace Examples.Issues
{
    [TestFixture]
    public class SO8466936
    {
        [Test]
        public void Execute()
        {
            var model = RuntimeTypeModel.Create();
            model.AutoCompile = false;

            RunTest(model, "Runtime");
            model.CompileInPlace();
            RunTest(model, "CompileInPlace");
            RunTest(model.Compile(), "Compile");
        }

        private void RunTest(TypeModel model, string caption)
        {
            var foo = new Bar<int> {BaseValue = 123, Value = 456};
            var clone = (Bar<int>) model.DeepClone(foo);
            Assert.IsInstanceOf(typeof(Bar<int>), clone, caption);
            Assert.AreEqual(123, clone.BaseValue, caption);
            Assert.AreEqual(456, clone.Value, caption);
        }

        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(2, typeof(Bar<int>))]
        public class Foo
        {
            [ProtoBuf.ProtoMember(1)]
            public int BaseValue { get; set; }
        }

        [ProtoBuf.ProtoContract(IgnoreListHandling = true), DataContract]
        public class Bar<T> : Foo, IEnumerable<T>
        {
            public IEnumerator<T> GetEnumerator() { yield break; }
            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
            public void Add(T i)
            {
            }

            [DataMember(Order=1)]
            public int Value { get; set; }
        }

    }
}
