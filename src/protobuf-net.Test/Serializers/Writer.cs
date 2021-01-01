// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Serializers
{
<<<<<<< HEAD
    [TestFixture]
=======
>>>>>>> 0bd254189a523f5332a2518461c7a1c41fecae0c
    public class Writer
    {
        [Test]
        public void TestString_abc()
        {
            Util.Test((ProtoWriter pw, ref ProtoWriter.State st) =>
            {
                ProtoWriter.WriteFieldHeader(1, WireType.String, pw, ref st);
                ProtoWriter.WriteString("abc", pw, ref st);
            }, "0A03616263");
        }
        [Test]
        public void TestVariantInt32()
        {
            for (int i = 0; i < 128; i++)
            {
                Util.Test((ProtoWriter pw, ref ProtoWriter.State st) =>
                  {
                      ProtoWriter.WriteFieldHeader(1, WireType.Variant, pw, ref st);
                      ProtoWriter.WriteInt32(i, pw, ref st);
                  }, "08" // 1 * 8 + 0
                 + i.ToString("X2")
                );
            }
            Util.Test((ProtoWriter pw, ref ProtoWriter.State st) => {
                ProtoWriter.WriteFieldHeader(1, WireType.Variant, pw, ref st);
                ProtoWriter.WriteInt32(128, pw, ref st);
            }, "088001");
        }
    }
}