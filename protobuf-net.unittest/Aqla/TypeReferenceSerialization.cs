using System;
using System.IO;
using System.Linq;
using AqlaSerializer;
using NUnit.Framework;
using ProtoBuf.Meta;

namespace ProtoBuf.unittest.Aqla
{
    [TestFixture]
    public class TypeReferenceSerialization
    {
        [SerializableType]
        public class Container
        {
            public Type[] Types { get; set; }
        }

        [Test]
        public void Execute()
        {
            var model = TypeModel.Create();
            using (var ms = new MemoryStream())
            {
                Container c;
                c = new Container()
                        {
                            Types = Enumerable.Range(0, 2).Select(x => typeof(int)).ToArray()
                        };

                model.Serialize(ms, c);
                long length1 = ms.Length;
                ms.SetLength(0);

                c = new Container()
                {
                    Types = Enumerable.Range(0, 4).Select(x => typeof(int)).ToArray()
                };

                model.Serialize(ms, c);

                long length2 = ms.Length;
                float ratio = (float)length2 / (float)length1;

                Assert.Less(ratio, 1.2f);

            }
        }

        [Test]
        public void CheckClone()
        {
            var model = TypeModel.Create();

            Container c;
            c = new Container()
                    {
                        Types = Enumerable.Range(0, 10).Select(x => typeof(int)).ToArray()
                    };
            var clone = model.DeepClone(c);
            Assert.IsTrue(c.Types.SequenceEqual(clone.Types));
        }
    }
}