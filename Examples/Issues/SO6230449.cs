// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using System.Collections.Generic;

namespace Examples.Issues
{
    [TestFixture]
    public class SO6230449
    {
        [ProtoBuf.ProtoContract]
        public class Foo
        {
            [ProtoBuf.ProtoMember(1)]
            public int Bar { get; set; }
        }

        [Test]
        public void Execute()
        {
            using (var ms = new MemoryStream())
            {
                // write data with a length-prefix but no field number
                var tm = TypeModel.Create(false, ProtoCompatibilitySettings.FullCompatibility);
                tm.SerializeWithLengthPrefix(ms, new Foo { Bar = 1 }, typeof(Foo), PrefixStyle.Base128, 0);
                tm.SerializeWithLengthPrefix(ms, new Foo { Bar = 2 }, typeof(Foo), PrefixStyle.Base128, 0);
                tm.SerializeWithLengthPrefix(ms, new Foo { Bar = 3 }, typeof(Foo), PrefixStyle.Base128, 0);

                ms.Position = 0;
                Assert.AreEqual(9, ms.Length, "3 lengths, 3 headers, 3 values");

                // read the length prefix and use that to limit each call
                int len, fieldNumber, bytesRead;
                List<Foo> foos = new List<Foo>();
                do
                {
                    len = ProtoReader.ReadLengthPrefix(ms, false, PrefixStyle.Base128, out fieldNumber, out bytesRead);
                    if (bytesRead <= 0) continue;

                    foos.Add((Foo)tm.Deserialize(ms, null, typeof(Foo), len));

                    Assert.IsTrue(foos.Count <= 3, "too much data! (manual)");
                } while (bytesRead > 0);

                Assert.AreEqual(3, foos.Count);
                Assert.AreEqual(1, foos[0].Bar);
                Assert.AreEqual(2, foos[1].Bar);
                Assert.AreEqual(3, foos[2].Bar);

                // do it using DeserializeItems
                ms.Position = 0;

                foos.Clear();
                foreach (var obj in tm.DeserializeItems<Foo>(ms, PrefixStyle.Base128, 0))
                {
                    foos.Add(obj);
                    Assert.IsTrue(foos.Count <= 3, "too much data! (foreach)");
                }
                Assert.AreEqual(3, foos.Count);
                Assert.AreEqual(1, foos[0].Bar);
                Assert.AreEqual(2, foos[1].Bar);
                Assert.AreEqual(3, foos[2].Bar);
            }
        }
    }
}
