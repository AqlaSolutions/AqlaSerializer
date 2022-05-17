using AqlaSerializer;
using AqlaSerializer.Meta;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Examples
{
    [TestFixture]
    public class Deflate
    {
        [Test]
        public void TestCompress()
        {
            byte[] bytes;
            using (MemoryStream stream = new MemoryStream())
            {
                using (DeflateStream dest = new DeflateStream(stream, CompressionMode.Compress))
                {
                    Serializer.Serialize(dest, "Test");
                }

                bytes = stream.ToArray();
            }

            using (var stream = new MemoryStream(bytes))
            using (DeflateStream dest = new DeflateStream(stream, CompressionMode.Decompress))
            {
                var str = Serializer.Deserialize<string>(dest);
                Assert.AreEqual("Test", str);
            }
        }

    }
}
