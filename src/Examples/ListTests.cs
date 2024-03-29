﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using Examples.Ppt;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Collections;
using System.Net;
using AqlaSerializer.Meta;
using Serializer = AqlaSerializer.Serializer;
using AqlaSerializer;

namespace Examples
{
    [ProtoBuf.ProtoContract]
    public class Entity
    {
        [ProtoBuf.ProtoMember(1)]
        public string Foo { get; set; }
    }

    public class CustomEnumerable : IEnumerable<int>
    {
        private readonly List<int> items = new List<int>();
        IEnumerator<int> IEnumerable<int>.GetEnumerator() { return items.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return items.GetEnumerator(); }
        public void Add(int value) { items.Add(value); }
    }
    [ProtoBuf.ProtoContract]
    public class EntityWithPackedInts
    {
        public void ClearList()
        {
            List = null;
        }
        public EntityWithPackedInts() { List = new List<int>(); }
        [ProtoBuf.ProtoMember(1, Options = ProtoBuf.MemberSerializationOptions.Packed)]
        public List<int> List {get;private set;}

        [ProtoBuf.ProtoMember(2, Options = ProtoBuf.MemberSerializationOptions.Packed)]
        public List<int> ListNoDefault { get; set; }

        [ProtoBuf.ProtoMember(3, Options = ProtoBuf.MemberSerializationOptions.Packed)]
        public int[] ItemArray { get; set; }

        [ProtoBuf.ProtoMember(4, Options = ProtoBuf.MemberSerializationOptions.Packed)]
        public CustomEnumerable Custom { get; set; }
    }
    [ProtoBuf.ProtoContract]
    public class EntityWithUnpackedInts
    {
        public EntityWithUnpackedInts() { Items = new List<int>(); }
        [ProtoBuf.ProtoMember(1)]
        public List<int> Items { get; private set; }

        [ProtoBuf.ProtoMember(2)]
        public List<int> ItemsNoDefault { get; set; }

        [ProtoBuf.ProtoMember(3)]
        public int[] ItemArray { get; set; }

        [ProtoBuf.ProtoMember(4)]
        public CustomEnumerable Custom { get; set; }
    }

    public class MyList : List<Entity> { }

    [TestFixture]
    public class ListTests
    {
        [Test]
        public void ListOfByteArray()
        {
            var data = new List<byte[]> {
                new byte[] {0,1,2,3,4},
                new byte[] {5,6,7},
                new byte[] {8,9,10},
                new byte[] {}
            };
            var clone = Serializer.DeepClone(data);

            Assert.AreNotSame(data, clone);
            Assert.AreEqual(4, clone.Count);
            Assert.IsTrue(data[0].SequenceEqual(clone[0]));
            Assert.IsTrue(data[1].SequenceEqual(clone[1]));
            Assert.IsTrue(data[2].SequenceEqual(clone[2]));
            Assert.IsTrue(data[3].SequenceEqual(clone[3]));
        }

        [Test]
        public void JaggedByteArray()
        {
            var data = new[] {
                new byte[] {0,1,2,3,4},
                new byte[] {5,6,7},
                new byte[] {8,9,10}
            };
            var clone = Serializer.DeepClone(data);

            Assert.AreNotSame(data, clone);
            Assert.AreEqual(3, clone.Length);
            Assert.IsTrue(data[0].SequenceEqual(clone[0]));
            Assert.IsTrue(data[1].SequenceEqual(clone[1]));
            Assert.IsTrue(data[2].SequenceEqual(clone[2]));
        }


        [Test]
        public void TestUnpackedIntListLayout()
        {
            EntityWithUnpackedInts item = new EntityWithUnpackedInts {
                Items = {1,2,3,4,5,1000}
            };
            Assert.IsTrue(Program.CheckBytes(item, 08, 01, 08, 02, 08, 03, 08, 04, 08, 05, 08, 0xE8, 07));

            var clone = Serializer.DeepClone(item);
            Assert.AreNotSame(item.Items, clone.Items);
            Assert.IsTrue(item.Items.SequenceEqual(clone.Items));
        }

        [Test]
        public void TestUnpackedIntArrayLayout()
        {
            EntityWithUnpackedInts item = new EntityWithUnpackedInts
            {
                ItemArray = new int[] { 1, 2, 3, 4, 5, 1000 }
            };
            Assert.IsTrue(Program.CheckBytes(item, 0x18, 01, 0x18, 02, 0x18, 03, 0x18, 04, 0x18, 05, 0x18, 0xE8, 07));

            var clone = Serializer.DeepClone(item);
            Assert.AreNotSame(item.ItemArray, clone.ItemArray);
            Assert.IsTrue(item.ItemArray.SequenceEqual(clone.ItemArray));
        }

        [Test]
        public void TestUnpackedIntCustomLayout()
        {
            EntityWithUnpackedInts item = new EntityWithUnpackedInts
            {
                Custom = new CustomEnumerable { 1, 2, 3, 4, 5, 1000 }
            };
            Assert.IsTrue(Program.CheckBytes(item, 0x20, 01, 0x20, 02, 0x20, 03, 0x20, 04, 0x20, 05, 0x20, 0xE8, 07));

            var clone = Serializer.DeepClone(item);
            Assert.AreNotSame(item.Custom, clone.Custom);
            Assert.IsTrue(item.Custom.SequenceEqual(clone.Custom));
        }

        [Test]
        public void TestPackedIntListLayout()
        {
            EntityWithPackedInts item = new EntityWithPackedInts
            {
                List = { 1, 2, 3, 4, 5, 1000}
            };
            Assert.IsTrue(Program.CheckBytes(item, 0x0A, 07, 01, 02, 03, 04, 05, 0xE8, 07));

            var clone = Serializer.DeepClone(item);
            Assert.AreNotSame(item.List, clone.List);
            Assert.IsTrue(item.List.SequenceEqual(clone.List));
        }

        [Test]
        public void TestPackedIntArrayLayout()
        {
            EntityWithPackedInts item = new EntityWithPackedInts
            {
                ItemArray = new int[] { 1, 2, 3, 4, 5, 1000 }
            };
            item.ClearList();
            Assert.IsTrue(Program.CheckBytes(item, 0x1A, 07, 01, 02, 03, 04, 05, 0xE8, 07));

            var clone = Serializer.DeepClone(item);
            Assert.AreNotSame(item.ItemArray, clone.ItemArray);
            Assert.IsTrue(item.ItemArray.SequenceEqual(clone.ItemArray));
        }

        [Test]
        public void TestPackedIntCustomLayout()
        {
            EntityWithPackedInts item = new EntityWithPackedInts
            {
                Custom = new CustomEnumerable { 1, 2, 3, 4, 5, 1000 }
            };
            item.ClearList();
            Assert.IsTrue(Program.CheckBytes(item, 0x22, 07, 01, 02, 03, 04, 05, 0xE8, 07));

            var clone = Serializer.DeepClone(item);
            Assert.AreNotSame(item.Custom, clone.Custom);
            Assert.IsTrue(item.Custom.SequenceEqual(clone.Custom));
        }


        [Test]
        public void SerializePackedDeserializeUnpacked()
        {
            EntityWithPackedInts item = new EntityWithPackedInts
            {
                List = { 1, 2, 3, 4, 5, 1000 }
            };
            var tm = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
            EntityWithUnpackedInts clone = tm.ChangeType<EntityWithPackedInts, EntityWithUnpackedInts>(item);
            Assert.AreNotSame(item.List, clone.Items);
            Assert.IsTrue(item.List.SequenceEqual(clone.Items));
        }

        [Test]
        public void SerializeUnpackedSerializePacked()
        {
            EntityWithUnpackedInts item = new EntityWithUnpackedInts
            {
                Items = { 1, 2, 3, 4, 5, 1000 }
            };
            var tm = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
            EntityWithPackedInts clone = tm.ChangeType<EntityWithUnpackedInts, EntityWithPackedInts>(item);
            Assert.AreNotSame(item.Items, clone.List);
            Assert.IsTrue(item.Items.SequenceEqual(clone.List));
        }

        [Test]
        public void PackedEmptyListDeserializesAsEmpty()
        {
            var item = new EntityWithPackedInts();
            Assert.IsNull(item.ListNoDefault);
            EntityWithPackedInts clone;// = Serializer.DeepClone(item);
            //Assert.IsNull(clone.ListNoDefault);

            item.ListNoDefault = new List<int>();
            clone = Serializer.DeepClone(item);
            Assert.IsNotNull(clone.ListNoDefault);
            Assert.AreEqual(0, clone.ListNoDefault.Count);

            item.ListNoDefault.Add(123);
            clone = Serializer.DeepClone(item);
            Assert.IsNotNull(clone.ListNoDefault);
            Assert.AreEqual(1, clone.ListNoDefault.Count);
            Assert.AreEqual(123, clone.ListNoDefault[0]);
        }

        [Test]
        public void PackedEmptyArrayDeserializesAsEmpty()
        {
            var item = new EntityWithPackedInts();
            Assert.IsNull(item.ItemArray);
            var clone = Serializer.DeepClone(item);
            Assert.IsNull(clone.ItemArray);

            item.ItemArray = new int[0];
            clone = Serializer.DeepClone(item);
            Assert.IsNotNull(clone.ItemArray);
            Assert.AreEqual(0, clone.ItemArray.Length);

            item.ItemArray = new int[1] { 123 };
            clone = Serializer.DeepClone(item);
            Assert.IsNotNull(clone.ItemArray);
            Assert.AreEqual(1, clone.ItemArray.Length);
            Assert.AreEqual(123, clone.ItemArray[0]);
        }

        [Test]
        public void PackedNullListDeserializesAsNull()
        {
            var item = new EntityWithPackedInts();
            Assert.IsNull(item.ListNoDefault);
            var clone = Serializer.DeepClone(item);
            Assert.IsNull(clone.ListNoDefault);

            item.ListNoDefault = null;
            clone = Serializer.DeepClone(item);
            Assert.IsNull(clone.ListNoDefault);
        }

        [Test]
        public void PackedNullArrayDeserializesAsNull()
        {
            var item = new EntityWithPackedInts();
            Assert.IsNull(item.ItemArray);
            var clone = Serializer.DeepClone(item);
            Assert.IsNull(clone.ItemArray);

            item.ItemArray = null;
            clone = Serializer.DeepClone(item);
            Assert.IsNull(clone.ItemArray);
        }

        [Test]
        public void PackedEmptyCustomDeserializesAsEmpty()
        {
            var item = new EntityWithPackedInts();
            Assert.IsNull(item.Custom);
            var clone = Serializer.DeepClone(item);
            Assert.IsNull(clone.Custom);

            item.Custom = new CustomEnumerable();
            clone = Serializer.DeepClone(item);
            Assert.IsNotNull(clone.Custom);
            Assert.AreEqual(0, clone.Custom.Count());

            item.Custom.Add(123);
            clone = Serializer.DeepClone(item);
            Assert.IsNotNull(clone.Custom);
            Assert.AreEqual(123, item.Custom.Single());
        }

        [Test]
        public void TestEmtpyBasicListOfEntity()
        {
            var foos = new List<Entity>();
            var clone = Serializer.DeepClone(foos);
            Assert.IsNotNull(clone);
        }

        [Test]
        public void TestEmptyMyListOfEntity()
        {
            var foos = new MyList();
            var clone = Serializer.DeepClone(foos);
            Assert.IsNotNull(clone);
        }

        [Test]
        public void TestNonEmtpyBasicListOfEntity()
        {
            var foos = new List<Entity>
            {
                new Entity { Foo = "abc"},
                new Entity { Foo = "def"},
            };
            var clone = Serializer.DeepClone(foos);
            Assert.IsNotNull(clone);
            Assert.AreNotSame(foos, clone);
            Assert.AreEqual(foos.GetType(), clone.GetType());
            Assert.AreEqual(2, clone.Count);
            Assert.AreEqual(foos[0].Foo, clone[0].Foo);
            Assert.AreEqual(foos[1].Foo, clone[1].Foo);
        }

        [Test]
        public void TestNonEmptyMyListOfEntity()
        {
            var foos = new MyList()
            {
                new Entity { Foo = "abc"},
                new Entity { Foo = "def"},
            };
            var clone = Serializer.DeepClone(foos);
            Assert.IsNotNull(clone);
            Assert.AreNotSame(foos, clone);
            Assert.AreEqual(foos.GetType(), clone.GetType());
            Assert.AreEqual(2, clone.Count);
            Assert.AreEqual(foos[0].Foo, clone[0].Foo);
            Assert.AreEqual(foos[1].Foo, clone[1].Foo);
        }

        [Test]
        public void TestCompositeDictionary()
        {
            DictionaryTestEntity obj = new DictionaryTestEntity
            {
                Foo = "bar",
                Stuff =
                {
                    {"abc", CompositeType.Create(123)},
                    {"def", CompositeType.Create(DateTime.Today)},
                    {"ghi", CompositeType.Create("hello world")},
                }
            }, clone = Serializer.DeepClone(obj);

            Assert.IsNotNull(clone);
            Assert.AreNotSame(clone, obj);
            Assert.AreEqual("bar", clone.Foo);
            Assert.AreEqual(3, clone.Stuff.Count);
            Assert.AreEqual(123, clone.Stuff["abc"].Value);
            Assert.AreEqual(DateTime.Today, clone.Stuff["def"].Value);
            Assert.AreEqual("hello world", clone.Stuff["ghi"].Value);
        }

        [ProtoBuf.ProtoContract]
        public class DictionaryTestEntity
        {
            public DictionaryTestEntity() {
                Stuff = new CustomBox();
            }
            [ProtoBuf.ProtoMember(1)]
            public string Foo { get; set; }

            [ProtoBuf.ProtoMember(2)]
            public CustomBox Stuff { get; private set; }
        }


        public class CustomBox : Dictionary<string, CompositeType>
        {

        }

        [ProtoBuf.ProtoContract]
        [ProtoBuf.ProtoInclude(1, typeof(CompositeType<int>))]
        [ProtoBuf.ProtoInclude(2, typeof(CompositeType<DateTime>))]
        [ProtoBuf.ProtoInclude(3, typeof(CompositeType<string>))]
        abstract public class CompositeType
        {
            public static CompositeType<T> Create<T>(T value)
            {
                return new CompositeType<T> { Value = value };
            }

            protected abstract object ValueImpl {get;set;}
            public object Value
            {
                get { return ValueImpl; }
                set { ValueImpl = value; }
            }
        }
        [ProtoBuf.ProtoContract]
        public class CompositeType<T> : CompositeType
        {
            [ProtoBuf.ProtoMember(1)]
            public new T Value { get; set; }

            protected override object ValueImpl
            {
                get { return Value; }
                set { Value = (T)value; }
            }
        }

        [Test]
        public void TestListBytes()
        {
            List<Test3> list = new List<Test3> { new Test3 { C = new Test1 { A= 150} } };
            Serializer.DeepClone(list);
            // Actual 0A 05 1A 03 08 96 01 - don't know why, variant?
            //Assert.IsTrue(Program.CheckBytes(list, 0x0A, 0x09, 0x1a, 0x07, 0x10, 0x01, 0x52, 0x03, 0x08, 0x96, 0x01));
        }
        [Test]
        public void TestListContents()
        {
            List<Test3> list = new List<Test3>
            {
                new Test3 { C = new Test1 { A = 123}},
                new Test3 { C = new Test1 { A = 456}},
                new Test3 { C = new Test1 { A = 789}}
            };

            var clone = Serializer.DeepClone(list);
            CheckLists(list, clone);
        }

        public class Test3Enumerable : IEnumerable<Test3>
        {
            private readonly List<Test3> items = new List<Test3>();


            public IEnumerator<Test3> GetEnumerator()
            {
                foreach (var item in items) yield return item;
            }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Add(Test3 item) { items.Add(item); }
        }

        [Test]
        public void TestEnumerableBytes()
        {
            Test3Enumerable list = new Test3Enumerable { new Test3 { C = new Test1 { A = 150 } } };
            Serializer.DeepClone(list);
            // Actual: 0A 05 1A 03 08 96 01 why, variant??
            //Assert.IsTrue(Program.CheckBytes(list, 0x0A, 0x09, 0x1a, 0x07, 0x10, 0x01, 0x52, 0x03, 0x08, 0x96, 0x01));
        }

        [Test]
        public void TestEnumerableContents()
        {
            Test3Enumerable items = new Test3Enumerable
            {
                new Test3 { C = new Test1 { A = 123}},
                new Test3 { C = new Test1 { A = 456}},
                new Test3 { C = new Test1 { A = 789}}
            };

            var clone = Serializer.DeepClone(items);
            CheckLists(items, clone);
        }

        [Test]
        public void TestArrayBytes()
        {
            Test3[] list = new Test3[] { new Test3 { C = new Test1 { A = 150 } } };
            Serializer.DeepClone(list);
            // variant?
            //Assert.IsTrue(Program.CheckBytes(list, 0x0A, 0x09, 0x1a, 0x07, 0x10, 0x01, 0x52, 0x03, 0x08, 0x96, 0x01));
        }

        [Test]
        public void TestArrayContents()
        {
            Test3[] arr = new Test3[]
            {
                new Test3 { C = new Test1 { A = 123}},
                new Test3 { C = new Test1 { A = 456}},
                new Test3 { C = new Test1 { A = 789}}
            };

            var clone = Serializer.DeepClone(arr);
            CheckLists(arr, clone);
        }

        [Ignore("Not relevant for Aqla")]
        [Test]
        public void TestPackedArrayString()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => {
                Serializer.DeepClone(new ArrayOfString());
            });
            Assert.That(ex.Message, Is.EqualTo("Only simple data-types can use packed encoding"));
        }
        [ProtoBuf.ProtoContract]
        public class ArrayOfString
        {
            [ProtoBuf.ProtoMember(1, Options = ProtoBuf.MemberSerializationOptions.Packed)]
            public string[] Items { get; set; }
        }
        [Ignore("Not relevant for Aqla")]
        [Test]
        public void TestPackedListDateTime()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => {
                Serializer.DeepClone(new ListOfDateTime());
            });
            Assert.That(ex.Message, Is.EqualTo("Only simple data-types can use packed encoding"));
        }
        [ProtoBuf.ProtoContract]
        public class ListOfDateTime
        {
            [ProtoBuf.ProtoMember(1, Options = ProtoBuf.MemberSerializationOptions.Packed)]
            public List<DateTime> Items { get; set; }
        }
        [Ignore("Not relevant for Aqla")]
        [Test]
        public void TestPackedCustomOfSubMessage()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => {
                Serializer.DeepClone(new CustomOfSubMessage());
            });
            Assert.That(ex.Message, Is.EqualTo("Only simple data-types can use packed encoding"));
        }

        [ProtoBuf.ProtoContract]
        public class CustomOfSubMessage
        {
            [ProtoBuf.ProtoMember(1, Options = ProtoBuf.MemberSerializationOptions.Packed)]
            public CustomCollection Items { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class CustomItem { }
        public class CustomCollection : IEnumerable<CustomItem>
        {
            public void Add(CustomItem item) { throw new NotImplementedException(); }
            public IEnumerator<CustomItem> GetEnumerator() { throw new NotImplementedException(); }
            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        }

        public class Test3Comparer : IEqualityComparer<Test3>
        {

            public bool Equals(Test3 x, Test3 y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x == null || y == null) return false;
                if (ReferenceEquals(x.C, y.C)) return true;
                if (x.C == null || y.C == null) return false;
                return x.C.A == y.C.A;
            }
            public int GetHashCode(Test3 obj)
            {
                throw new System.NotImplementedException();
            }


        }
        static void CheckLists(IEnumerable<Test3> original, IEnumerable<Test3> clone)
        {
            Assert.IsTrue(original.SequenceEqual(clone,new Test3Comparer()));
        }

        [Test]
        public void CheckNakedLinkedListCanRoundtrip()
        {
            var list = new LinkedList<BasicItem>();
            list.AddLast(new BasicItem{Value="abc"});
            list.AddLast(new BasicItem{Value="def"});
            var clone = Serializer.DeepClone(list);
            Assert.AreEqual(2, clone.Count);
            Assert.AreEqual("abc", clone.First.Value.Value);
            Assert.AreEqual("def", clone.Last.Value.Value);
        }
        [Test]
        public void CheckWrappedLinkedListCanRoundtrip()
        {
            var wrapper = new WithLinkedList();
            wrapper.Items.AddLast(new BasicItem { Value = "abc" });
            wrapper.Items.AddLast(new BasicItem { Value = "def" });
            var clone = Serializer.DeepClone(wrapper);
            Assert.AreEqual(2, clone.Items.Count);
            Assert.AreEqual("abc", clone.Items.First.Value.Value);
            Assert.AreEqual("def", clone.Items.Last.Value.Value);
        }
        [ProtoBuf.ProtoContract]
        public class BasicItem
        {
            [ProtoBuf.ProtoMember(1)]
            public string Value { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class WithLinkedList
        {
            [ProtoBuf.ProtoMember(1)]
            public LinkedList<BasicItem> Items { get; private set; }

            public WithLinkedList()
            {
                Items = new LinkedList<BasicItem>();
            }
        }

        [Test]
        public void SetTest()
        {
            var obj = new WithISet() { Collection = new HashSet<int>() { 1, 2, 3, 4, 5 } };
            var cloned = Serializer.DeepClone(obj);
            CollectionAssert.AreEquivalent(obj.Collection, cloned.Collection);
        }

        [SerializableType]
        public class WithISet
        {
            [SerializableMember(1)]
            public ISet<int> Collection;
        }

        [Test]
        public void ReadOnlyDictionaryTest()
        {
            var obj = new WithReadOnlyDictinary() { Collection = new Dictionary<int, byte>() { [1] = 11, [2] = 22, [3] = 33 } };
            var cloned = Serializer.DeepClone(obj);
            CollectionAssert.AreEquivalent(obj.Collection, cloned.Collection);
        }

        [SerializableType]
        public class WithReadOnlyDictinary
        {
            [SerializableMember(1)]
            public IReadOnlyDictionary<int, byte> Collection;
        }

#if NET5_0_OR_GREATER
        [Test]
        public void ReadOnlySetTest()
        {
            var obj = new WithReadOnlySet() { Collection = new HashSet<int>() { 1, 2, 3, 4, 5 } };
            var cloned = Serializer.DeepClone(obj);
            CollectionAssert.AreEquivalent(obj.Collection, cloned.Collection);
        }

        [SerializableType]
        public class WithReadOnlySet
        {
            [SerializableMember(1)]
            public IReadOnlySet<int> Collection;
        }
#endif
        [Test]
        public void CloneListOfList([Values(true,false)] bool setDefaultType)
        {
            var model = TypeModel.Create();
            MetaType outer = model.Add(typeof(List<IList<int>>), true);
            if (setDefaultType)
                outer.SetNestedSettingsWhenRoot(x => x.V.Collection.ConcreteType = typeof(int[]), 1);
            else
            {
                model.Add(typeof(IList<int>), true)
                    .AddSubType(1, typeof(int[]));
            }
            var listOfList = new List<IList<int>> { new[] { 1, 2 } };
            var clone = model.DeepClone(listOfList);
            CollectionAssert.AreEqual(listOfList, clone);
            Assert.AreEqual(listOfList[0].GetType(), clone[0].GetType());
        }
    }
}
