﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;
using System.IO;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    [TestFixture]
    public class SO6174298
    {
        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(10, typeof(BinaryNode))]
        public class Node
        {
            public virtual int Count() { return 1; }
        }

        [ProtoBuf.ProtoContract]
        public class BinaryNode : Node
        {
            public override int Count()
            {
                int count = 1;
                if (Left != null) count += Left.Count();
                if (Right != null) count += Right.Count();
                return count;

            }
            [ProtoBuf.ProtoMember(1, IsRequired = true)]
            public Node Left { get; set; }
            [ProtoBuf.ProtoMember(2, IsRequired = true)]
            public Node Right { get; set; }
        }

        [Test]
        public void Execute()
        {
            var model = TypeModel.Create();
            Execute(model, "runtime");

            model.CompileInPlace();
            Execute(model, "CompileInPlace");

            var pregen = model.Compile();
            Execute(pregen, "Compile");
        }

        public void Execute(TypeModel model, string caption)
        {
            BinaryNode head = new BinaryNode();
            BinaryNode node = head;
            // 13 is the magic limit that triggers recursion check        
            for (int i = 0; i < 13; ++i)
            {
                node.Left = new BinaryNode();
                node = (BinaryNode)node.Left;
            }

            var clone = (Node)model.DeepClone(head);
            Assert.AreEqual(head.Count(), clone.Count(), caption);
        }
    }
}
