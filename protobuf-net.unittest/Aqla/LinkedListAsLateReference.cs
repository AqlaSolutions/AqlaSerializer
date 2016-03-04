using System;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class LinkedListAsLateReference
    {
        [SerializableType(ImplicitFirstTag = 3)]
        public class Node
        {
            [SerializableMember(1, ValueFormat.LateReference)]
            public Node Next { get; set; }

            [SerializableMember(2, ValueFormat.LateReference)]
            public Node Prev { get; set; }

            public int Value { get; set; }

            public Node(int value)
            {
                Value = value;
            }

            public Node()
            {
            }
        }

        [SerializableType]
        public class Container
        {
            public Node Head { get; set; }
            public Node Tail { get; set; }
        }

        [Test]
        public void Execute([Values(true, false)] bool late)
        {
            int index = 0;
            var first = new Node(index++);
            Node current = first;
            const int max = 5000; // magic number for StackOverflowException
            while (index < max)
            {
                var node = new Node(index) { Prev = current };
                current.Next = node;
                current = node;
                index++;
            }

            var original = new Container() { Tail = current, Head = first };
            
            var tm = TypeModel.Create();
            
            tm.SkipForcedLateReference = true;

            if (!late)
            {
                var t = tm.Add(typeof(Node), true);
                t[1].SetSettings(x => x.V.Format = ValueFormat.Reference);

                t[2].SetSettings(x => x.V.Format = ValueFormat.Reference);
            }

            Container copy;

            if (late)
                copy = tm.DeepClone(original);
            else
            {
                Assert.That(
                    () => copy = tm.DeepClone(original),
                    Throws.TypeOf<ProtoException>().With.Message.StartsWith("Recursion depth exceeded safe limit. See TypeModel.RecursionDepthLimit"));
                return;
            }

            current = copy.Head;
            index = 0;
            while (index < max)
            {
                Assert.That(current, Is.Not.Null);
                Assert.That(current.Value, Is.EqualTo(index));
                if (current.Next != null)
                    Assert.That(current.Next.Prev, Is.EqualTo(current));
                current = current.Next;
                index++;
            }
        }
    }
}