using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AltLinq;

namespace AqlaSerializer
{
    internal class NetObjectKeyPositionsList
    {
        List<int> _keyToPosition = new List<int>();

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

        int _exportKnownCount;
        int _importKnownCount;

        public int[] ExportNew()
        {
            var r = _keyToPosition.Skip(_exportKnownCount).ToArray();
            _exportKnownCount += r.Length;
            return r;
        }

        public void ImportAppending(int[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
                SetPosition(_importKnownCount++, arr[i]);
        }

        public NetObjectKeyPositionsList()
        {
            Reset();
        }

        public void Reset()
        {
            _keyToPosition.Clear();
            _importKnownCount = _exportKnownCount = 1;
        }

        static void ThrowNotFound(int key)
        {
            throw new KeyNotFoundException(nameof(NetObjectKeyPositionsList) + " can't find a key: " + key + ", try to set TypeModel.EnableVersioningSeeking = true");
        }

        public NetObjectKeyPositionsList Clone()
        {
            var r = (NetObjectKeyPositionsList)MemberwiseClone();
            r._keyToPosition = new List<int>(_keyToPosition);
            return r;
        }
    }
}