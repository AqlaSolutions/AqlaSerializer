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
        List<MemberLevelSettingsValue?> _levels;
        public object DefaultValue { get; set; }
        public MemberLevelSettingsValue? DefaultLevel { get; set; }

        public ValueSerializationSettings()
        {
            _levels = new List<MemberLevelSettingsValue?>();
        }

        public ValueSerializationSettings(IEnumerable<MemberLevelSettingsValue?> levels, MemberLevelSettingsValue defaultLevel)
        {
            _levels = new List<MemberLevelSettingsValue?>(levels ?? new MemberLevelSettingsValue?[0]);
            DefaultLevel = defaultLevel;
        }

        public bool HasSettingsSpecified(int level)
        {
            return _levels.Count > level && _levels[level] != null;
        }

        public int MinSpecifiedLevelsCount => _levels.Count;

        public MemberLevelSettingsValue GetSettingsCopy(int level)
        {
            return (_levels.Count <= level ? null : _levels[level]) ?? DefaultLevel.GetValueOrDefault();
        }

        public void SetSettings(MemberLevelSettingsValue value, int level)
        {
            while (level >= _levels.Count)
                _levels.Add(null);
            _levels[level] = value;
        }

        internal void SetForAllLevels(Func<MemberLevelSettingsValue, MemberLevelSettingsValue> setter)
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

        public ValueSerializationSettings Clone()
        {
            var c = (ValueSerializationSettings)MemberwiseClone();
            c._levels = new List<MemberLevelSettingsValue?>(c._levels);
            return c;
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
    }
}

#endif