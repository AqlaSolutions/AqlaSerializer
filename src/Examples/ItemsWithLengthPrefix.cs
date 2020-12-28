// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using System.IO;
using AqlaSerializer;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using AqlaSerializer.Meta;

namespace Examples
{
    [TestFixture]
    public class ItemsWithLengthPrefix
    {
        static Stream WriteData(int tag, PrefixStyle style, params int[] values)
        {
            MemoryStream ms = new MemoryStream();
            Foo foo = new Foo();
            var tm = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
            foreach (int value in values)
            {
                foo.Value = value;
                tm.SerializeWithLengthPrefix(ms, foo, style, tag);
            }
            ms.Position = 0;
            return ms;
        }
        static int ReadIndividually(Stream source, int tag, PrefixStyle style, params int[] values)
        {
            int count = 0;
            foreach(int value in values)
            {
                if (source.Length == source.Position)
                {
                    Debugger.Break();
                }
                var tm = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility); ;
                Foo foo = tm.DeserializeWithLengthPrefix<Foo>(source, null, style, tag);
                Assert.AreEqual(value, foo.Value);
                count++;
            }
            return count;
        }

        static int ReadStreaming(Stream source, int tag, PrefixStyle style, params int[] values)
        {
            var list = Serializer.DeserializeItems<int>(source, style, tag).ToList();
            Assert.AreEqual(values.Length, list.Count, "Count");
            for (int i = 0; i < values.Length; i++ )
            {
                Assert.AreEqual(values[i], list[i], "Index " + i + ", value " + values[i]);
            }
            return values.Length;
        }

        private static int CheckIndividually(int tag, PrefixStyle style, params int[] values)
        {
            using(Stream source = WriteData(tag, style, values))
            {
                return ReadIndividually(source, tag, style, values);
            }
        }
        private static int CheckStreaming(int tag, PrefixStyle style, params int[] values)
        {
            using (Stream source = WriteData(tag, style, values))
            {
                return ReadStreaming(source, tag, style, values);
            }
        }

        [Test]
        public void ReadIndividuallyFixedLength()
        {
            Assert.AreEqual(8, CheckIndividually(0, PrefixStyle.Fixed32, -2,-1,0,1,2,3,4,5));
        }
        
        [Test]
        public void ReadIndividuallyBase128NoTag()
        {
            Assert.AreEqual(8, CheckIndividually(0, PrefixStyle.Base128, -2, -1, 0, 1, 2, 3, 4, 5));
        }

        [Test]
        public void ReadIndividuallyBase128Tag()
        {
            Assert.AreEqual(8, CheckIndividually(2, PrefixStyle.Base128, -2, -1, 0, 1, 2, 3, 4, 5));
        }

        [Test]
        public void ReadStreamingFixedLength()
        {
            Assert.AreEqual(8, CheckStreaming(0, PrefixStyle.Fixed32, -2, -1, 0, 1, 2, 3, 4, 5));
        }

        [Test]
        public void ReadStreamingBase128NoTag()
        {
            Assert.AreEqual(8, CheckStreaming(0, PrefixStyle.Base128, -2, -1, 0, 1, 2, 3, 4, 5));
        }

        [Test]
        public void ReadStreamingBase128Tag()
        {
            Assert.AreEqual(8, CheckStreaming(2, PrefixStyle.Base128, -2, -1, 0, 1, 2, 3, 4, 5));
        }

        [Test]
        public void ReadStreamingParentFixedLength()
        {
            ReadStreamingBase128Parent(PrefixStyle.Fixed32, 0);
        }
        [Test]
        public void ReadStreamingParentBase128Tag()
        {
            ReadStreamingBase128Parent(PrefixStyle.Base128, 3);
        }

        [Test]
        public void ReadStreamingParentBase128NoTag()
        {
            ReadStreamingBase128Parent(PrefixStyle.Base128, 0);
        }


        public void ReadStreamingBase128Parent(PrefixStyle style, int tag)
        {
            MemoryStream ms = new MemoryStream();
            IMLParent a, b, c;
            Serializer.SerializeWithLengthPrefix<IMLParent>(ms, a = InheritanceMidLevel.CreateChild(123, 456, 789), style, tag);
            Serializer.SerializeWithLengthPrefix<IMLParent>(ms, b = InheritanceMidLevel.CreateChild(100, 200, 300), style, tag);
            Serializer.SerializeWithLengthPrefix<IMLParent>(ms, c = InheritanceMidLevel.CreateChild(400, 500, 600), style, tag);
            ms.Position = 0;
            var list = Serializer.DeserializeItems<IMLParent>(ms, style, tag).ToList();
            Assert.AreEqual(3, list.Count, "Count");
            InheritanceMidLevel.CheckParent(a, list[0]);
            InheritanceMidLevel.CheckParent(b, list[1]);
            InheritanceMidLevel.CheckParent(c, list[2]);
        }


        [Test]
        public void ReadStreamingChildFixedLength()
        {
            ReadStreamingChild(PrefixStyle.Fixed32, 0);
        }

        public void ReadStreamingChild(PrefixStyle style, int tag)
        {
            MemoryStream ms = new MemoryStream();
            IMLChild a, b, c;
            Serializer.SerializeWithLengthPrefix<IMLChild>(ms, a = InheritanceMidLevel.CreateChild(123, 456, 789), style, tag);
            Serializer.SerializeWithLengthPrefix<IMLChild>(ms, b = InheritanceMidLevel.CreateChild(100, 200, 300), style, tag);
            Serializer.SerializeWithLengthPrefix<IMLChild>(ms, c = InheritanceMidLevel.CreateChild(400, 500, 600), style, tag);
            ms.Position = 0;
            var list = Serializer.DeserializeItems<IMLChild>(ms, style, tag).ToList();
            Assert.AreEqual(3, list.Count, "Count");
            InheritanceMidLevel.CheckChild(a, list[0]);
            InheritanceMidLevel.CheckChild(b, list[1]);
            InheritanceMidLevel.CheckChild(c, list[2]);
        }
        [Test]
        public void ReadStreamingChildBase128Tag()
        {
            ReadStreamingChild(PrefixStyle.Base128, 3);
        }

        [Test]
        public void ReadStreamingChildBase128NoTag()
        {
            ReadStreamingChild(PrefixStyle.Base128, 0);
        }

        [Test]
        public void TestEmptyStreams()
        {
            TestEmptyStreams<int>();
            TestEmptyStreams<IMLChild>();
            TestEmptyStreams<IMLParent>();
        }

        static void TestEmptyStreams<T>()
        {
            Assert.IsFalse(Serializer.DeserializeItems<T>(Stream.Null, PrefixStyle.Fixed32, 0).Any());
            Assert.IsFalse(Serializer.DeserializeItems<T>(Stream.Null, PrefixStyle.Base128, 0).Any());
            Assert.IsFalse(Serializer.DeserializeItems<T>(Stream.Null, PrefixStyle.Base128, 1).Any());

            Assert.IsFalse(Serializer.DeserializeItems<T>(new MemoryStream(), PrefixStyle.Fixed32, 0).Any());
            Assert.IsFalse(Serializer.DeserializeItems<T>(new MemoryStream(), PrefixStyle.Base128, 0).Any());
            Assert.IsFalse(Serializer.DeserializeItems<T>(new MemoryStream(), PrefixStyle.Base128, 1).Any());
        }
    }
}
