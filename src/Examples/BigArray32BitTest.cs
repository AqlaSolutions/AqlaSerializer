using System;
using System.IO;
using System.Linq;
using AqlaSerializer;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace Examples
{
    [TestFixture]
    public class BigArray32BitTest
    {
        public class Wrapper
        {
            public int Count;
        }

        [SerializableType]
        public class Surrogate
        {
            public byte[] Test { get; set; }
            public int Count { get; set; }

            public static implicit operator Surrogate(Wrapper w)
            {
                if (w == null) return new Surrogate();
                byte[] array = new byte[w.Count];
                for (int i = 0; i < array.Length; i++)
                    array[i] = (byte)(i % 255);

                return new Surrogate() { Test = array, Count = w.Count };
            }

            public static implicit operator Wrapper(Surrogate s)
            {
                Assert.That(s.Test.Length, Is.EqualTo(s.Count));

                // check first 100
                for (int i = 0; i < s.Test.Length && i < 100; i += 1)
                {
                    var b = (byte)(i % 255);
                    Assert.That(s.Test[i], Is.EqualTo(b));
                }

                // we can't check the whole array
                var rnd = new Random(12424);

                for (int i = 0; i < s.Test.Length; i += rnd.Next(1, 2000))
                {
                    var b = (byte)(i % 255);
                    Assert.That(s.Test[i], Is.EqualTo(b));
                }

                // check last 100
                for (int i = s.Test.Length - 100; i < s.Test.Length; i += 1)
                {
                    var b = (byte)(i % 255);
                    Assert.That(s.Test[i], Is.EqualTo(b));
                }
                return new Wrapper() { Count = s.Count };
            }
        }

#if !DEBUG
        [TestCase(400 * 1024 * 1024)]
#else
        [TestCase(1 * 1024 * 1024)]
#endif
        public void Execute(int count)
        {
            var m = TypeModel.Create();
            m.Add(typeof(Wrapper), false).SetSurrogate(typeof(Surrogate));
            m.Compile("BigArray32BitTest", "BigArray32BitTest.dll");
            PEVerify.AssertValid("BigArray32BitTest.dll");
            const string fileName = "BigArray32BitTest.bin";
            TestClone(fileName, count, m);
            File.Delete(fileName);
        }

        //[Test]
        public void GenerateCorrect([Values(400 * 1024 * 1024)] int count)
        {
            var m = TypeModel.Create();
            m.Add(typeof(Wrapper), false).SetSurrogate(typeof(Surrogate));
            m.AllowStreamRewriting = false;
            const string fileName = "BigArray32BitTest_correct.bin";
            TestClone(fileName, count, m);
        }

        static void TestClone(string fileName, int size, RuntimeTypeModel m)
        {
            using (var stream = new FileStream(fileName, FileMode.Create))
            {
                stream.SetLength(0);
                GC.GetTotalMemory(true);
                m.SerializeWithLengthPrefix(stream, new Wrapper() { Count = size }, typeof(Wrapper), PrefixStyle.Base128, 0);
                Assert.That(stream.Length, Is.AtLeast(size));
                Assert.That(stream.Length, Is.LessThan(size + 10000));
                stream.Flush(true);
                GC.GetTotalMemory(true);
                stream.Position = 0;
                var w = (Wrapper)m.DeserializeWithLengthPrefix(stream, null, typeof(Wrapper), PrefixStyle.Base128, 0);
                Assert.That(w.Count, Is.EqualTo(size));
                Assert.That(stream.Position, Is.EqualTo(stream.Length));
            }
        }
    }
}