using System.IO;
using NUnit.Framework;

namespace ProtoBuf.Issues
{
    public class Issue472
    {
        [Test]
        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        public void Execute(byte boolvalue, bool expected)
        {
            byte[] buffer = { 8, boolvalue };
            using var ms = new MemoryStream(buffer);
            using var state = new ProtoReader(ms, null);
            var fieldNumber = state.ReadFieldHeader();
            var value = state.ReadBoolean();

            Assert.AreEqual(expected, value);
        }
    }
}
