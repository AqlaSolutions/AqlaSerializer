using System;
using System.Collections.Generic;
using System.IO;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class LateReferenceVersioning
    {
        [SerializableType]
        public class Container
        {
            public Referenced Value { get; set; }
        }

        [SerializableType(ImplicitFirstTag = 2)]
        public class Referenced
        {
            public int Data { get; set; }

            [SerializableMember(1, ValueFormat.LateReference)]
            public Referenced Next { get; set; }
        }

        [Test]
        public void VersioningSimple()
        {
            var original = new Container { Value = new Referenced() { Data = 123 } };
            Test(original, copy => Assert.That(copy.Value.Data, Is.EqualTo(original.Value.Data)));
        }

        [Test]
        public void VersioningNodesFew()
        {
            var original = new Container { Value = GenerateNodes(100) };
            original.Value.Data = 123;
            Test(original, copy => CheckNodes(original.Value, copy.Value));
        }

        [Test]
        public void VersioningNodesManyDeserializeAsNotLate()
        {
            var original = new Container { Value = GenerateNodes(4000) };
            original.Value.Data = 123;
            var copy = DeepClone(original, false);
            CheckNodes(original.Value, copy.Value);
        }

        [Test]
        public void VersioningNodesCantDeserializeFromNotLate()
        {
            var original = new Container { Value = GenerateNodes(4000) };
            original.Value.Data = 123;
            Assert.That(
                () => DeepClone(original, true),
                Throws.TypeOf<ProtoException>().With.Message.StartsWith("Recursion depth exceeded safe limit. See TypeModel.RecursionDepthLimit"));
        }

        [Test]
        public void EnsureLateWorks()
        {
            var original = new Container { Value = GenerateNodes(4000) };
            var tm1 = TypeModel.Create();
            tm1.SkipForcedLateReference = true;
            Container copy = tm1.DeepClone(original);
            CheckNodes(original.Value, copy.Value);
        }

        static void CheckNodes(Referenced original, Referenced copy)
        {
            while (original != null)
            {
                Assert.That(copy.Data, Is.EqualTo(original.Data));
                original = original.Next;
                copy = copy.Next;
            }
        }

        static Referenced GenerateNodes(int count)
        {
            var head = new Referenced() { Data = 123 };
            for (int i = 0; i < count; i++)
            {
                head = new Referenced() { Data = i, Next = head };
            }
            return head;
        }

        void Test(Container original, Action<Container> check)
        {
            check(DeepClone(original, false));
            check(DeepClone(original, true));
        }

        static Container DeepClone(Container original, bool notLateToLate)
        {
            var tm1 = TypeModel.Create();
            var tm2 = TypeModel.Create();

            tm1.SkipForcedLateReference = true;
            tm2.SkipForcedLateReference = true;

            ValueMember f = tm2.Add(typeof(Referenced), true)[1];
            f.Format = ValueFormat.Reference;

            if (notLateToLate)
            {
                var tmTemp = tm1;
                tm1 = tm2;
                tm2 = tmTemp;
            }

            Container copy;

            using (var ms = new MemoryStream())
            {
                tm1.Serialize(ms, original);

                ms.Position = 0;

                copy = tm2.Deserialize<Container>(ms);
            }
            return copy;
        }
    }
}