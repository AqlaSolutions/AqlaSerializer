// Modified by Vladyslav Taranov for AqlaSerializer, 2014
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
    public class Issue312
    {
        [ProtoBuf.ProtoContract]
        public class RootClass
        {
            [ProtoBuf.ProtoMember(1)]
            public int Prop1 { get; set; }
            [ProtoBuf.ProtoMember(2)]
            public string Prop2 { get; set; }
            [ProtoBuf.ProtoMember(3)]
            public IEnumerableClass Prop3 { get; set; }
        }

        [ProtoBuf.ProtoContract(IgnoreListHandling = true)]
        public class IEnumerableClass : IEnumerable<char>
        {
            [ProtoBuf.ProtoMember(1)]
            public int Prop1 { get; set; }
            [ProtoBuf.ProtoMember(2)]
            public string Prop2 { get; set; }

            public IEnumerator<char> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            // Comment out this indexed property to prevent the crash
            public char this[int i] { get { return Prop2[i]; } }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }

        [Test]
        public void Execute()
        {
            var rt = TypeModel.Create();
            rt.Add(typeof(IEnumerableClass), true);
            rt.Add(typeof(RootClass), true);
            rt.Compile();
            var c1 = new IEnumerableClass() { Prop1 = 1, Prop2 = "a" };
            var i1 = new RootClass() { Prop1 = 1, Prop2 = "blabla", Prop3 = c1 };
            var cloned = rt.DeepClone(i1) as RootClass;
            Assert.AreEqual(1, cloned.Prop3.Prop1);
        }
    }
}
