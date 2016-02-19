﻿using AqlaSerializer;
using AqlaSerializer.Meta;
using IKVM.Reflection;
using NUnit.Framework;

namespace precompile.tests
{
    [TestFixture]
    public class IKVMSerializableMemberAttributeTests
    {
        [Test]
        public void TestSimple()
        {
            var rtm = TypeModel.Create();
            Type t = LoadType(rtm);
            var runtime = MakeRuntime(rtm, t, "Simple");
            Assert.That(runtime.Tag, Is.EqualTo(1));
        }

        [Test]
        public void TestEnum()
        {
            var rtm = TypeModel.Create();
            Type t = LoadType(rtm);
            var runtime = MakeRuntime(rtm, t, "Enum");
            Assert.That(runtime.EnhancedWriteAs, Is.EqualTo(EnhancedMode.Reference));
        }

        [Test]
        public void TestNamed()
        {
            var rtm = TypeModel.Create();
            Type t = LoadType(rtm);
            var runtime = MakeRuntime(rtm, t, "Named");
            Assert.That(runtime.CollectionFormat, Is.EqualTo(CollectionFormat.Google));
        }

        static SerializableMemberAttribute MakeRuntime(RuntimeTypeModel rtm, Type t, string name)
        {

            var map = AttributeMap.Create(rtm, t.GetProperty(name), true);
            var attr = AttributeMap.GetAttribute(map, "AqlaSerializer.SerializableMemberAttribute");
            var runtime = attr.GetRuntimeAttribute<SerializableMemberAttribute>(rtm);
            return runtime;
        }

        static Type LoadType(RuntimeTypeModel rtm)
        {
            string configuration = "Release";
#if DEBUG
            configuration = "Debug";
#endif
            Assert.That(rtm.Universe.LoadFile($@"..\..\..\protobuf-net.unittest\bin\{configuration}\aqlaserializer.dll"), Is.Not.Null);
            var asm = rtm.Load($@"..\..\..\protobuf-net.unittest\bin\{configuration}\aqlaserializer.unittest.exe");
            var t = asm.GetType("AqlaSerializer.unittest.Aqla.ClassWithMembersForIKVM", true);
            return t;
        }
    }
}