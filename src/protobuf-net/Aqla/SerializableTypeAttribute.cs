﻿// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using AqlaSerializer.Settings;
#if FEAT_IKVM
using AqlaSerializer;
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#else
using System.Reflection;
using AqlaSerializer;

#endif

namespace AqlaSerializer
{
    /// <summary>
    /// Indicates that a type is defined for protocol-buffer serialization. Settings specified here are inherited by members of this type if not explicitely specified for them.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Interface,
        AllowMultiple = true, Inherited = false)]
    public sealed class SerializableTypeAttribute : Attribute
    {
        public SerializableTypeAttribute()
        {
            
        }
        
        public SerializableTypeAttribute(ValueFormat defaultFormat)
        {
            TypeSettings.Member.Format = defaultFormat;
        }

        public TypeSettingsValue TypeSettings;

        /// <summary>
        /// Allows to use multiple attributes with different settings for each model
        /// </summary>
        public object ModelId { get; set; }

        /// <summary>
        /// Gets or sets the defined name of the type.
        /// </summary>
        public string Name { get { return TypeSettings.Name; } set { TypeSettings.Name = value; } }

        /// <summary>
        /// Applies only to enums (not to DTO classes themselves); gets or sets a value indicating that an enum should be treated directly as an int/short/etc, rather
        /// than enforcing .proto enum rules. This is useful *in particul* for [Flags] enums. Default: <see langword="true"/>.
        /// </summary>
        public bool EnumPassthru { get { return TypeSettings.EnumPassthru.Value; } set { TypeSettings.EnumPassthru = value; } }

        public bool EnumPassthruHasValue => TypeSettings.EnumPassthru.HasValue;

        /// <summary>
        /// If true, the constructor for the type is bypassed during deserialization, meaning any field initializers
        /// or other initialization code is skipped. 
        /// This settings can't be controlled per member.
        /// </summary>
        public bool SkipConstructor { get { return TypeSettings.SkipConstructor; } set { TypeSettings.SkipConstructor = value; } }

        /// <summary>
        /// Indicates whether the value should be prefixed with length instead of using StartGroup-EndGroup tags. If set to true makes skipping removed field faster when deserializing but slows down writing.
        /// This settings can't be controlled per member.
        /// </summary>
        public bool PrefixLength { get { return TypeSettings.PrefixLength.Value; } set { TypeSettings.PrefixLength = value; } }

        public bool PrefixLengthHasValue => TypeSettings.PrefixLength.HasValue;

        /// <summary>
        /// The concrete type to create when a new instance of this type is needed; this may be useful when dealing
        /// with dynamic proxies, or with interface-based APIs; for collections this is a default collection type.
        /// </summary>
        public Type ConstructType
        {
            get { return TypeSettings.ConstructType; }
            set
            {
                // sets both for member and for type
                // because member can only have ConcreteType specified for collection but not normal types
                // while TypeSettings.ConcreteType may used for not collections too
                TypeSettings.Member.Collection.ConcreteType = value;
                TypeSettings.ConstructType = value;
            }
        }

        /// <summary>
        /// Supported features; this settings is used only for members; serialization of root type itself is controlled by RuntimeTypeModel. See <see cref="SerializableMemberAttributeBase.Format"/>
        /// </summary>
        public ValueFormat DefaultEnhancedFormat { get { return TypeSettings.Member.Format; } set { TypeSettings.Member.Format = value; } }
        
        /// <summary>
        /// The data-format to be used when encoding this value.
        /// </summary>
        public BinaryDataFormat ContentBinaryFormatHint { get { return TypeSettings.Member.ContentBinaryFormatHint.Value; } set { TypeSettings.Member.ContentBinaryFormatHint = value; } }

        public bool ContentBinaryFormatHintHasValue => TypeSettings.Member.ContentBinaryFormatHint.HasValue;

        /// <summary>
        /// Supported collection features
        /// </summary>
        public CollectionFormat CollectionFormat { get { return TypeSettings.Member.Collection.Format; } set { TypeSettings.Member.Collection.Format = value; } }

        /// <summary>
        /// The type of object for each item in the list (especially useful with ArrayList)
        /// </summary>
        public Type CollectionItemType { get { return TypeSettings.Member.Collection.ItemType; } set { TypeSettings.Member.Collection.ItemType = value; } }

        /// <summary>
        /// If true, when used as root object will not support root null checking or references to root
        /// </summary>
        public bool ForceCompactFormatForRoot { get { return TypeSettings.ForceCompactFormatForRoot; } set { TypeSettings.ForceCompactFormatForRoot = value; } }
        
        /// <summary>
        /// If specified, do NOT treat this type as a list, even if it looks like one.
        /// </summary>
        public bool IgnoreListHandling { get { return TypeSettings.IgnoreListHandling; } set { TypeSettings.IgnoreListHandling = value; } }

        /// <summary>
        /// Gets or sets the fist offset to use with implicit field tags;
        /// only uesd if ImplicitFields is set.
        /// </summary>
        public int ImplicitFirstTag
        {
            get { return _implicitFirstTag; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
                _implicitFirstTag = value;
            }
        }

        private int _implicitFirstTag;

        /// <summary>
        /// If specified, alternative contract markers (such as markers for XmlSerailizer or DataContractSerializer) are ignored.
        /// </summary>
        public bool UseAqlaMembersOnly { get { return HasFlag(OPTIONS_UseAqlaMembersOnly); } set { SetFlag(OPTIONS_UseAqlaMembersOnly, value); } }

        /// <summary>
        /// Gets or sets the mechanism used to automatically infer field tags
        /// for members. This option should be used in advanced scenarios only.
        /// Please review the important notes against the ImplicitFields enumeration.
        /// </summary>
        public ImplicitFieldsMode ImplicitFields { get; set; } = ImplicitFieldsMode.PublicProperties;


        /// <summary>
        /// Implicit public property is added only when both get and set accessors are present and public; implicit private property is added only when both get and set accessors are present.
        /// </summary>
        public bool ImplicitOnlyWriteable { get; set; } = true;
        
        /// <summary>
        /// Enables/disables automatic tag generation based on the existing name / order
        /// of the defined members. This option is not used for members marked
        /// with ProtoMemberAttribute, as intended to provide compatibility with
        /// WCF serialization. WARNING: when adding new fields you must take
        /// care to increase the Order for new elements, otherwise data corruption
        /// may occur.
        /// </summary>
        /// <remarks>If not explicitly specified, the default is assumed from Serializer.GlobalOptions.InferTagFromName.</remarks>
        public bool InferTagFromName
        {
            get { return HasFlag(OPTIONS_InferTagFromName); }
            set
            {
                SetFlag(OPTIONS_InferTagFromName, value);
                SetFlag(OPTIONS_InferTagFromNameHasValue, true);
            }
        }

        /// <summary>
        /// Has a InferTagFromName value been explicitly set? if not, the default from the type-model is assumed.
        /// </summary>
        internal bool InferTagFromNameHasValue => HasFlag(OPTIONS_InferTagFromNameHasValue);

        /// <summary>
        /// Specifies an offset to apply to [DataMember(Order=...)] markers;
        /// this is useful when working with mex-generated classes that have
        /// a different origin (usually 1 vs 0) than the original data-contract.
        /// 
        /// This value is added to the Order of each member.
        /// </summary>
        public int DataMemberOffset { get; set; }

        private bool HasFlag(byte flag)
        {
            return (_flags & flag) == flag;
        }

        private void SetFlag(byte flag, bool value)
        {
            if (value) _flags |= flag;
            else _flags = (byte)(_flags & ~flag);
        }

        private byte _flags;

        private const byte
            OPTIONS_InferTagFromName = 1,
            OPTIONS_InferTagFromNameHasValue = 2,
            OPTIONS_UseAqlaMembersOnly = 4;
    }
}