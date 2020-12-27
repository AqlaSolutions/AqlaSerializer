// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Examples
{

#if FAKE_COMPILE
    [Ignore]
#endif
    [TestFixture]
    public class NonPublic_Compile
    {
        private static void Compile<T>()
        {
            var model = TypeModel.Create();
            model.Add(typeof(T), true);
            string name = typeof(T).Name + "Serializer", path = name + ".dll";
            model.Compile(name, path);
            PEVerify.AssertValid(path);
            Assert.Fail("Should have failed");
        }
        [ProtoBuf.ProtoContract]
        private class PrivateType
        {
        }
        [Test]
        public void PrivateTypeShouldFail()
        {
            var ex = Assert.Throws<ProtoAggregateException>(() => {
                Compile<PrivateType>();
            });
            Assert.That(ex.Message, Is.EqualTo("One or multiple exceptions occurred: InvalidOperationException (Non-public type cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivateType)"));
        }
        private class NonPublicWrapper
        {
            [ProtoBuf.ProtoContract]
            internal class IndirectlyPrivateType
            {
            }
        }
        [Test]
        public void IndirectlyPrivateTypeShouldFail()
        {
            var ex = Assert.Throws<ProtoAggregateException>(() => {
                Compile<NonPublicWrapper.IndirectlyPrivateType>();
            });
            Assert.That(ex.Message, Is.EqualTo("One or multiple exceptions occurred: InvalidOperationException (Non-public type cannot be used with full dll compilation: Examples.NonPublic_Compile+NonPublicWrapper+IndirectlyPrivateType)"));
        }
        [ProtoBuf.ProtoContract]
        public class PrivateCallback
        {
            [ProtoBuf.ProtoBeforeSerialization]
            private void OnDeserialize() { }
        }
        [Test]
        public void PrivateCallbackShouldFail()
        {
            var ex = Assert.Throws<ProtoAggregateException>(() => {
                Compile<PrivateCallback>();
            });
            Assert.That(ex.Message, Is.EqualTo("One or multiple exceptions occurred: InvalidOperationException (Non-public member cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivateCallback.OnDeserialize)"));
        }

        [ProtoBuf.ProtoContract]
        public class PrivateField
        {
#pragma warning disable 0169
            [ProtoBuf.ProtoMember(1)]
            private int Foo;
#pragma warning restore 0169
        }
        [Test]
        public void PrivateFieldShouldFail()
        {
            var ex = Assert.Throws<ProtoAggregateException>(() => {
                Compile<PrivateField>();
            });
            Assert.That(ex.Message, Is.EqualTo("One or multiple exceptions occurred: InvalidOperationException (Non-public member cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivateField.Foo)"));
        }
        [ProtoBuf.ProtoContract]
        public class PrivateProperty
        {
            [ProtoBuf.ProtoMember(1)]
            private int Foo { get; set; }
        }
        [Test]
        public void PrivatePropertyShouldFail()
        {
            var ex = Assert.Throws<ProtoAggregateException>(() => {
                Compile<PrivateProperty>();
            });
            Assert.That(ex.Message, Is.EqualTo("One or multiple exceptions occurred: InvalidOperationException (Non-public member cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivateProperty.get_Foo)"));
        }
        [ProtoBuf.ProtoContract]
        public class PrivatePropertyGet
        {
            [ProtoBuf.ProtoMember(1)]
            public int Foo { private get; set; }
        }
        [Test]
        public void PrivatePropertyGetShouldFail()
        {
            var ex = Assert.Throws<ProtoAggregateException>(() => {
                Compile<PrivatePropertyGet>();
            });
            Assert.That(ex.Message, Is.EqualTo("One or multiple exceptions occurred: InvalidOperationException (Non-public member cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivatePropertyGet.get_Foo)"));
        }
        [ProtoBuf.ProtoContract]
        public class PrivatePropertySet
        {
            [ProtoBuf.ProtoMember(1)]
            public int Foo { get; private set; }
        }
        [Test]
        public void PrivatePropertySetShouldFail()
        {
            var ex = Assert.Throws<ProtoAggregateException>(() => {
                Compile<PrivatePropertySet>();
            });
            Assert.That(ex.Message, Is.EqualTo("One or multiple exceptions occurred: InvalidOperationException (Cannot apply changes to property Examples.NonPublic_Compile+PrivatePropertySet.Foo)"));
        }
        [ProtoBuf.ProtoContract]
        public class PrivateConditional
        {
            [ProtoBuf.ProtoMember(1)]
            public int Foo { get; set; }

            private bool ShouldSerializeFoo() { return true; }
        }
        [Test]
        public void PrivateConditionalSetShouldFail()
        {
            var ex = Assert.Throws<ProtoAggregateException>(() => {
                Compile<PrivateConditional>();
            });
            Assert.That(ex.Message, Is.EqualTo("One or multiple exceptions occurred: InvalidOperationException (Non-public member cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivateConditional.ShouldSerializeFoo)"));
        }
        [ProtoBuf.ProtoContract]
        public class PrivateConstructor
        {
            private PrivateConstructor() { }
            [ProtoBuf.ProtoMember(1)]
            public int Foo { get; set; }
        }
        [Test]
        public void PrivateConstructorShouldFail()
        {
            var ex = Assert.Throws<ProtoAggregateException>(() => {
                Compile<PrivateConstructor>();
            });
            Assert.That(ex.Message, Is.EqualTo("One or multiple exceptions occurred: InvalidOperationException (Non-public member cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivateConstructor..ctor)"));
        }
    }
}
