using System;
using System.IO;
using System.Linq;
using AqlaSerializer;
using NUnit.Framework;
using ProtoBuf.Meta;

namespace ProtoBuf.unittest.Aqla
{
    [TestFixture]
    public class CoreReferenceSerialization
    {
        [SerializableType]
        public class TypeContainer
        {
            public Type[] Types { get; set; }
        }

        [Test]
        public void ExecuteType()
        {
            var model = TypeModel.Create();
            using (var ms = new MemoryStream())
            {
                TypeContainer c;
                c = new TypeContainer()
                        {
                            Types = Enumerable.Range(0, 2).Select(x => typeof(int)).ToArray()
                        };

                model.Serialize(ms, c);
                long length1 = ms.Length;
                ms.SetLength(0);

                c = new TypeContainer()
                {
                    Types = Enumerable.Range(0, 4).Select(x => typeof(int)).ToArray()
                };

                model.Serialize(ms, c);

                long length2 = ms.Length;
                float ratio = (float)length2 / (float)length1;

                Assert.Less(ratio, 1.2f);

                ms.Position = 0;

                var d = model.Deserialize<TypeContainer>(ms);
                Assert.AreSame(c.Types[0], c.Types[1]);
                Assert.AreSame(d.Types[0], d.Types[1]);

            }
        }

        [Test]
        public void CheckCloneType()
        {
            var model = TypeModel.Create();

            TypeContainer c;
            c = new TypeContainer()
                    {
                        Types = Enumerable.Range(0, 10).Select(x => typeof(int)).ToArray()
                    };
            var clone = model.DeepClone(c);
            Assert.IsTrue(c.Types.SequenceEqual(clone.Types));
        }

        [SerializableType]
        public class UriContainer
        {
            public Uri[] Uris { get; set; }
        }

        [Test]
        public void ExecuteUri()
        {
            var model = TypeModel.Create();
            using (var ms = new MemoryStream())
            {
                UriContainer c;
                var uri = new Uri("http://localhost");
                c = new UriContainer()
                        {
                            Uris = Enumerable.Range(0, 2).Select(x => uri).ToArray()
                        };

                model.Serialize(ms, c);

                ms.Position = 0;

                var d = model.Deserialize<UriContainer>(ms);
                Assert.AreSame(c.Uris[0], c.Uris[1]);
                Assert.AreSame(d.Uris[0], d.Uris[1]);

            }
        }

        [Ignore("For V2")] // TODO fix in V2
        [Test]
        public void ExecuteUriArray()
        {
            var model = TypeModel.Create();
            using (var ms = new MemoryStream())
            {
                var uri = new Uri("http://localhost");
                var c = Enumerable.Range(0, 2).Select(x => uri).ToArray();

                model.Serialize(ms, c);

                ms.Position = 0;

                var d = model.Deserialize<Uri[]>(ms);
                Assert.AreSame(c[0], c[1]);
                Assert.AreSame(d[0], d[1]);

            }
        }

        [Test]
        public void CheckCloneUri()
        {
            var model = TypeModel.Create();

            UriContainer c;

            var uri = new Uri("http://localhost");
            c = new UriContainer()
                    {
                        Uris = Enumerable.Range(0, 10).Select(x => uri).ToArray()
                    };
            var clone = model.DeepClone(c);
            Assert.IsTrue(c.Uris.SequenceEqual(clone.Uris));
        }

        [Test]
        public void CheckSimpleCloneUriArray()
        {
            var model = TypeModel.Create();

            var uri = new Uri("http://localhost");
            var c = Enumerable.Range(0, 10).Select(x => uri).ToArray();
            var clone = model.DeepClone(c);
            Assert.IsTrue(c.SequenceEqual(clone));
        }
    }
}