// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace AqlaSerializer.Meta
{

    internal sealed class MutableList : BasicList
    {
        public MutableList()
        {
        }

        public MutableList(IEnumerable<object> enumerable)
            : base(enumerable)
        {
        }

        /*  Like BasicList, but allows existing values to be changed
         */ 
        public new object this[int index] {
            get { return Head[index]; }
            set { Head[index] = value; }
        }
        public void RemoveLast()
        {
            Head.RemoveLastWithMutate();
        }

        public void Clear()
        {
            Head.Clear();
        }

        protected override void HandleIListClear()
        {
            Clear();
        }
    }
    internal class BasicList : IEnumerable, IList
    {
        /* Requirements:
         *   - Fast access by index
         *   - Immutable in the tail, so a node can be read (iterated) without locking
         *   - Lock-free tail handling must match the memory mode; struct for Node
         *     wouldn't work as "read" would not be atomic
         *   - Only operation required is append, but this shouldn't go out of its
         *     way to be inefficient
         *   - Assume that the caller is handling thread-safety (to co-ordinate with
         *     other code); no attempt to be thread-safe
         *   - Assume that the data is private; internal data structure is allowed to
         *     be mutable (i.e. array is fine as long as we don't screw it up)
         */

        public BasicList()
        {
        }

        public BasicList(IEnumerable<object> enumerable)
        {
            foreach (var el in enumerable) Add(el);
        }

        private static readonly Node Nil = new Node(null, 0);
        public void CopyTo(Array array, int offset)
        {
            Head.CopyTo(array, offset);
        }
        
        public void CopyTo(Array array, int sourceStart, int destinationStart, int length)
        {
            Head.CopyTo(array, sourceStart, destinationStart, length);
        }

        protected Node Head = Nil;
        public int Add(object value)
        {
            return (Head = Head.Append(value)).Length - 1;
        }

        bool IList.Contains(object value)
        {
            return Contains(value);
        }

        protected virtual void HandleIListClear()
        {
            throw new NotSupportedException();
        }

        void IList.Clear()
        {
            HandleIListClear();
        }

        int IList.IndexOf(object value)
        {
            return IndexOf((x, ctx) => Object.Equals(x, value), null);
        }

        void IList.Insert(int index, object value)
        {
            throw new NotSupportedException();
        }

        void IList.Remove(object value)
        {
            throw new NotSupportedException();
        }

        void IList.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        object IList.this[int index] { get { return this[index]; } set { throw new NotSupportedException(); } }

        bool IList.IsReadOnly => false;

        bool IList.IsFixedSize => false;

        public object this[int index] => Head[index];
        //public object TryGet(int index)
        //{
        //    return head.TryGet(index);
        //}
        public void Trim() { Head = Head.Trim(); }
        public int Count => Head.Length;

        object ICollection.SyncRoot { get; } = new object();

        bool ICollection.IsSynchronized => false;

        IEnumerator IEnumerable.GetEnumerator() { return new NodeEnumerator(Head); }
        public NodeEnumerator GetEnumerator() { return new NodeEnumerator(Head); }

        public struct NodeEnumerator : IEnumerator
        {
            private int _position;
            private readonly Node _node;
            internal NodeEnumerator(Node node)
            {
                this._position = -1;
                this._node = node;
            }
            void IEnumerator.Reset() { _position = -1; }
            public object Current => _node[_position];

            public bool MoveNext()
            {
                int len = _node.Length;
                return (_position <= len) && (++_position < len);
            }
        }
        internal sealed class Node
        {
            public object this[int index]
            {
                get {
                    if (index >= 0 && index < Length)
                    {
                        return _data[index];
                    }
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                set
                {
                    if (index >= 0 && index < Length)
                    {
                        _data[index] = value;
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }
                }
            }
            //public object TryGet(int index)
            //{
            //    return (index >= 0 && index < length) ? data[index] : null;
            //}
            private readonly object[] _data;

            public int Length { get; set; }

            internal Node(object[] data, int length)
            {
                Helpers.DebugAssert((data == null && length == 0) ||
                    (data != null && length > 0 && length <= data.Length));
                this._data = data;

                this.Length = length;
            }
            public void RemoveLastWithMutate()
            {
                if (Length == 0) throw new InvalidOperationException();
                Length -= 1;
            }
            public Node Append(object value)
            {
                object[] newData;
                int newLength = Length + 1;
                if (_data == null)
                {
                    newData = new object[10];
                }
                else if (Length == _data.Length)
                {
                    newData = new object[_data.Length * 2];
                    Array.Copy(_data, newData, Length);
                } else
                {
                    newData = _data;
                }
                newData[Length] = value;
                return new Node(newData, newLength);
            }
            public Node Trim()
            {
                if (Length == 0 || Length == _data.Length) return this;
                object[] newData = new object[Length];
                Array.Copy(_data, newData, Length);
                return new Node(newData, Length);
            }

            internal int IndexOfString(string value)
            {
                for (int i = 0; i < Length; i++)
                {
                    if ((string)value == (string)_data[i]) return i;
                }
                return -1;
            }
            internal int IndexOfReference(object instance)
            {
                for (int i = 0; i < Length; i++)
                {
                    if ((object)instance == (object)_data[i]) return i;
                } // ^^^ (object) above should be preserved, even if this was typed; needs
                  // to be a reference check
                return -1;
            }
            internal bool HasReferences(object instance, int required)
            {
                int count = 0;
                for (int i = 0; i < Length; i++)
                {
                    if ((object)instance == (object)_data[i])
                    {
                        if (++count >= required) return true;
                    }
                } // ^^^ (object) above should be preserved, even if this was typed; needs
                  // to be a reference check
                return required <= 0;
            }
            internal int IndexOf(MatchPredicate predicate, object ctx)
            {
                for (int i = 0; i < Length; i++)
                {
                    if (predicate(_data[i], ctx)) return i;
                }
                return -1;
            }

            internal void CopyTo(Array array, int offset)
            {
                CopyTo(array, 0, offset, Length);
            }

            internal void CopyTo(Array array, int sourceStart, int destinationStart, int length)
            {
                if (_data == null)
                {
                    if (sourceStart > 0 || length < 0)
                        throw new ArgumentOutOfRangeException();
                    return;
                }
                Helpers.MemoryBarrier();
                Array.Copy(_data, sourceStart, array, destinationStart, length);
            }

            internal void Clear()
            {
                if(_data != null)
                {
                    Array.Clear(_data, 0, _data.Length);
                }
                Length = 0;
            }
        }

        internal int IndexOf(MatchPredicate predicate, object ctx)
        {
            return Head.IndexOf(predicate, ctx);
        }
        internal int IndexOfString(string value)
        {
            return Head.IndexOfString(value);
        }
        internal int IndexOfReference(object instance)
        {
            return Head.IndexOfReference(instance);
        }

        internal bool HasReferences(object instance, int max)
        {
            return Head.HasReferences(instance, max);
        }

        internal delegate bool MatchPredicate(object value, object ctx);

        internal bool Contains(object value)
        {
            foreach (object obj in this)
            {
                if (object.Equals(obj, value)) return true;
            }
            return false;
        }
        internal sealed class Group
        {
            public readonly int First;
            public readonly BasicList Items;
            public Group(int first)
            {
                this.First = first;
                this.Items = new BasicList();
            }
        }
        internal static BasicList GetContiguousGroups(int[] keys, object[] values)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Length < keys.Length) throw new ArgumentException("Not all keys are covered by values", nameof(values));
            BasicList outer = new BasicList();
            Group group = null;
            for (int i = 0; i < keys.Length; i++)
            {
                if (i == 0 || keys[i] != keys[i - 1]) { group = null; }
                if (group == null)
                {
                    group = new Group(keys[i]);
                    outer.Add(group);
                }
                group.Items.Add(values[i]);
            }
            return outer;
        }
    }


}