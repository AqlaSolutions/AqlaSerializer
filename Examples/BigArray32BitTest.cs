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

        [Test]
        public void Execute()
        {
            var m = TypeModel.Create();
            m.Add(typeof(Wrapper), false).SetSurrogate(typeof(Surrogate));
            using (var stream = new FileStream("BigArray32BitTest.bin", FileMode.Create))
            {
                stream.SetLength(0);
                int count = 900 * 1024 * 1024;
                GC.GetTotalMemory(true);
                m.SerializeWithLengthPrefix(stream, new Wrapper() { Count = count }, typeof(Wrapper), PrefixStyle.Base128, 0);
                Assert.That(stream.Length, Is.AtLeast(count));
                Assert.That(stream.Length, Is.LessThan(count + 10000));
                stream.Flush(true);
                GC.GetTotalMemory(true);
                stream.Position = 0;
                var w = (Wrapper)m.DeserializeWithLengthPrefix(stream, null, typeof(Wrapper), PrefixStyle.Base128, 0);
                Assert.That(w.Count, Is.EqualTo(count));
                Assert.That(stream.Position, Is.EqualTo(stream.Length));
            }
            File.Delete("BigArray32BitTest.bin");
        }
    }
}