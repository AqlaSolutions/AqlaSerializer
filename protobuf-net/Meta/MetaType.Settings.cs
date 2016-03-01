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

        private string name;

        /// <summary>
        /// Gets or sets the name of this contract.
        /// </summary>
        public string Name
        {
            get { return !string.IsNullOrEmpty(name) ? name : Type.Name; }
            set
            {
                ThrowIfFrozen();
                name = value;
            }
        }

        /// <summary>
        /// Gets or sets whether the type should use a parameterless constructor (the default),
        /// or whether the type should skip the constructor completely. This option is not supported
        /// on compact-framework.
        /// </summary>
        public bool UseConstructor
        { // negated to have defaults as flat zero
            get { return !HasFlag(OPTIONS_SkipConstructor); }
            set { SetFlag(OPTIONS_SkipConstructor, !value, true); }
        }
        /// <summary>
        /// The concrete type to create when a new instance of this type is needed; this may be useful when dealing
        /// with dynamic proxies, or with interface-based APIs; for collections this is a default collection type.
        /// </summary>
        public Type ConstructType
        {
            get { return constructType; }
            set
            {
                ThrowIfFrozen();

                if (value != null && !Helpers.IsAssignableFrom(this.Type, value))
                    throw new ArgumentException("Specified type " + value.Name + " is not assignable to " + this.Type.Name);
                constructType = value;
            }
        }
        private Type constructType;
        /// <summary>
        /// Gets or sets a value indicating that an enum should be treated directly as an int/short/etc, rather
        /// than enforcing .proto enum rules. This is useful *in particul* for [Flags] enums.
        /// </summary>
        public bool EnumPassthru
        {
            get { return HasFlag(OPTIONS_EnumPassThru); }
            set { SetFlag(OPTIONS_EnumPassThru, value, true); }
        }

        /// <summary>
        /// Gets or sets a value indicating that this type should NOT be treated as a list, even if it has
        /// familiar list-like characteristics (enumerable, add, etc)
        /// </summary>
        public bool IgnoreListHandling
        {
            get { return HasFlag(OPTIONS_IgnoreListHandling); }
            set
            {
                if (value && Type.IsArray)
                    throw new InvalidOperationException("Can't disable list handling for arrays");
                SetFlag(OPTIONS_IgnoreListHandling, value, true);
            }
        }

        /// <summary>
        /// Should this type be treated as a reference by default FOR MISSING TYPE MEMBERS ONLY?
        /// </summary>
        public bool AsReferenceDefault
        {
            get { return DefaultAutoAddStrategy.GetAsReferenceDefault(_settingsValue.Member, Type); }
            set
            {
                if (value)
                {
                    _settingsValue.Member.EnhancedFormat = true;
                    if (!AsReferenceDefault) _settingsValue.Member.EnhancedWriteMode = EnhancedMode.Reference;
                }
                else
                {
                    _settingsValue.Member.EnhancedFormat = false;
                }
            }
        }

        public bool PrefixLength
        {
            get { return _settingsValue.PrefixLength.GetValueOrDefault(true); }
            set { _settingsValue.PrefixLength = value; }
        }

        public BinaryDataFormat CollectionDataFormat
        {
            get { return _settingsValue.Member.ContentBinaryFormatHint.GetValueOrDefault(); }
            set { _settingsValue.Member.ContentBinaryFormatHint = value; }
        }

        internal bool IsAutoTuple
        {
            get { return HasFlag(OPTIONS_AutoTuple); }
            set { SetFlag(OPTIONS_AutoTuple, value, true); }
        }

    }
}
#endif