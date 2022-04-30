using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using NUnit.Framework;

namespace ProtoBuf.Serializers
{
    public class Collections
    {
        [Test]
        [TestCase(typeof(string), null)]
        [TestCase(typeof(DoubleEnumerable), null)] // ambiguous? you can go without, then!

        [TestCase(typeof(int[]), typeof(VectorSerializer<int>))]
        [TestCase(typeof(List<int>), typeof(ListSerializer<int>))]
        [TestCase(typeof(ListGenericSubclass<int>), typeof(ListSerializer<ListGenericSubclass<int>, int>))]
        [TestCase(typeof(ListNonGenericSubclass), typeof(ListSerializer<ListNonGenericSubclass, int>))]
        [TestCase(typeof(Collection<int>), typeof(EnumerableSerializer<Collection<int>, Collection<int>, int>))]
        [TestCase(typeof(ICollection<int>), typeof(EnumerableSerializer<ICollection<int>, ICollection<int>, int>))]
        [TestCase(typeof(IEnumerable<int>), typeof(EnumerableSerializer<IEnumerable<int>, IEnumerable<int>, int>))]
        [TestCase(typeof(IList<int>), typeof(EnumerableSerializer<IList<int>, IList<int>, int>))]
        [TestCase(typeof(Dictionary<int, string>), typeof(DictionarySerializer<int, string>))]
        [TestCase(typeof(IDictionary<int, string>), typeof(DictionarySerializer<IDictionary<int,string>,int, string>))]
        [TestCase(typeof(ImmutableArray<int>), typeof(ImmutableArraySerializer<int>))]
        [TestCase(typeof(ImmutableDictionary<int, string>), typeof(ImmutableDictionarySerializer<int, string>))]
        [TestCase(typeof(ImmutableSortedDictionary<int, string>), typeof(ImmutableSortedDictionarySerializer<int, string>))]
        [TestCase(typeof(IImmutableDictionary<int, string>), typeof(ImmutableIDictionarySerializer<int, string>))]
        [TestCase(typeof(Queue<int>), typeof(QueueSerializer<Queue<int>, int>))]
        [TestCase(typeof(Stack<int>), typeof(StackSerializer<Stack<int>, int>))]
        [TestCase(typeof(CustomGenericCollection<int>), typeof(EnumerableSerializer<CustomGenericCollection<int>, CustomGenericCollection<int>,int>))]
        [TestCase(typeof(CustomNonGenericCollection), typeof(EnumerableSerializer<CustomNonGenericCollection,CustomNonGenericCollection,bool>))]
        [TestCase(typeof(IReadOnlyCollection<string>), typeof(EnumerableSerializer<IReadOnlyCollection<string>, IReadOnlyCollection<string>, string>))]
        [TestCase(typeof(CustomNonGenericReadOnlyCollection), typeof(EnumerableSerializer<CustomNonGenericReadOnlyCollection, CustomNonGenericReadOnlyCollection, string>))]
        [TestCase(typeof(CustomGenericReadOnlyCollection<string>), typeof(EnumerableSerializer<CustomGenericReadOnlyCollection<string>, CustomGenericReadOnlyCollection<string>, string>))]

        [TestCase(typeof(ImmutableList<string>), typeof(ImmutableListSerializer<string>))]
        [TestCase(typeof(IImmutableList<string>), typeof(ImmutableIListSerializer<string>))]
        [TestCase(typeof(ImmutableHashSet<string>), typeof(ImmutableHashSetSerializer<string>))]
        [TestCase(typeof(ImmutableSortedSet<string>), typeof(ImmutableSortedSetSerializer<string>))]
        [TestCase(typeof(IImmutableSet<string>), typeof(ImmutableISetSerializer<string>))]
        [TestCase(typeof(ImmutableQueue<string>), typeof(ImmutableQueueSerializer<string>))]
        [TestCase(typeof(IImmutableQueue<string>), typeof(ImmutableIQueueSerializer<string>))]
        [TestCase(typeof(ImmutableStack<string>), typeof(ImmutableStackSerializer<string>))]
        [TestCase(typeof(IImmutableStack<string>), typeof(ImmutableIStackSerializer<string>))]

        [TestCase(typeof(ConcurrentBag<string>), typeof(ConcurrentBagSerializer<ConcurrentBag<string>, string>))]
        [TestCase(typeof(ConcurrentStack<string>), typeof(ConcurrentStackSerializer<ConcurrentStack<string>, string>))]
        [TestCase(typeof(ConcurrentQueue<string>), typeof(ConcurrentQueueSerializer<ConcurrentQueue<string>, string>))]
        [TestCase(typeof(IProducerConsumerCollection<string>), typeof(ProducerConsumerSerializer<IProducerConsumerCollection<string>, string>))]

        [TestCase(typeof(CustomEnumerable), typeof(EnumerableSerializer<CustomEnumerable, CustomEnumerable, int>))]
        [TestCase(typeof(Dictionary<int[], int>), typeof(DictionarySerializer<int[], int>))]
        [TestCase(typeof(Dictionary<int, int[]>), typeof(DictionarySerializer<int, int[]>))]
        [TestCase(typeof(Dictionary<int[], int[]>), typeof(DictionarySerializer<int[], int[]>))]

        public void TestWhatProviderWeGet(Type type, Type expected)
        {
            var provider = RepeatedSerializers.TryGetRepeatedProvider(type);
            if (expected == null)
            {
                Assert.Null(provider);
            }
            else
            {
                Assert.NotNull(provider);
                var ser = provider.Serializer;
                Assert.NotNull(ser);
                Assert.AreEqual(expected, ser.GetType());
            }
        }

        [Test]
        // these are things we don't expect to support at any point
        [TestCase(typeof(int[,]))]
        [TestCase(typeof(List<int>[]))]
        [TestCase(typeof(List<int[]>))]
        [TestCase(typeof(int[][]))]
        [TestCase(typeof(List<List<int>>))]

        // these are things we'll probably light up later as "repeated",
        [TestCase(typeof(ArraySegment<int>))]
        [TestCase(typeof(Memory<int>))]
        [TestCase(typeof(ReadOnlyMemory<int>))]
        [TestCase(typeof(ReadOnlySequence<int>))]
        [TestCase(typeof(IMemoryOwner<int>))]

        // these are things we'll probably light up later as "bytes",
        [TestCase(typeof(ArraySegment<byte>))]
        [TestCase(typeof(Memory<byte>))]
        [TestCase(typeof(ReadOnlyMemory<byte>))]
        [TestCase(typeof(ReadOnlySequence<byte>))]
        [TestCase(typeof(IMemoryOwner<byte>))]

        public void NotSupportedScenarios(Type type)
        {
            var provider = RepeatedSerializers.TryGetRepeatedProvider(type);
            Assert.NotNull(provider);
            Assert.Throws<NotSupportedException>(() =>
            {
                _ = provider.Serializer;
            });
        }

        // spans a: can't be stored, so the concept of assigning them
        // is bad; and b: can't be used as generics, so can't be expressed
        // as ISerializer<Span<Foo>>, etc; all in all: just nope!
        [Test]
        [TestCase(typeof(Span<int>))]
        [TestCase(typeof(Span<byte>))]
        [TestCase(typeof(ReadOnlySpan<int>))]
        [TestCase(typeof(ReadOnlySpan<byte>))]
        public void SpansAreReallyReallyNotSupported(Type type)
        {
            var ex = Assert.Throws<NotSupportedException>(() =>
            {
                RepeatedSerializers.TryGetRepeatedProvider(type);
            });
            Assert.AreEqual("Serialization cannot work with [ReadOnly]Span<T>; [ReadOnly]Memory<T> may be enabled later", ex.Message);
        }

        class CustomEnumerable : IEnumerable<int>, ICollection<int>
        {
            private readonly List<int> items = new List<int>();
            IEnumerator<int> IEnumerable<int>.GetEnumerator() { return items.GetEnumerator(); }
            IEnumerator IEnumerable.GetEnumerator() { return items.GetEnumerator(); }
            public void Add(int value) { items.Add(value); }

            // need ICollection<int> for Add to work
            bool ICollection<int>.IsReadOnly => false;
            void ICollection<int>.Clear() => items.Clear();
            int ICollection<int>.Count => items.Count;
            bool ICollection<int>.Contains(int item) => items.Contains(item);
            bool ICollection<int>.Remove(int item) => items.Remove(item);
            void ICollection<int>.CopyTo(int[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);
        }

        public class ListGenericSubclass<T> : List<T> { }
        public class ListNonGenericSubclass : List<int> { }

        public class CustomNonGenericReadOnlyCollection : IReadOnlyCollection<string>
        {
            int IReadOnlyCollection<string>.Count => throw new NotImplementedException();
            IEnumerator<string> IEnumerable<string>.GetEnumerator() => throw new NotImplementedException();
            IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
        }
        public class CustomGenericReadOnlyCollection<T> : IReadOnlyCollection<T>
        {
            int IReadOnlyCollection<T>.Count => throw new NotImplementedException();
            IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotImplementedException();
            IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
        }

        public class CustomGenericCollection<T> : IList<T>
        {
            #region nope
            T IList<T>.this[int index] {
                get => throw new NotImplementedException();
                set => throw new NotImplementedException();
            }
            int ICollection<T>.Count => throw new NotImplementedException();
            bool ICollection<T>.IsReadOnly => throw new NotImplementedException();
            void ICollection<T>.Add(T item) => throw new NotImplementedException();
            void ICollection<T>.Clear() => throw new NotImplementedException();
            bool ICollection<T>.Contains(T item) => throw new NotImplementedException();
            void ICollection<T>.CopyTo(T[] array, int arrayIndex) => throw new NotImplementedException();
            IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotImplementedException();
            IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
            int IList<T>.IndexOf(T item) => throw new NotImplementedException();
            void IList<T>.Insert(int index, T item) => throw new NotImplementedException();
            bool ICollection<T>.Remove(T item) => throw new NotImplementedException();
            void IList<T>.RemoveAt(int index) => throw new NotImplementedException();
            #endregion
        }
        public class CustomNonGenericCollection : ICollection<bool>
        {
            #region nope
            int ICollection<bool>.Count => throw new NotImplementedException();
            bool ICollection<bool>.IsReadOnly => throw new NotImplementedException();
            void ICollection<bool>.Add(bool item) => throw new NotImplementedException();
            void ICollection<bool>.Clear() => throw new NotImplementedException();
            bool ICollection<bool>.Contains(bool item) => throw new NotImplementedException();
            void ICollection<bool>.CopyTo(bool[] array, int arrayIndex) => throw new NotImplementedException();
            IEnumerator<bool> IEnumerable<bool>.GetEnumerator() => throw new NotImplementedException();
            IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
            bool ICollection<bool>.Remove(bool item) => throw new NotImplementedException();
            #endregion
        }

        public class DoubleEnumerable : IEnumerable<string>, IEnumerable<int>
        {
            IEnumerator<string> IEnumerable<string>.GetEnumerator() => throw new NotImplementedException();
            IEnumerator<int> IEnumerable<int>.GetEnumerator() => throw new NotImplementedException();
            IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
        }

    }
}
