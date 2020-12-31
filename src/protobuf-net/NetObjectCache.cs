// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using AltLinq; using System.Linq;
using AqlaSerializer.Meta;

namespace AqlaSerializer
{
    internal sealed class NetObjectCache : ICloneable
    {
        const int Root = 0;

        internal void ResetRoot()
        {
            
        }

        private MutableList _underlyingList;

        private MutableList List => _underlyingList ?? (_underlyingList = new MutableList(2048));

        public int LastNewKey => List.Count > 0 ? List.Count + 1 : Root;
        public object LastNewValue => List.Count > 0 ? List[List.Count - 1] : _rootObject;

        internal object GetKeyedObject(int key, bool allowMissing)
        {
            if (key-- == Root)
            {
                if (_rootObject == null) throw new ProtoException("No root object assigned");
                return _rootObject;
            }
            BasicList list = List;

            if (key < 0 || key >= list.Count)
            {
                Helpers.DebugWriteLine("Missing key: " + key);
                if (allowMissing) return null;
                throw new ProtoException("Internal error; a missing key occurred");
            }

            object tmp = list[key];
            if (tmp == null)
            {
                Helpers.DebugWriteLine("Missing key: " + key);
                if (allowMissing) return null;
                throw new ProtoException("A deferred key does not have a value yet (NoteObject call missed?)");
            }
            return tmp;
        }

        internal void SetKeyedObject(int key, object value)
        {
            SetKeyedObject(key, value, false);
        }

        internal void SetKeyedObject(int key, object value, bool lateSet)
        {
            if (key-- == Root)
            {
                if (_rootObject != null && ((object)_rootObject != (object)value)) throw new ProtoException("The root object cannot be reassigned");
                _rootObject = value;
            }
            else
            {
                MutableList list = List;

                while (key > list.Count)
                    list.Add(null); 
                
                if (key < list.Count)
                {
                    object oldVal = list[key];
                    if (oldVal == null || lateSet)
                    {
                        list[key] = value;
                    }
                    else if (!ReferenceEquals(oldVal, value) )
                    {
                        throw new ProtoException("Reference-tracked objects cannot change reference");
                    } // otherwise was the same; nothing to do
                }
                else if (key != list.Add(value))
                {
                    throw new ProtoException("Internal error; a key mismatch occurred");
                }
            }
        }

        private object _rootObject;
        internal int AddObjectKey(object value, out bool existing)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            if ((object)_rootObject==null)
            {
                _rootObject = value;
                existing = false;
                return Root;
            }

            if ((object)value == (object)_rootObject) // (object) here is no-op, but should be
            {                                        // preserved even if this was typed - needs ref-check
                existing = true;
                return Root;
            }

            string s = value as string;
            BasicList list = List;
            int index;


            if(s == null)
            {
#if CF || PORTABLE // CF has very limited proper object ref-tracking; so instead, we'll search it the hard way
                index = list.IndexOfReference(value);
#else
                if (_objectKeys == null) 
                {
                    _objectKeys = new System.Collections.Generic.Dictionary<object, int>(ReferenceComparer.Default);
                    index = -1;
                }
                else
                {
                    if (!_objectKeys.TryGetValue(value, out index)) index = -1;
                }
#endif
            }
            else
            {
                if (_stringKeys == null)
                {
                    _stringKeys = new System.Collections.Generic.Dictionary<string, int>();
                    index = -1;
                } 
                else
                {
                    if (!_stringKeys.TryGetValue(s, out index)) index = -1;
                }
            }

            if (!(existing = index >= 0))
            {
                index = list.Add(value);

                if (s == null)
                {
#if !CF && !PORTABLE // CF can't handle the object keys very well
                    _objectKeys.Add(value, index);
#endif
                }
                else
                {
                    _stringKeys.Add(s, index);
                }
            }
            return index + 1;
        }

        private int _trapStartIndex; // defaults to 0 - optimization for RegisterTrappedObject
                                    // to make it faster at seeking to find deferred-objects

        internal bool RegisterTrappedRootObject(object value)
        {
            if (_rootObject == null)
            {
                _rootObject = value;
                return true;
            }
            return false;
        }

        internal void RegisterTrappedObject(object value)
        {
            if (_rootObject == null)
            {
                _rootObject = value;
            }
            else
            {
                if(_underlyingList != null)
                {
                    for (int i = _trapStartIndex; i < _underlyingList.Count; i++)
                    {
                        _trapStartIndex = i + 1; // things never *become* null; whether or
                                                // not the next item is null, it will never
                                                // need to be checked again

                        if(_underlyingList[i] == null)
                        {
                            _underlyingList[i] = value;    
                            break;
                        }
                    }
                }
            }
        }

		private Dictionary<string, int> _stringKeys;
		
#if !CF && !PORTABLE // CF lacks the ability to get a robust reference-based hash-code, so we'll do it the harder way instead
		private System.Collections.Generic.Dictionary<object, int> _objectKeys;
        private sealed class ReferenceComparer : System.Collections.Generic.IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Default = new ReferenceComparer();
            private ReferenceComparer() {}

            bool System.Collections.Generic.IEqualityComparer<object>.Equals(object x, object y)
            {
                return x == y; // ref equality
            }

            int System.Collections.Generic.IEqualityComparer<object>.GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
#endif

        internal void Clear()
        {
            _trapStartIndex = 0;
            _rootObject = null;
            _underlyingList?.Clear();
            _stringKeys?.Clear();
#if !CF && !PORTABLE
            _objectKeys?.Clear();
#endif
        }

        public NetObjectCache Clone()
        {
            var c = (NetObjectCache)MemberwiseClone();
            if (_stringKeys != null)
                c._stringKeys = new Dictionary<string, int>(_stringKeys);
#if !CF && !PORTABLE
            if (_objectKeys != null)
                c._objectKeys = new Dictionary<object, int>(_objectKeys);
#endif
            if (_underlyingList != null)
                c._underlyingList = new MutableList(_underlyingList.Cast<object>());
            return c;
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
    }
}
