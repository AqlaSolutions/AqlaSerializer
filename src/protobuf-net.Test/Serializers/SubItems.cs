// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.IO;
using AqlaSerializer.Meta;

namespace AqlaSerializer.unittest.Serializers
{
<<<<<<< HEAD
    [TestFixture]
=======
>>>>>>> 0bd254189a523f5332a2518461c7a1c41fecae0c
    public class SubItems
    {
        [Test]
        public void TestWriteSubItemWithShortBlob() {
            Util.Test((ProtoWriter pw, ref ProtoWriter.State st) =>
            {
<<<<<<< HEAD
                ProtoWriter.WriteFieldHeader(5, WireType.String, pw);
                SubItemToken token = ProtoWriter.StartSubItemWithoutWritingHeader(new object(), pw);
                ProtoWriter.WriteFieldHeader(6, WireType.String, pw);
                ProtoWriter.WriteBytes(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, pw);
                ProtoWriter.EndSubItem(token, pw);
=======
                ProtoWriter.WriteFieldHeader(5, WireType.String, pw, ref st);
                SubItemToken token = ProtoWriter.StartSubItem(new object(), pw, ref st);
                ProtoWriter.WriteFieldHeader(6, WireType.String, pw, ref st);
                ProtoWriter.WriteBytes(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, pw, ref st);
                ProtoWriter.EndSubItem(token, pw, ref st);
>>>>>>> 0bd254189a523f5332a2518461c7a1c41fecae0c
            }, "2A" // 5 * 8 + 2 = 42
             + "0A" // sub-item length = 10
             + "32" // 6 * 8 + 2 = 50 = 0x32
             + "08" // BLOB length
             + "0001020304050607"); // BLOB
        }
    }
}
