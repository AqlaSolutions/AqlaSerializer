using ProtoBuf.Meta;
using System;
using System.IO;
using NUnit.Framework;

namespace ProtoBuf.Test.Meta
{
    public class AddTypeEvents
    {
        [Test]
        [TestCase(false, false, true, 0, "08-01")] // vanilla
        [TestCase(true, true, true, 0, "08-01")] // vanilla, check callbacks invoked
        [TestCase(true, false, true, 0, "08-01")] // vanilla, check pre-only invoked
        [TestCase(false, true, true, 0, "08-01")] // vanilla, check post-only invoked
        [TestCase(true, true, true, 1, "10-01")] // offset by one
        [TestCase(true, true, false, 0, "")] // disable default config
        public void ConfigureModelWithCallbacks(bool before, bool after, bool allowDefault , int offset, string expected)
        {
            var model = RuntimeTypeModel.Create();
            bool beforeInvoked = false, afterInvoked = false;
            if (before) model.BeforeApplyDefaultBehaviour += (s, a) =>
            {
                beforeInvoked = true;
                a.ApplyDefaultBehaviour = allowDefault;
            };
            if (after) model.AfterApplyDefaultBehaviour += (s, a) =>
            {
                afterInvoked = true;
                a.MetaType.ApplyFieldOffset(offset);
            };
            using var ms = new MemoryStream();
            model.Serialize(ms, new Foo { X = 1 });
            string hex = BitConverter.ToString(ms.GetBuffer(), 0, (int)ms.Length);
            Assert.AreEqual(before, beforeInvoked);
            Assert.AreEqual(after, afterInvoked);
            Assert.AreEqual(expected, hex);
        }

        [ProtoContract]
        public class Foo
        {
            [ProtoMember(1)]
            public int X { get; set; }
        }
    }
}
