using System;
using System.IO;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class MultiDimensionalArrays
    {
        [SerializableType]
        public class Element : IEquatable<Element>
        {
            public int Value { get; set; }

            public bool Equals(Element other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Value == other.Value;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Element)obj);
            }

            public override string ToString()
            {
                return Value.ToString();
            }
        }

        [SerializableType]
        public class Container
        {
            public Element[,,,] Values { get; set; }
        }


        Element[,,,] MakeArray()
        {
            Element[,,,] arr = new Element[3, 2, 1, 10];
            FillArray(arr, 0, 0, 0, 0);
            return arr;
        }

        Element[,,,] MakeArrayTwice()
        {
            Element[,,,] arr = new Element[6, 2, 1, 10];
            FillArray(arr, 0, 0, 0, 0);
            FillArray(arr, 3, 0, 0, 0);
            return arr;
        }

        static void FillArray(Element[,,,] arr, int aPos, int bPos, int cPos, int dPos)
        {
            int m = 0;
            
            for (int a = aPos; a < arr.GetLength(0); a++)
            {
                for (int b = bPos; b < arr.GetLength(1); b++)
                {
                    for (int c = cPos; c < arr.GetLength(2); c++)
                    {
                        for (int d = dPos; d < arr.GetLength(3); d++)
                        {
                            m++;
                            arr[a, b, c, d] = new Element() { Value = m };
                        }
                    }
                }
            }
        }

        [Test]
        public void TestFramework()
        {
            var arr = MakeArray();
            var originalArr = MakeArray();
            Assert.That(arr, Is.EqualTo(originalArr));
            arr[0, 0, 0, 0].Value++;
            Assert.That(arr, Is.Not.EqualTo(originalArr));
        }


        [Test]
        public void Contained()
        {
            var tm = TypeModel.Create();
            var obj = new Container { Values = MakeArray() };
            var copy = tm.DeepClone(obj);
            Assert.That(copy.Values, Is.EqualTo(obj.Values));
        }


        [Test]
        public void Append()
        {
            var tm = TypeModel.Create();
            tm.Add(typeof(Container), true)[1].SetSettings(x => x.V.Collection.Append = true);
            var obj = new Container { Values = MakeArray() };
            using (var ms = new MemoryStream())
            {
                tm.Serialize(ms, obj);
                ms.Position = 0;
                var copy = (Container)tm.Deserialize(ms, null, typeof(Container));
                ms.Position = 0;
                copy = tm.Deserialize(ms, copy);
                var twice = MakeArrayTwice();
                Assert.That(copy.Values, Is.EqualTo(twice));
            }
        }

        [Test]
        public void Root()
        {
            var tm = TypeModel.Create();
            var original = MakeArray();
            var copy = tm.DeepClone(original);
            Assert.That(copy, Is.EqualTo(original));
        }
    }
}