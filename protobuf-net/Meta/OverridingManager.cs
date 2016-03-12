// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using AltLinq; using System.Linq;
using AqlaSerializer;
using AqlaSerializer.Internal;
using AqlaSerializer.Meta.Mapping;
using AqlaSerializer.Serializers;
using AqlaSerializer.Settings;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#if FEAT_COMPILER
using IKVM.Reflection.Emit;
#endif
#else
using System.Reflection;
#if FEAT_COMPILER
using System.Reflection.Emit;

#endif
#endif

namespace AqlaSerializer.Meta
{
    /// <summary>
    /// Returns true if it's a final override and others should not be invoked
    /// </summary>
    public delegate bool FieldSettingsOverride(ValueMember member);

    /// <summary>
    /// Returns true if it's a final override and others should not be invoked
    /// </summary>
    public delegate bool TypeSettingsOverride(MetaType type);

    class OverridingManager
    {
        List<FieldSettingsOverride> _memberOverrides = new List<FieldSettingsOverride>();
        List<TypeSettingsOverride> _typeOverrides = new List<TypeSettingsOverride>();
        
        public void Add(FieldSettingsOverride @override)
        {
            if (@override == null) throw new ArgumentNullException(nameof(@override));
            _memberOverrides.Add(@override);
        }

        public void Add(TypeSettingsOverride @override)
        {
            if (@override == null) throw new ArgumentNullException(nameof(@override));
            _typeOverrides.Add(@override);
        }

        public void SubscribeTo(MetaType metaType)
        {
            metaType.FinalizingMemberSettings += MetaType_FinalizingMemberSettings;
            metaType.FinalizingOwnSettings += MetaType_FinalizingOwnSettings;
        }

        private void MetaType_FinalizingOwnSettings(object sender, MetaType.FinalizingOwnSettingsArgs e)
        {
            foreach (var @override in _typeOverrides)
            {
                if (@override.Invoke(e.Type)) break;
            }
        }

        private void MetaType_FinalizingMemberSettings(object sender, ValueMember.FinalizingSettingsArgs e)
        {
            foreach (var @override in _memberOverrides)
            {
                if (@override.Invoke(e.Member)) break;
            }
        }

        public OverridingManager CloneAsUnsubscribed()
        {
            return new OverridingManager()
            {
                _memberOverrides = new List<FieldSettingsOverride>(_memberOverrides),
                _typeOverrides = new List<TypeSettingsOverride>(_typeOverrides),
            };
        }
    }
}

#endif