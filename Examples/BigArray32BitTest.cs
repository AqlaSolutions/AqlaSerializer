using System;
using System.IO;
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
            public int[,] Array = new int[7000, 10000];
        }

        [SerializableType]
        public class Surrogate
        {
            public byte[] Test { get; set; } = new byte[311097602];

            public static implicit operator Surrogate(Wrapper w)
            {
                return new Surrogate();
            }

            public static implicit operator Wrapper(Surrogate s)
            {
                return new Wrapper();
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
                m.SerializeWithLengthPrefix(stream, new Wrapper(), typeof(Wrapper), PrefixStyle.Base128, 0);
                Assert.That(stream.Length, Is.AtLeast(311097602));
                stream.Position = 0;
                m.DeserializeWithLengthPrefix(stream, null, typeof(Wrapper), PrefixStyle.Base128, 0);
                Assert.That(stream.Position, Is.EqualTo(stream.Length));
            }

        }
    }
}