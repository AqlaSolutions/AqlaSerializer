using NUnit.Framework;
using System.IO;
using System.Reflection;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class Compatibility64bitVs32bit
    {
        [Test]
        public void ExecuteIntValues()
        {
            var write32 = typeof(ProtoWriter).GetMethod("WriteUInt32Variant", BindingFlags.Static | BindingFlags.NonPublic);
            var write64 = typeof(ProtoWriter).GetMethod("WriteUInt64Variant", BindingFlags.Static | BindingFlags.NonPublic);

            var read32 = typeof(ProtoReader).GetMethod("ReadUInt32Variant", BindingFlags.Instance | BindingFlags.NonPublic);
            var read64 = typeof(ProtoReader).GetMethod("ReadUInt64Variant", BindingFlags.Instance | BindingFlags.NonPublic);

            var ms32 = new MemoryStream();
            var ms64 = new MemoryStream();
            for (int i = 0; i <= int.MaxValue / 2; i += i < 1000 ? 123 : i < 10000 ? 1234 : i < 100000 ? 12345 : i < 1000000 ? 123456 : i < 10000000 ? 1234567 : 12345678)
            {
                ms32.SetLength(0);
                ms64.SetLength(0);
                var pw32 = new ProtoWriter(ms32, null, null);
                var pw64 = new ProtoWriter(ms64, null, null);
                write32.Invoke(null, new object[] { (uint)i, pw32 });
                write64.Invoke(null, new object[] { (ulong)i, pw64 });
                pw32.Close();
                pw64.Close();
                CollectionAssert.AreEqual(ms32.ToArray(), ms64.ToArray());


                ms64.Position = 0;
                var pr = new ProtoReader(ms64, null, null);
                Assert.That((uint)read32.Invoke(pr, new object[] { true }), Is.EqualTo(i));
                
                ms64.Position = 0;
                pr = new ProtoReader(ms64, null, null);
                Assert.That((uint)read32.Invoke(pr, new object[] { false }), Is.EqualTo(i));

                ms64.Position = 0;
                pr = new ProtoReader(ms64, null, null);
                Assert.That((ulong)read64.Invoke(pr, new object[] { }), Is.EqualTo(i));

            }
        }
    }
}