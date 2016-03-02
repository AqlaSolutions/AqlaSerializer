// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Threading;
using AltLinq;
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
        TypeSettingsValue _settingsValue;
        internal TypeSettingsValue SettingsValue { get { return _settingsValue; } set { _settingsValue = value; } }
        bool _finalizedSettings;

        internal void FinalizeSettingsValue()
        {
            if (_finalizedSettings) return;
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

        public bool? DefaultEnhancedFormat
        {
            get
            {
                return _settingsValue.Member.EnhancedFormat;
            }
            set
            {
                ChangeSettings(
                    sv =>
                    {
                        sv.Member.EnhancedFormat = value;
                        return sv;
                    });
            }
        }

        public EnhancedMode DefaultEnhancedWriteAs
        {
            get
            {
                return _settingsValue.Member.EnhancedWriteMode;
            }
            set
            {
                ChangeSettings(
                    sv =>
                    {
                        sv.Member.EnhancedWriteMode = value;
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
        [Obsolete("Use DefaultEnhancedFormat and DefaultEnhancedWriteAs")]
        public bool AsReferenceDefault
        {
            get
            {
                MemberLevelSettingsValue m = _settingsValue.Member;
                return m.EnhancedFormat != false && m.EnhancedWriteMode != EnhancedMode.Minimal &&
                       (m.EnhancedWriteMode != EnhancedMode.NotSpecified || ValueSerializerBuilder.CanTypeBeAsReference(Type));
            }
            set
            {
                ChangeSettings(
                    sv =>
                        {
                            if (value)
                            {
                                sv.Member.EnhancedFormat = true;
                                if (!AsReferenceDefault) sv.Member.EnhancedWriteMode = EnhancedMode.Reference;
                            }
                            else
                            {
                                sv.Member.EnhancedFormat = false;
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
            _settingsValue = sv;
        }
    }
}

#endif