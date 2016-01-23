// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    [TestFixture]
    public class SO8933251
    {
#if FAKE_COMPILE
        [Ignore]
#endif
        [Test]
        public void CheckTypeSpecificCompileInPlaceCascadesToBaseAndChildTypes()
        {
            var model = TypeModel.Create();
            model.AutoCompile = false;
            model[typeof(B)].CompileInPlace();

            Assert.IsTrue(model.IsPrepared(typeof(B)), "B"); // self
            Assert.IsFalse(model.IsPrepared(typeof(D)), "D"); // sub-sub-type
            Assert.IsFalse(model.IsPrepared(typeof(C)), "C"); // sub-type
            Assert.IsFalse(model.IsPrepared(typeof(A)), "A"); // base-type
        }

#if FAKE_COMPILE
        [Ignore]
#endif
        [Test]
        public void CheckGlobalCompileInPlaceCascadesToBaseAndChildTypes()
        {
            var model = TypeModel.Create();
            model.AutoCompile = false;
            model.Add(typeof (B), true); // give the model a clue!
            model.CompileInPlace();

            Assert.IsTrue(model.IsPrepared(typeof(D)), "D"); // sub-sub-type
            Assert.IsTrue(model.IsPrepared(typeof(C)), "C"); // sub-type
            Assert.IsTrue(model.IsPrepared(typeof(B)), "B"); // self
            Assert.IsTrue(model.IsPrepared(typeof(A)), "A"); // base-type
        }


        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(1, typeof(B))]
        public class A { }
        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(1, typeof(C))]
        public class B : A { }
        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(1, typeof(D))]
        public class C : B { }
        [ProtoBuf.ProtoContract]
        public class D : C { }
    }
}
