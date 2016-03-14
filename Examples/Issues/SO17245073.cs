// Modified by Vladyslav Taranov for AqlaSerializer, 2016
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
            model.Add(typeof(A), true);
            model.Add(typeof(B), true);
            model.Add(typeof(C), true);
            model.Add(typeof(D), true);
            model.Add(typeof(E), true);
            model.Add(typeof(F), true);
            model.Add(typeof(G), true);
            model.Add(typeof(H), true);
            model.CompileInPlace();
            Assert.IsFalse(GetEnumPassthrough(model[typeof(A)]), "A");
            Assert.IsTrue(GetEnumPassthrough(model[typeof(B)]), "B");

            Assert.IsFalse(GetEnumPassthrough(model[typeof(C)]), "C");
            Assert.IsTrue(GetEnumPassthrough(model[typeof(D)]), "D");

            Assert.IsTrue(GetEnumPassthrough(model[typeof(E)]), "E");
            Assert.IsTrue(GetEnumPassthrough(model[typeof(F)]), "F");

            Assert.IsFalse(GetEnumPassthrough(model[typeof(G)]), "G");
            Assert.IsFalse(GetEnumPassthrough(model[typeof(H)]), "H");            
        }

        static bool GetEnumPassthrough(MetaType metaType)
        {
            return metaType.GetFinalSettingsCopy().EnumPassthru.Value;
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
