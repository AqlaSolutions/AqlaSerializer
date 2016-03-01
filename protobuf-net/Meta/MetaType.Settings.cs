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

        internal TypeSettingsValue MakeFinalizedSettingsValue()
        {
            return FinalizeSettingsValue(_settingsValue);
        }

        /// <summary>
        /// Gets or sets the name of this contract.
        /// </summary>
        public string Name
        {
            get { return !string.IsNullOrEmpty(_settingsValue.Name) ? _settingsValue.Name : Type.Name; }
            set
            {
                ThrowIfFrozen();
                _settingsValue.Name = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating that an enum should be treated directly as an int/short/etc, rather
        /// than enforcing .proto enum rules. This is useful *in particul* for [Flags] enums.
        /// </summary>
        public bool? EnumPassthru
        {
            get { return _settingsValue.EnumPassthru; }
            set { _settingsValue.EnumPassthru = value; }
        }

        /// <summary>
        /// Gets or sets whether the type should use a parameterless constructor (the default),
        /// or whether the type should skip the constructor completely. This option is not supported
        /// on compact-framework.
        /// </summary>
        public bool UseConstructor
        { // negated to have defaults as flat zero
            get { return !_settingsValue.SkipConstructor; }
            set { _settingsValue.SkipConstructor = !value; }
        }

        public bool? PrefixLength { get { return _settingsValue.PrefixLength; } set { _settingsValue.PrefixLength = value; } }

        /// <summary>
        /// The concrete type to create when a new instance of this type is needed; this may be useful when dealing
        /// with dynamic proxies, or with interface-based APIs; for collections this is a default collection type.
        /// </summary>
        public Type ConstructType
        {
            get { return _settingsValue.ConstructType; }
            set
            {
                ThrowIfFrozen();

                if (value != null && !Helpers.IsAssignableFrom(this.Type, value))
                    throw new ArgumentException("Specified type " + value.Name + " is not assignable to " + this.Type.Name);
                // will set also member.CollectionConcreteType in InitSerializers
                _settingsValue.ConstructType = value;
            }
        }

        public bool? DefaultEnhancedFormat { get { return _settingsValue.Member.EnhancedFormat; } set { _settingsValue.Member.EnhancedFormat = value; } }
        public EnhancedMode DefaultEnhancedWriteAs { get { return _settingsValue.Member.EnhancedWriteMode; } set { _settingsValue.Member.EnhancedWriteMode = value; } }
        public BinaryDataFormat? ContentBinaryFormatHint { get { return _settingsValue.Member.ContentBinaryFormatHint; } set { _settingsValue.Member.ContentBinaryFormatHint = value; } }
        public CollectionFormat CollectionFormat { get { return _settingsValue.Member.Collection.Format; } set { _settingsValue.Member.Collection.Format = value; } }
        public Type CollectionItemType { get { return _settingsValue.Member.Collection.ItemType; } set { _settingsValue.Member.Collection.ItemType = value; } }
        
        /// <summary>
        /// Gets or sets a value indicating that this type should NOT be treated as a list, even if it has
        /// familiar list-like characteristics (enumerable, add, etc)
        /// </summary>
        public bool IgnoreListHandling
        {
            get { return _settingsValue.IgnoreListHandling; }
            set
            {
                if (value && Type.IsArray)
                    throw new InvalidOperationException("Can't disable list handling for arrays");
                _settingsValue.IgnoreListHandling = value;
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

        internal bool IsAutoTuple
        {
            get { return _settingsValue.IsAutoTuple; }
            set { _settingsValue.IsAutoTuple = value; }
        }

    }
}
#endif