// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections.Generic;
using System.Diagnostics;
using AqlaSerializer.Serializers;
using System.Globalization;
using AltLinq;
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

        public int MinSpecifiedLevelsCount => _levels.Count;

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

        internal void SetForAllLevels(Func<LevelValue, LevelValue> setter)
        {
            if (DefaultLevel != null)
                DefaultLevel = setter(DefaultLevel.Value);
            for (int i = 0; i < _levels.Count; i++)
            {
                var level = _levels[i];
                if (level == null) continue;
                _levels[i] = setter(level.Value);
            }
        }

        internal void SetForAllLevels(Func<MemberLevelSettingsValue, MemberLevelSettingsValue> setter)
        {
            if (DefaultLevel != null)
            {
                var v = DefaultLevel.Value;
                v.Basic = setter(DefaultLevel.Value.Basic);
                DefaultLevel = v;
            }
            for (int i = 0; i < _levels.Count; i++)
            {
                var level = _levels[i];
                if (level == null) continue;
                var v = level.Value;
                v.Basic = setter(level.Value.Basic);
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