﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using AqlaSerializer.Meta;
using System.Linq;
using ProtoBuf;
using Serializer = AqlaSerializer.Serializer;

namespace Examples
{
    [ProtoBuf.ProtoContract]
    class Node
    {
        [ProtoBuf.ProtoMember(1)]
        public int Key { get; set; }

        [ProtoBuf.ProtoMember(2)]
        public Node[] Nodes { get; set; }        
    }

    [ProtoBuf.ProtoContract]
    class Prim
    {
        [ProtoBuf.ProtoMember(1)]
        public string[] Values { get; set; }
    }

    [ProtoBuf.ProtoContract]
    public class ArrayArray
    {
        [ProtoBuf.ProtoMember(1)]
        public string[][] Values { get; set; }

        public static ArrayArray CreateFilled() => new ArrayArray()
        {
            Values = new []
            {
                new[] { "11", "12" },
                new[] { "21", "22", "23" },
                null,
                new string[0]
            }
        };
    }

    [ProtoBuf.ProtoContract]
    public class ArrayArrayCustom
    {
        [ProtoBuf.ProtoMember(1)]
        public CustomString[][] Values { get; set; }

        public static ArrayArrayCustom CreateFilled() => new ArrayArrayCustom()
        {
            Values = new CustomString[][]
            {
                new CustomString[] { "11", "12" },
                new CustomString[] { "21", "22", "23" },
                null,
                new CustomString[0]
            }
        };
    }

    [ProtoContract]
    public class CustomString
    {
        [ProtoMember(1)]
        public string Value { get; set; }

        public static implicit operator CustomString(string v)
        {
            return new CustomString() { Value = v };
        }

        protected bool Equals(CustomString other)
        {
            return string.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CustomString)obj);
        }

        public override int GetHashCode()
        {
            return (Value != null ? Value.GetHashCode() : 0);
        }

        public static bool operator ==(CustomString left, CustomString right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CustomString left, CustomString right)
        {
            return !Equals(left, right);
        }
    }

    [ProtoBuf.ProtoContract]
    class ArrayArrayArray
    {
        [ProtoBuf.ProtoMember(1)]
        public string[][][] Values { get; set; }

        public static ArrayArrayArray CreateFilled() => new ArrayArrayArray()
        {
            Values = new[]
                {
                    new[]
                    {
                        new[] { "111", "112" },
                        new[] { "121", "122", "123" },
                        null,
                        new string[0]
                    },
                    new string[0][],
                    null,
                    new[]
                    {
                        new[] { "311", "312" },
                        new[] { "321", "322", "323" },
                        null,
                        new string[0]
                    }
                }
        };
    }

    [ProtoBuf.ProtoContract]
    class ArrayList
    {
        [ProtoBuf.ProtoMember(1)]
        public List<string>[] Values { get; set; } 


        public static ArrayList CreateFilled() => new ArrayList()
        {
            Values = new[]
            {
                new List<string>(){ "11", "12" },
                new List<string>(){ "21", "22", "23" },
                null,
                new List<string>()
            }
        };
    }

    [ProtoBuf.ProtoContract]
    class ListArray
    {
        [ProtoBuf.ProtoMember(1)]
        public List<string[]> Values { get; set; }

        public static ListArray CreateFilled() => new ListArray()
        {
            Values = new List<string[]>()
            {
                new[] { "11", "12" },
                new[] { "21", "22", "23" },
                null,
                new string[0]
            }
        };
    }

    [ProtoBuf.ProtoContract]
    class ListList
    {
        [ProtoBuf.ProtoMember(1)]
        public List<List<string>> Values { get; set; }

        public static ListList CreateFilled() => new ListList()
        {
            Values = new List<List<string>>
            {
                new List<string>() { "11", "12" },
                new List<string>() { "21", "22", "23" },
                null,
                new List<string>()
            }
        };
    }

    [ProtoBuf.ProtoContract]
    class MultiDim
    {
        [ProtoBuf.ProtoMember(1)]
        public int[,] Values { get; set; }
    }

    [ProtoBuf.ProtoContract(SkipConstructor=false)]
    public class WithAndWithoutOverwrite
    {
        [ProtoBuf.ProtoMember(1, OverwriteList=false)]
        public int[] Append = { 1, 2, 3 };

        [ProtoBuf.ProtoMember(2, OverwriteList=true)]
        public int[] Overwrite = { 4, 5, 6 };
    }
    [ProtoBuf.ProtoContract(SkipConstructor=true)]
    public class WithSkipConstructor
    {
        [ProtoBuf.ProtoMember(1)]
        public int[] Values = { 1, 2, 3 };
    }

    [TestFixture]
    public class ArrayTests
    {
        [ProtoBuf.ProtoContract]
        class Foo { }
        [Test]
        public void DeserializeNakedArray()
        {
            var arr = new Foo[0];
            var model = TypeModel.Create();
            Foo[] foo = (Foo[])model.DeepClone(arr);
            Assert.AreEqual(0, foo.Length);
        }
        [Test]
        public void DeserializeBusyArray()
        {
            var arr = new Foo[3] { new Foo(), new Foo(), new Foo() };
            var model = TypeModel.Create();
            Foo[] foo = (Foo[])model.DeepClone(arr);
            Assert.AreEqual(3, foo.Length);
        }
        [Test]
        public void TestOverwriteVersusAppend()
        {
            var orig = new WithAndWithoutOverwrite { Append = new[] {7,8}, Overwrite = new[] { 9,10}};
            var model = TypeModel.Create();
            model.AutoCompile = false;
            model.Add(typeof(WithAndWithoutOverwrite), true);

            var clone = (WithAndWithoutOverwrite)model.DeepClone(orig);
            Assert.IsTrue(clone.Overwrite.SequenceEqual(new[] { 9, 10 }), "Overwrite, Runtime");
            Assert.IsTrue(clone.Append.SequenceEqual(new[] { 1, 2, 3, 7, 8 }), "Append, Runtime");

            model.CompileInPlace();
            clone = (WithAndWithoutOverwrite)model.DeepClone(orig);
            Assert.IsTrue(clone.Overwrite.SequenceEqual(new[] { 9, 10 }), "Overwrite, CompileInPlace");
            Assert.IsTrue(clone.Append.SequenceEqual(new[] { 1, 2, 3, 7, 8 }), "Append, CompileInPlace");

            clone = (WithAndWithoutOverwrite)(model.Compile()).DeepClone(orig);
            Assert.IsTrue(clone.Overwrite.SequenceEqual(new[] { 9, 10 }), "Overwrite, Compile");
            Assert.IsTrue(clone.Append.SequenceEqual(new[] { 1, 2, 3, 7, 8 }), "Append, Compile");
        }

        [Test]
        public void TestOverwriteVersusAppendComile()
        {
            var orig = new WithAndWithoutOverwrite { Append = new[] {7,8}, Overwrite = new[] { 9,10}};
            var model = TypeModel.Create();
            model.AutoCompile = false;
            model.Add(typeof(WithAndWithoutOverwrite), true);
            model.AutoAddMissingTypes = false;
            model.Compile();

            var clone = (WithAndWithoutOverwrite)model.DeepClone(orig);
            Assert.IsTrue(clone.Overwrite.SequenceEqual(new[] { 9, 10 }), "Overwrite, Runtime");
            Assert.IsTrue(clone.Append.SequenceEqual(new[] { 1, 2, 3, 7, 8 }), "Append, Runtime");

            model.CompileInPlace();
            clone = (WithAndWithoutOverwrite)model.DeepClone(orig);
            Assert.IsTrue(clone.Overwrite.SequenceEqual(new[] { 9, 10 }), "Overwrite, CompileInPlace");
            Assert.IsTrue(clone.Append.SequenceEqual(new[] { 1, 2, 3, 7, 8 }), "Append, CompileInPlace");

            clone = (WithAndWithoutOverwrite)(model.Compile()).DeepClone(orig);
            Assert.IsTrue(clone.Overwrite.SequenceEqual(new[] { 9, 10 }), "Overwrite, Compile");
            Assert.IsTrue(clone.Append.SequenceEqual(new[] { 1, 2, 3, 7, 8 }), "Append, Compile");
        }

        [Test]
        public void TestSkipConstructor()
        {
            var orig = new WithSkipConstructor { Values = new[] { 4, 5 } };
            var model = TypeModel.Create();
            model.AutoCompile = false;
            model.Add(typeof(WithSkipConstructor), true);

            var clone = (WithSkipConstructor)model.DeepClone(orig);
            Assert.IsTrue(clone.Values.SequenceEqual(new[] { 4, 5 }), "Runtime");

            model.CompileInPlace();
            clone = (WithSkipConstructor)model.DeepClone(orig);
            Assert.IsTrue(clone.Values.SequenceEqual(new[] { 4, 5 }), "CompileInPlace");

            clone = (WithSkipConstructor)(model.Compile()).DeepClone(orig);
            Assert.IsTrue(clone.Values.SequenceEqual(new[] { 4, 5 }), "Compile");
        }

        [Test]
        public void TestPrimativeArray()
        {
            Prim p = new Prim { Values = new[] { "abc", "def", "ghi", "jkl" } },
                clone = Serializer.DeepClone(p);

            string[] oldArr = p.Values, newArr = clone.Values;
            Assert.AreEqual(oldArr.Length, newArr.Length);
            for (int i = 0; i < oldArr.Length; i++)
            {
                Assert.AreEqual(oldArr[i], newArr[i], "Item " + i.ToString());
            }
        }

        [Test, ExpectedException(typeof(NotSupportedException))]
        public void TestMultidimArray()
        {
            MultiDim md = new MultiDim { Values = new int[1, 2] { { 3, 4 } } };
            Serializer.DeepClone(md);
        }

        [Test]
        public void TestArrayList([Values(false,true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = ArrayList.CreateFilled();
            var copy = tm.DeepClone(source);
            Assert.That(copy.Values, Is.EqualTo(source.Values));
        }

        [Test]
        public void TestArrayListDirect([Values(false,true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = ArrayList.CreateFilled().Values;
            var copy = tm.DeepClone(source);
            Assert.That(copy, Is.EqualTo(source));
        }

        [Test]
        public void TestArrayListNull([Values(false,true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = new ArrayList();
            var copy = tm.DeepClone(source);
            Assert.That(copy.Values, Is.EqualTo(source.Values));
        }

        [Test]
        public void TestListArrayDirect([Values(false, true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = ListArray.CreateFilled().Values;
            var copy = tm.DeepClone(source);
            Assert.That(copy, Is.EqualTo(source));
        }

        [Test]
        public void TestListArray([Values(false, true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = ListArray.CreateFilled();
            var copy = tm.DeepClone(source);
            Assert.That(copy.Values, Is.EqualTo(source.Values));
        }

        [Test]
        public void TestListArrayNull([Values(false, true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = new ListArray();
            var copy = tm.DeepClone(source);
            Assert.That(copy.Values, Is.EqualTo(source.Values));
        }

        [Test]
        public void TestListList([Values(false,true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = ListList.CreateFilled();
            var copy = tm.DeepClone(source);
            Assert.That(copy.Values, Is.EqualTo(source.Values));
        }

        [Test]
        public void TestListListDirect([Values(false,true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = ListList.CreateFilled().Values;
            var copy = tm.DeepClone(source);
            Assert.That(copy, Is.EqualTo(source));
        }

        [Test]
        public void TestListListNull([Values(false,true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = new ListList();
            var copy = tm.DeepClone(source);
            Assert.That(copy.Values, Is.EqualTo(source.Values));
        }

        [Test]
        public void TestArrayArray([Values(false,true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            if (compile)
            {
                tm.Add(typeof(ArrayArray), true);
                tm.Compile("arrayarray", "arrayarray.dll");
            }
            var source = ArrayArray.CreateFilled();
            var copy = tm.DeepClone(source);
            Assert.That(copy.Values, Is.EqualTo(source.Values));
            if (compile)
            {
                PEVerify.AssertValid("arrayarray.dll");
            }
        }

        [Test]
        public void TestArrayArrayDirect([Values(false,true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = ArrayArray.CreateFilled().Values;
            var copy = tm.DeepClone(source);
            Assert.That(copy, Is.EqualTo(source));
        }

        [Test]
        public void TestArrayArrayNull([Values(false,true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = new ArrayArray();
            var copy = tm.DeepClone(source);
            Assert.That(copy.Values, Is.EqualTo(source.Values));
        }

        [Test]
        public void TestArrayArrayCustom([Values(false,true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            if (compile)
            {
                tm.Add(typeof(ArrayArrayCustom), true);
                tm.Compile("ArrayArrayCustom", "ArrayArrayCustom.dll");
            }
            var source = ArrayArrayCustom.CreateFilled();
            var copy = tm.DeepClone(source);
            Assert.That(copy.Values, Is.EqualTo(source.Values));
            if (compile)
            {
                PEVerify.AssertValid("ArrayArrayCustom.dll");
            }
        }

        [Test]
        public void TestArrayArrayCustomDirect([Values(false,true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = ArrayArrayCustom.CreateFilled().Values;
            var copy = tm.DeepClone(source);
            Assert.That(copy, Is.EqualTo(source));
        }

        [Test]
        public void TestArrayArrayCustomNull([Values(false,true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = new ArrayArrayCustom();
            var copy = tm.DeepClone(source);
            Assert.That(copy.Values, Is.EqualTo(source.Values));
        }

        [Test]
        public void TestArrayArrayArray([Values(false, true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = ArrayArrayArray.CreateFilled();
            var copy = tm.DeepClone(source);
            Assert.That(copy.Values, Is.EqualTo(source.Values));
        }

        [Test]
        public void TestArrayArrayArrayDirect([Values(false, true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = ArrayArrayArray.CreateFilled().Values;
            var copy = tm.DeepClone(source);
            Assert.That(copy, Is.EqualTo(source));
        }

        [Test]
        public void TestArrayArrayArrayNull([Values(false, true)] bool compile)
        {
            var tm = TypeModel.Create();
            tm.AutoCompile = compile;
            var source = new ArrayArrayArray();
            var copy = tm.DeepClone(source);
            Assert.That(copy.Values, Is.EqualTo(source.Values));
        }

        [Test]
        public void TestObjectArray()
        {
            Node node = new Node
            {
                Key = 27,
                Nodes = new[] {
                    new Node { Key = 1 },
                    new Node { Key = 3 } }
            };
            VerifyNodeTree(node);
        }

        // [Test] known variation...
        public void TestEmptyArray()
        {
            Node node = new Node
            {
                Key = 27,
                Nodes = new Node[0]
            };
            VerifyNodeTree(node);
        }

        [Test]
        public void TestNullArray()
        {
            Node node = new Node
            {
                Key = 27,
                Nodes = null
            };
            VerifyNodeTree(node);
        }

        [Test]
        public void TestDuplicateNonRecursive()
        {
            Node child = new Node { Key = 17 };
            Node parent = new Node { Nodes = new[] { child, child, child } };
            VerifyNodeTree(parent);
        }

        [Test]
        public void TestDuplicateRecursive()
        {
            Node child = new Node { Key = 17 };
            Node parent = new Node { Nodes = new[] { child, child, child } };
            child.Nodes = new[] { parent };
            // arrays are asreferencedefault in AqlaSerializer
            //VerifyNodeTree(parent);
        }

        [Test]
        public void TestNestedArray()
        {
            Node node = new Node
            {
                Key = 27,
                Nodes = new[] {
                    new Node {
                        Key = 19,
                        Nodes = new[] {
                            new Node {Key = 1},
                            new Node {Key = 14},
                        },
                    },
                    new Node {
                        Key = 3
                    },
                    new Node {
                        Key = 3,
                        Nodes = new[] {
                            new Node {Key = 234}
                        }
                    }
                }
            };
            VerifyNodeTree(node);
        }

        [Test]
        public void TestStringArray()
        {
            var foo = new List<string> { "abc", "def", "ghi" };

            var clone = Serializer.DeepClone(foo);
                
        }

        [ProtoBuf.ProtoContract]
        internal class Tst
        {
            [ProtoBuf.ProtoMember(1)]
            public int ValInt
            {
                get;
                set;
            }

            [ProtoBuf.ProtoMember(2)]
            public byte[] ArrayData
            {
                get;
                set;
            }

            [ProtoBuf.ProtoMember(3)]
            public string Str1
            {
                get;
                set;
            }
        }
        [Test]
        public void TestEmptyArrays()
        {
            Tst t = new Tst();
            t.ValInt = 128;
            t.Str1 = "SOme string text value ttt";
            t.ArrayData = new byte[] { };

            MemoryStream stm = new MemoryStream();
            Serializer.Serialize(stm, t);
            Console.WriteLine(stm.Length);
        }
        static void VerifyNodeTree(Node node) {
            Node clone = Serializer.DeepClone(node);
            string msg;
            bool eq = AreEqual(node, clone, out msg);
            Assert.IsTrue(eq, msg);
        }

        static bool AreEqual(Node x, Node y, out string msg)
        {
            // compare core
            if (ReferenceEquals(x, y)) { msg = ""; return true; }
            if (x == null || y == null) { msg = "1 node null"; return false; }
            if (x.Key != y.Key) { msg = "key"; return false; }

            Node[] xNodes = x.Nodes, yNodes = y.Nodes;
            if (ReferenceEquals(xNodes,yNodes))
            { // trivial
            }
            else
            {
                if (xNodes == null || yNodes == null) { msg = "1 Nodes null"; return false; }
                if (xNodes.Length != yNodes.Length) { msg = "Nodes length"; return false; }
                for (int i = 0; i < xNodes.Length; i++)
                {
                    bool eq = AreEqual(xNodes[i], yNodes[i], out msg);
                    if (!eq)
                    {
                        msg = i.ToString() + "; " + msg;
                        return false;
                    }
                }
            }
            // run out of things to be different!
            msg = "";
            return true;

        }
    }
}
