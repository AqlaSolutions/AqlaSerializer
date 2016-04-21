using System;
using System.Collections;
using System.Collections.Generic;

namespace AqlaSerializer
{
    internal class NetObjectKeyPositionsList
    {
        readonly List<int> _keyToPosition = new List<int>();

        public void SetPosition(int key, int position)
        {
            if (position < 0 || (key > 0 && position == 0)) throw new ArgumentOutOfRangeException(nameof(position));
            while (_keyToPosition.Count - 1 < key)
                _keyToPosition.Add(0);
            _keyToPosition[key] = position;
        }

        public int GetPosition(int key)
        {
            if (_keyToPosition.Count - 1 < key) ThrowNotFound(key);
            var r = _keyToPosition[key];
            if (r == 0 && key > 0) ThrowNotFound(key);
            return r;
        }

        public int[] ToKeyToPositionArray()
        {
            return _keyToPosition.ToArray();
        }

        public void Reset()
        {
            _keyToPosition.Clear();
        }

        static void ThrowNotFound(int key)
        {
            throw new KeyNotFoundException(nameof(NetObjectKeyPositionsList) + " can't find a key: " + key);
        }
    }
}