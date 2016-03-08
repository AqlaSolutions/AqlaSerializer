// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
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
    partial class MetaType
    {
        ValueSerializationSettings _rootNestedVs = new ValueSerializationSettings();

        /// <summary>
        /// These settings are only applicable for collection types when they are serialized not as a member (root or LateReference)
        /// </summary>
        public MemberLevelSettingsValue GetNestedSettingsCopyWhenRoot(int level)
        {
            if (level == 0) throw new ArgumentOutOfRangeException(nameof(level), "Zero level settings should be obtained through DefaultX properties on MetaType");
            return _rootNestedVs.GetSettingsCopy(level).Basic;
        }
        
        /// <summary>
        /// These settings are only applicable for collection types when they are serialized not as a member (root or LateReference)
        /// </summary>
        public void SetNestedSettingsWhenRoot(MemberLevelSettingsValue value, int level)
        {
            if (level == 0) throw new ArgumentOutOfRangeException(nameof(level), "Zero level settings should be specified through DefaultX properties on MetaType");
            ThrowIfFrozen();
            Helpers.MemoryBarrier();
            _rootNestedVs.SetSettings(value, level);
        }

        /// <summary>
        /// These settings are only applicable for collection types when they are serialized not as a member (root or LateReference)
        /// </summary>
        public void SetNestedSettingsWhenRoot(MemberLevelSettingsChangeDelegate func, int level)
        {
            var v = new MemberLevelSettingsRef(GetNestedSettingsCopyWhenRoot(level));
            func(v);
            SetNestedSettingsWhenRoot(v.V, level);
        }

        TypeSettingsValue _settingsValue;
        internal TypeSettingsValue SettingsValue { get { return _settingsValue; } set { _settingsValue = value; } }

        internal class FinalizingOwnSettingsArgs : EventArgs
        {
            public MetaType Type { get;  }

            public FinalizingOwnSettingsArgs(MetaType type)
            {
                Type = type;
            }
        }

        internal event EventHandler<FinalizingOwnSettingsArgs> FinalizingOwnSettings;
        internal event EventHandler<ValueMember.FinalizingSettingsArgs> FinalizingMemberSettings;

        bool _finalizedSettings;

        internal void FinalizeSettingsValue()
        {
            if (_finalizedSettings) return;

            int opaqueToken = 0;
            try
            {
                _model.TakeLock(ref opaqueToken);

                if (_finalizedSettings) return;

                ThrowIfInvalidSettings(_settingsValue);
                FinalizingOwnSettings?.Invoke(this, new FinalizingOwnSettingsArgs(this));
                ThrowIfInvalidSettings(_settingsValue);

                MemberLevelSettingsValue m = _settingsValue.Member;

                if (_settingsValue.EnumPassthru == null && Helpers.IsEnum(Type))
                {
#if WINRT
                    _settingsValue.EnumPassthru = _typeInfo.IsDefined(typeof(FlagsAttribute), false);
#else
                    _settingsValue.EnumPassthru = Type.IsDefined(_model.MapType(typeof(FlagsAttribute)), false);
#endif
                }
                if (_settingsValue.IgnoreListHandling)
                {
                    m.Collection.ItemType = null;
                    m.Collection.Format = CollectionFormat.NotSpecified;
                    m.Collection.PackedWireTypeForRead = null;
                }

                m.Collection.ConcreteType = _settingsValue.ConstructType;

                if (_settingsValue.PrefixLength == null && !IsSimpleValue)
                    _settingsValue.PrefixLength = true;

                _settingsValue.Member = m;

                _finalizedSettings = true;
                IsFrozen = true;
            }
            finally
            {
                _model.ReleaseLock(opaqueToken);
            }
        }

        void ThrowIfInvalidSettings(TypeSettingsValue sv)
        {
            if (sv.IgnoreListHandling && Type.IsArray)
                throw new InvalidOperationException("Can't disable list handling for arrays");

            if (sv.ConstructType != null && !Helpers.IsAssignableFrom(this.Type, sv.ConstructType))
                throw new ArgumentException("Specified construct type " + sv.ConstructType.Name + " is not assignable to " + this.Type.Name);

            if (sv.Member.Collection.ConcreteType != null && !Helpers.IsAssignableFrom(this.Type, sv.Member.Collection.ConcreteType))
                throw new ArgumentException("Specified collection concrete type " + sv.Member.Collection.ConcreteType.Name + " is not assignable to " + this.Type.Name);
        }
        
        /// <summary>
        /// Gets or sets the name of this contract.
        /// </summary>
        public string Name
        {
            get { return !string.IsNullOrEmpty(_settingsValue.Name) ? _settingsValue.Name : Type.Name; }
            set
            {
                ChangeSettings(
                    sv =>
                    {
                        sv.Name = value;
                        return sv;
                    });
            }
        }

        /// <summary>
        /// Gets or sets a value indicating that an enum should be treated directly as an int/short/etc, rather
        /// than enforcing .proto enum rules. This is useful *in particul* for [Flags] enums.
        /// </summary>
        public bool? EnumPassthru
        {
            get { return _settingsValue.EnumPassthru; }
            set
            {
                ChangeSettings(
                    sv =>
                        {
                            sv.EnumPassthru = value;
                            return sv;
                        });
            }
        }
        
        /// <summary>
        /// Maximum allowed array allocation length counting all 
        /// </summary>
        public int? ArrayLengthReadLimit
        {
            get { return _settingsValue.Member.Collection.ArrayLengthReadLimit; }
            set
            {
                ChangeSettings(
                    sv =>
                        {
                            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                            sv.Member.Collection.ArrayLengthReadLimit = value;
                            return sv;
                        });
            }
        }

        /// <summary>
        /// Gets or sets whether the type should use a parameterless constructor (the default),
        /// or whether the type should skip the constructor completely. This option is not supported
        /// on compact-framework.
        /// </summary>
        public bool UseConstructor
        { // negated to have defaults as flat zero
            get { return !_settingsValue.SkipConstructor; }
            set
            {
                ChangeSettings(
                    sv =>
                    {
                        sv.SkipConstructor = !value;
                        return sv;
                    });
            }
        }

        public bool? PrefixLength
        {
            get
            {
                return _settingsValue.PrefixLength;
            }
            set
            {
                ChangeSettings(
                    sv =>
                    {
                        sv.PrefixLength = value;
                        return sv;
                    });
            }
        }

        /// <summary>
        /// The concrete type to create when a new instance of this type is needed; this may be useful when dealing
        /// with dynamic proxies, or with interface-based APIs; for collections this is a default collection type.
        /// </summary>
        public Type ConstructType
        {
            get { return _settingsValue.ConstructType; }
            set
            {
                ChangeSettings(
                    sv =>
                    {
                        // will set also member.Collection.ConcreteType in InitSerializers
                        sv.ConstructType = value;
                        return sv;
                    });
            }
        }

        public ValueFormat DefaultFormat
        {
            get
            {
                return _settingsValue.Member.Format;
            }
            set
            {
                ChangeSettings(
                    sv =>
                    {
                        sv.Member.Format = value;
                        return sv;
                    });
            }
        }
        
        public BinaryDataFormat? ContentBinaryFormatHint
        {
            get { return _settingsValue.Member.ContentBinaryFormatHint; }
            set
            {
                ChangeSettings(
                    sv =>
                    {
                        sv.Member.ContentBinaryFormatHint = value;
                        return sv;
                    });
            }
        }

        public CollectionFormat CollectionFormat
        {
            get
            {
                return _settingsValue.Member.Collection.Format;
            }
            set
            {
                ChangeSettings(
                    sv =>
                    {
                        sv.Member.Collection.Format = value;
                        return sv;
                    });
            }
        }

        public Type CollectionItemType
        {
            get
            {
                return _settingsValue.Member.Collection.ItemType;
            }
            set
            {
                ChangeSettings(
                    sv =>
                    {
                        sv.Member.Collection.ItemType = value;
                        return sv;
                    });
            }
        }

        /// <summary>
        /// Gets or sets a value indicating that this type should NOT be treated as a list, even if it has
        /// familiar list-like characteristics (enumerable, add, etc)
        /// </summary>
        public bool IgnoreListHandling
        {
            get { return _settingsValue.IgnoreListHandling; }
            set
            {
                ChangeSettings(
                    sv =>
                        {
                            sv.IgnoreListHandling = value;
                            return sv;
                        });
            }
        }

        /// <summary>
        /// Should this type be treated as a reference by default
        /// </summary>
        [Obsolete("Use DefaultFormat")]
        public bool AsReferenceDefault
        {
            get
            {
                MemberLevelSettingsValue m = _settingsValue.Member;
                return m.Format == ValueFormat.Reference || m.Format == ValueFormat.LateReference ||
                       (m.Format == ValueFormat.NotSpecified && ValueSerializerBuilder.CanTypeBeAsReference(Type));
            }
            set
            {
                ChangeSettings(
                    sv =>
                        {
                            if (value)
                            {
                                if (sv.Member.Format != ValueFormat.LateReference)
                                    sv.Member.Format = ValueFormat.Reference;
                            }
                            else
                            {
                                sv.Member.Format = ValueFormat.Compact;
                                sv.Member.WriteAsDynamicType = false;
                            }

                            return sv;
                        });
            }
        }

        internal bool IsAutoTuple
        {
            get { return _settingsValue.IsAutoTuple; }
            set
            {
                ChangeSettings(
                    sv =>
                        {
                            sv.IsAutoTuple = value;
                            return sv;
                        });
            }
        }

        void ChangeSettings(Func<TypeSettingsValue, TypeSettingsValue> setter)
        {
            ThrowIfFrozen();
            var sv = setter(_settingsValue);
            ThrowIfInvalidSettings(sv);
            Helpers.MemoryBarrier();
            _settingsValue = sv;
        }
    }
}

#endif