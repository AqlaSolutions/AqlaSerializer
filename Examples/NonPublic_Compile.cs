﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Examples
{
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
        [Test, ExpectedException(typeof(InvalidOperationException), ExpectedMessage = "Non-public type cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivateType")]
        public void PrivateTypeShouldFail()
        {
            Compile<PrivateType>();
        }
        private class NonPublicWrapper
        {
            [ProtoBuf.ProtoContract]
            internal class IndirectlyPrivateType
            {
            }
        }
        [Test, ExpectedException(typeof(InvalidOperationException), ExpectedMessage = "Non-public type cannot be used with full dll compilation: Examples.NonPublic_Compile+NonPublicWrapper+IndirectlyPrivateType")]
        public void IndirectlyPrivateTypeShouldFail()
        {
            Compile<NonPublicWrapper.IndirectlyPrivateType>();
        }
        [ProtoBuf.ProtoContract]
        public class PrivateCallback
        {
            [ProtoBuf.ProtoBeforeSerialization]
            private void OnDeserialize() { }
        }
        [Test, ExpectedException(typeof(InvalidOperationException), ExpectedMessage="Non-public member cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivateCallback.OnDeserialize")]
        public void PrivateCallbackShouldFail()
        {
            Compile<PrivateCallback>();
        }

        [ProtoBuf.ProtoContract]
        public class PrivateField
        {
#pragma warning disable 0169
            [ProtoBuf.ProtoMember(1)]
            private int Foo;
#pragma warning restore 0169
        }
        [Test, ExpectedException(typeof(InvalidOperationException), ExpectedMessage = "Non-public member cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivateField.Foo")]
        public void PrivateFieldShouldFail()
        {
            Compile<PrivateField>();
        }
        [ProtoBuf.ProtoContract]
        public class PrivateProperty
        {
            [ProtoBuf.ProtoMember(1)]
            private int Foo { get; set; }
        }
        [Test, ExpectedException(typeof(InvalidOperationException), ExpectedMessage = "Non-public member cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivateProperty.get_Foo")]
        public void PrivatePropertyShouldFail()
        {
            Compile<PrivateProperty>();
        }
        [ProtoBuf.ProtoContract]
        public class PrivatePropertyGet
        {
            [ProtoBuf.ProtoMember(1)]
            public int Foo { private get; set; }
        }
        [Test, ExpectedException(typeof(InvalidOperationException), ExpectedMessage="Non-public member cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivatePropertyGet.get_Foo")]
        public void PrivatePropertyGetShouldFail()
        {
            Compile<PrivatePropertyGet>();
        }
        [ProtoBuf.ProtoContract]
        public class PrivatePropertySet
        {
            [ProtoBuf.ProtoMember(1)]
            public int Foo { get; private set; }
        }
        [Test, ExpectedException(typeof(InvalidOperationException), ExpectedMessage = "Cannot apply changes to property Examples.NonPublic_Compile+PrivatePropertySet.Foo")]
        public void PrivatePropertySetShouldFail()
        {
            Compile<PrivatePropertySet>();
        }
        [ProtoBuf.ProtoContract]
        public class PrivateConditional
        {
            [ProtoBuf.ProtoMember(1)]
            public int Foo { get; set; }

            private bool ShouldSerializeFoo() { return true; }
        }
        [Test, ExpectedException(typeof(InvalidOperationException), ExpectedMessage = "Non-public member cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivateConditional.ShouldSerializeFoo")]
        public void PrivateConditionalSetShouldFail()
        {
            Compile<PrivateConditional>();
        }
        [ProtoBuf.ProtoContract]
        public class PrivateConstructor
        {
            private PrivateConstructor() { }
            [ProtoBuf.ProtoMember(1)]
            public int Foo { get; set; }
        }
        [Test, ExpectedException(typeof(InvalidOperationException), ExpectedMessage="Non-public member cannot be used with full dll compilation: Examples.NonPublic_Compile+PrivateConstructor..ctor")]
        public void PrivateConstructorShouldFail()
        {
            Compile<PrivateConstructor>();
        }
    }
}
