using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AqlaSerializer
{
    internal class LateReferencesCache : ICloneable
    {
        public struct LateReference
        {
            public readonly object Value;
            public readonly int TypeKey;
            public readonly int ReferenceKey;

            public LateReference(int typeKey, object value, int referenceKey)
            {
                TypeKey = typeKey;
                Value = value;
                ReferenceKey = referenceKey;
            }
        }

        List<LateReference> _lateReferences = new List<LateReference>();
        int _lateReferenceCurrentIndex;

        public void AddLateReference(LateReference v)
        {
            Debug.Assert(v.Value != null);
            _lateReferences.Add(v);
        }

        public LateReference? TryGetNextLateReference()
        {
            if (_lateReferenceCurrentIndex == _lateReferences.Count)
            {
                return null;
            }
            return _lateReferences[_lateReferenceCurrentIndex++];
        }

        public void Reset()
        {
            _lateReferenceCurrentIndex = 0;
            _lateReferences.Clear();
        }

        public LateReferencesCache Clone()
        {
            var c = (LateReferencesCache)MemberwiseClone();
            c._lateReferences = new List<LateReference>(_lateReferences);
            return c;
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
    }
}