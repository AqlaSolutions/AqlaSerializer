// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples
{
    [TestFixture]
    public class ShadowSetters
    {
        [ProtoBuf.ProtoContract]
        public class TypeWithShadowSetter
        {
            private int foo, bar, blap;
            [ProtoBuf.ProtoMember(1)]
            public int NoSetter { get { return foo; } }
            public void SetNoSetter(int value) { this.foo = value; }

            [ProtoBuf.ProtoMember(2)]
            public int SetterWeDontWantToInvoke { get { return bar; } set { throw new InvalidOperationException("oops, shadow should have won"); } }
            public void SetSetterWeDontWantToInvoke(int value) { this.bar = value; }

            [ProtoBuf.ProtoMember(3)]
            public int SetterWithUnrelatedSet { get { return blap; } set { blap = value; } }
            public void SetSetterWithUnrelatedSet(string value) { throw new InvalidOperationException("shouldn't have called this"); }
        }
        [Test]
        public void RoundTripWithShadow()
        {
            var orig = new TypeWithShadowSetter();
            orig.SetNoSetter(123);
            orig.SetSetterWeDontWantToInvoke(456);
            orig.SetterWithUnrelatedSet = 789;
            var clone = Serializer.DeepClone(orig);
            Assert.AreEqual(123, clone.NoSetter);
            Assert.AreEqual(456, clone.SetterWeDontWantToInvoke);
            Assert.AreEqual(789, clone.SetterWithUnrelatedSet);
        }
    }
}
