using System.Collections.Generic;
using System.Diagnostics;

namespace AqlaSerializer
{
    internal class LateReferencesCache
    {
        struct LateReference
        {
            public readonly object Value;
            public readonly int TypeKey;

            public LateReference(int typeKey, object value)
            {
                TypeKey = typeKey;
                Value = value;
            }
        }

        readonly List<LateReference> _lateReferences = new List<LateReference>();
        int _lateReferenceCurrentIndex;

        public void AddLateReference(int typeKey, object value)
        {
            Debug.Assert(value != null);
            _lateReferences.Add(new LateReference(typeKey, value));
        }

        public bool TryGetNextLateReference(out int typeKey, out object value)
        {
            if (_lateReferenceCurrentIndex == _lateReferences.Count)
            {
                typeKey = 0;
                value = null;
                return false;
            }
            var r = _lateReferences[_lateReferenceCurrentIndex];
            typeKey = r.TypeKey;
            value = r.Value;
            return true;
        }

        public void Reset()
        {
            _lateReferenceCurrentIndex = 0;
            _lateReferences.Clear();
        }
    }
}