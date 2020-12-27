// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Examples.Issues
{
    [TestFixture]
    public class MissingBaseTypes
    {
        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(15, typeof(D)), ProtoBuf.ProtoInclude(16, typeof(B)), ProtoBuf.ProtoInclude(17, typeof(C))]
        public class A
        {
            [ProtoBuf.ProtoMember(1)]
            public int DataA { get; set; }
        }

        [ProtoBuf.ProtoContract]

        public class B : A
        {
        }

        [ProtoBuf.ProtoContract]
        public class C : A
        {
        }

        [ProtoBuf.ProtoContract]
        public class D : A
        {

            [ProtoBuf.ProtoMember(4)]
            public int DataD { get; set; }


            [ProtoBuf.ProtoMember(5)]
            public List<C> DataB { get; set; }
        }


        [ProtoBuf.ProtoContract]
        public class TestCase
        {
            [ProtoBuf.ProtoMember(10)]
            public D DataD;

            [ProtoBuf.ProtoMember(11)]
            public List<A> DataA;

        }

        [Test]
        public void Execute()
        {

            var model = TypeModel.Create();
            model.Add(typeof(A), true);
            model.Add(typeof(B), true);
            model.Add(typeof(C), true);
            model.Add(typeof(D), true);
            model.Add(typeof(TestCase), true);

            string s = model.GetSchema(null);

            Assert.IsNull(model[typeof(A)].BaseType, "A");
            Assert.AreSame(model[typeof(A)], model[typeof(B)].BaseType, "B");
            Assert.AreSame(model[typeof(A)], model[typeof(C)].BaseType, "C");
            Assert.AreSame(model[typeof(A)], model[typeof(D)].BaseType, "D");
            Assert.IsNull(model[typeof(TestCase)].BaseType, "TestCase");
        }
    }
}
