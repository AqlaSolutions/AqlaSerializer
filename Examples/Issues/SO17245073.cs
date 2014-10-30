// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using System;

namespace Examples.Issues
{
    [TestFixture]
    public class SO17245073
    {
        [Test]
        public void Exec()
        {
            var model = TypeModel.Create();
            Assert.IsFalse(model[typeof(A)].EnumPassthru, "A");
            Assert.IsTrue(model[typeof(B)].EnumPassthru, "B");

            Assert.IsFalse(model[typeof(C)].EnumPassthru, "C");
            Assert.IsTrue(model[typeof(D)].EnumPassthru, "D");

            Assert.IsTrue(model[typeof(E)].EnumPassthru, "E");
            Assert.IsTrue(model[typeof(F)].EnumPassthru, "F");

            Assert.IsFalse(model[typeof(G)].EnumPassthru, "G");
            Assert.IsFalse(model[typeof(H)].EnumPassthru, "H");            
        }

        // no ProtoContract; with [Flags] is pass-thru, else not
        public enum A { X, Y, Z }
        [Flags]
        public enum B { X, Y, Z }

        // basic ProtoContract; with [Flags] is pass-thru, else not
        [ProtoBuf.ProtoContract]
        public enum C { X, Y, Z }
        [ProtoBuf.ProtoContract, Flags]
        public enum D { X, Y, Z }

        // ProtoContract with explicit pass-thru enabled; always pass-thru
        [ProtoBuf.ProtoContract(EnumPassthru = true)]
        public enum E { X, Y, Z }
        [ProtoBuf.ProtoContract(EnumPassthru = true), Flags]
        public enum F { X, Y, Z }

        // ProtoContract with explicit pass-thru disabled; never pass-thru (even if [Flags])
        [ProtoBuf.ProtoContract(EnumPassthru = false)]
        public enum G { X, Y, Z }
        [ProtoBuf.ProtoContract(EnumPassthru = false), Flags]
        public enum H { X, Y, Z }
    }
    
    
}
