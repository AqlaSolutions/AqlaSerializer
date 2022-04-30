using System;
using System.IO;
using NUnit.Framework;

namespace ProtoBuf.Test.Issues
{
    public class Issue871
    {
        [Test]
        public void CanRoundTripValuesWithLengthPrefix()
        {
            using var ms = new MemoryStream();
            int qtyExpected = 1;
            var whenExpected = new DateTime(2021, 1, 1);
            Serializer.SerializeWithLengthPrefix(ms, qtyExpected, PrefixStyle.Base128, 0); // 02-08-01
            Serializer.SerializeWithLengthPrefix(ms, whenExpected, PrefixStyle.Base128); // 06-0A-04-08-88-A3-02

            var hex = BitConverter.ToString(ms.GetBuffer(), 0, (int)ms.Length);
            Assert.AreEqual("02-08-01-06-0A-04-08-88-A3-02", hex); // captured on 2.4.6

            ms.Position = 0;
            int qtyActual = Serializer.DeserializeWithLengthPrefix<int>(ms, PrefixStyle.Base128);
            Assert.AreEqual(qtyExpected, qtyActual);
            var whenActual = Serializer.DeserializeWithLengthPrefix<DateTime>(ms, PrefixStyle.Base128);
            Assert.AreEqual(whenExpected, whenActual);
        }

        [Test]
        public void CanRoundtripWithoutLengthPrefix()
        {
            using var ms = new MemoryStream();
            var whenExpected = new DateTime(2021, 1, 1);
            Serializer.Serialize(ms, whenExpected);

            var hex = BitConverter.ToString(ms.GetBuffer(), 0, (int)ms.Length);
            Assert.AreEqual("0A-04-08-88-A3-02", hex); // captured on 2.4.6

            ms.Position = 0;
            var whenActual = Serializer.Deserialize<DateTime>(ms);
            Assert.AreEqual(whenExpected, whenActual);

        }
    }
}
