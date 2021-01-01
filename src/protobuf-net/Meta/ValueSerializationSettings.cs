// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections.Generic;
using System.Diagnostics;
using AqlaSerializer.Serializers;
using System.Globalization;
using AltLinq; using System.Linq;
using AqlaSerializer.Internal;
using AqlaSerializer.Settings;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;

#endif

namespace AqlaSerializer.Meta
{
    public class ValueSerializationSettings : ICloneable
    {
        List<LevelValue?> _levels;

        public struct LevelValue
        {
            public MemberLevelSettingsValue Basic;
            public bool IsNotAssignable;

            public LevelValue(MemberLevelSettingsValue basic)
                : this()
            {
                Basic = basic;
            }

            public override string ToString()
            {
                return Basic.ToString();
            }
        }

        public object DefaultValue { get; set; }
        public LevelValue? DefaultLevel { get; set; }

        public LevelValue[] ExistingLevels => _levels.Select(x => x.GetValueOrDefault()).ToArray();

        public ValueSerializationSettings()
        {
            _levels = new List<LevelValue?>();
        }

        public ValueSerializationSettings(IEnumerable<MemberLevelSettingsValue?> levels, MemberLevelSettingsValue defaultLevel)
        {
            _levels = new List<LevelValue?>(levels.Select(x => x != null ? new LevelValue(x.Value) : (LevelValue?)null) ?? new LevelValue?[0]);
            DefaultLevel = new LevelValue(defaultLevel);
        }

        public bool HasSettingsSpecified(int level)
        {
            return _levels.Count > level && _levels[level] != null;
        }

        public int MaxSpecifiedNestedLevel => _levels.Select((x, i) => new { Level = x, i }).LastOrDefault(x => x.Level != null)?.i ?? -1;

        public LevelValue GetSettingsCopy(int level)
        {
            return (_levels.Count <= level ? null : _levels[level]) ?? DefaultLevel.GetValueOrDefault();
        }

        public void SetSettings(LevelValue value, int level)
        {
            while (level >= _levels.Count)
                _levels.Add(null);
            _levels[level] = value;
        }

        public void SetSettings(MemberLevelSettingsValue value, int level)
        {
            var s = GetSettingsCopy(level);
            s.Basic = value;
            SetSettings(s, level);
        }

        internal void SetForAllLevels(Func<LevelValue, LevelValue> setter, bool frozen)
        {
            if (DefaultLevel != null)
            {
                var newLevel = setter(DefaultLevel.Value);
                if (frozen && !newLevel.Equals(DefaultLevel)) throw new InvalidOperationException("The type cannot be changed once a serializer has been generated");
                DefaultLevel = newLevel;
            }

            for (int i = 0; i < _levels.Count; i++)
            {
                var level = _levels[i];
                if (level == null) continue;
                LevelValue newValue = setter(level.Value);
                if (frozen && !newValue.Equals(level)) throw new InvalidOperationException("The type cannot be changed once a serializer has been generated");
                _levels[i] = newValue;
            }
        }

        internal void SetForAllLevels(Func<MemberLevelSettingsValue, MemberLevelSettingsValue> setter, bool frozen)
        {
            if (DefaultLevel != null)
            {
                var v = DefaultLevel.Value;
                MemberLevelSettingsValue newValue = setter(DefaultLevel.Value.Basic);
                if (frozen && !newValue.Equals(v.Basic)) throw new InvalidOperationException("The type cannot be changed once a serializer has been generated");
                v.Basic = newValue;
                DefaultLevel = v;
            }
            for (int i = 0; i < _levels.Count; i++)
            {
                var level = _levels[i];
                if (level == null) continue;
                var v = level.Value;
                MemberLevelSettingsValue newValue = setter(level.Value.Basic);
                if (frozen && !newValue.Equals(level.Value.Basic)) throw new InvalidOperationException("The type cannot be changed once a serializer has been generated");
                v.Basic = newValue;

                _levels[i] = v;
            }
        }

        public ValueSerializationSettings Clone()
        {
            var c = (ValueSerializationSettings)MemberwiseClone();
            c._levels = new List<LevelValue?>(c._levels);
            return c;
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        public override string ToString()
        {
            return "ValueSettings, L0=" + (HasSettingsSpecified(0) ? GetSettingsCopy(0).ToString() : "null");
        }
    }
}

#endif