using System;
using System.IO;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [SerializableType]
    public class MultiDimensionalArraysElement : IEquatable<MultiDimensionalArraysElement>
    {
        public int Value { get; set; }

        public bool Equals(MultiDimensionalArraysElement other)
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
            return Equals((MultiDimensionalArraysElement)obj);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    [TestFixture]
    public class MultiDimensionalArraysNormal : MultiDimensionalArrays<MultiDimensionalArraysElement>
    {
        public MultiDimensionalArraysNormal()
            : base(x => x.Value,
                   (ref MultiDimensionalArraysElement x, int v) =>
                   {
                       if (x == null)
                           x = new MultiDimensionalArraysElement() { Value = v };
                       else
                           x.Value = v;
                   })
        {
        }
    }

    [TestFixture]
    public class MultiDimensionalArraysInt : MultiDimensionalArrays<int>
    {
        public MultiDimensionalArraysInt()
            : base(x => x, (ref int x, int v) => x = v)
        {
        }
    }

    [TestFixture]
    public class MultiDimensionalArraysDouble : MultiDimensionalArrays<double>
    {
        public MultiDimensionalArraysDouble()
            : base(x => (int)x, (ref double x, int v) => x = v)
        {
        }
    }

    public class MultiDimensionalArrays<T>
    {
        public delegate void WriterDelegate(ref T x, int value);

        readonly Func<T, int> _reader;
        readonly WriterDelegate _writer;

        public MultiDimensionalArrays(Func<T, int> reader, WriterDelegate writer)
        {
            _reader = reader;
            _writer = writer;
        }

        [SerializableType]
        public class Container
        {
            public T[,,,] Values { get; set; }
        }

        [SerializableType]
        public class Container2
        {
            public T[,] Values { get; set; }
        }


        T[,,,] MakeArray()
        {
            T[,,,] arr = new T[3, 2, 1, 10];
            FillArray(arr, 0, 0, 0, 0);
            return arr;
        }

        T[,,,] MakeArrayTwice()
        {
            T[,,,] arr = new T[6, 2, 1, 10];
            FillArray(arr, 0, 0, 0, 0);
            FillArray(arr, 3, 0, 0, 0);
            return arr;
        }

        void FillArray(T[,,,] arr, int aPos, int bPos, int cPos, int dPos)
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
                            _writer(ref arr[a, b, c, d], m);
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
            _writer(ref arr[0, 0, 0, 0], _reader(arr[0, 0, 0, 0]) + 1);
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
        public void Contained2()
        {
            var tm = TypeModel.Create();
            var obj = new Container2 { Values = MakeArray2() };
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

        [Test]
        public void Root2()
        {
            var tm = TypeModel.Create();
            var original = MakeArray2();
            var copy = tm.DeepClone(original);
            Assert.That(copy, Is.EqualTo(original));
        }

        T[,] MakeArray2()
        {
            var original = new T[2, 2];
            _writer(ref original[1, 0], 123);
            return original;
        }
    }
}