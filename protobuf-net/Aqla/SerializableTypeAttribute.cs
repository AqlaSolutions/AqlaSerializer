// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016
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
        AllowMultiple = false, Inherited = false)]
    public sealed class SerializableTypeAttribute : Attribute
    {
        public SerializableTypeAttribute()
        {
            
        }

        public SerializableTypeAttribute(bool defaultEnhancedFormat, EnhancedMode defaultEnchancedWriteAs = 0)
        {
            DefaultEnhancedFormat = defaultEnhancedFormat;
            DefaultEnhancedWriteAs = defaultEnchancedWriteAs;
        }

        public SerializableTypeAttribute(EnhancedMode defaultEnchancedWriteAs)
        {
            DefaultEnhancedWriteAs = defaultEnchancedWriteAs;
            TypeSettings.Member.EnhancedFormat = defaultEnchancedWriteAs != EnhancedMode.NotSpecified ? true : (bool?)null;
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
        /// Supported features; this settings is used only for members; serialization of root type itself is controlled by RuntimeTypeModel settings. See <see cref="SerializableMemberAttributeBase.EnhancedFormat"/>
        /// </summary>
        public bool DefaultEnhancedFormat { get { return TypeSettings.Member.EnhancedFormat.Value; } set { TypeSettings.Member.EnhancedFormat = value; } }

        public bool DefaultEnhancedFormatHasValue => TypeSettings.Member.EnhancedFormat.HasValue;

        /// <summary>
        /// Applies only to enums (not to DTO classes themselves); gets or sets a value indicating that an enum should be treated directly as an int/short/etc, rather
        /// than enforcing .proto enum rules. This is useful *in particul* for [Flags] enums.
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
        public Type ConcreteType
        {
            get { return TypeSettings.ConcreteType; }
            set
            {
                // sets both for member and for type
                // because member can only have ConcreteType specified for collection but not normal types
                // while TypeSettings.ConcreteType may used for not collections too
                TypeSettings.Member.CollectionConcreteType = value;
                TypeSettings.ConcreteType = value;
            }
        }

        /// <summary>
        /// Enhanced features
        /// </summary>
        public EnhancedMode DefaultEnhancedWriteAs { get { return TypeSettings.Member.EnhancedWriteMode; } set { TypeSettings.Member.EnhancedWriteMode = value; } }
        
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
        /// If specified, do NOT treat this type as a list, even if it looks like one.
        /// </summary>
        public bool IgnoreListHandling { get { return TypeSettings.IgnoreListHandling; } set { TypeSettings.IgnoreListHandling = value; } }

        /// <summary>
        /// Gets or sets the fist offset to use with implicit field tags;
        /// only uesd if ImplicitFields is set.
        /// </summary>
        public int ImplicitFirstTag
        {
            get { return implicitFirstTag; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException("value");
                implicitFirstTag = value;
            }
        }

        private int implicitFirstTag;

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
        /// Property is treated as public only if both get and set accessors are public
        /// </summary>
        public bool ExplicitPropertiesContract { get; set; } = true;

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
        internal bool InferTagFromNameHasValue
        { // note that this property is accessed via reflection and should not be removed
            get { return HasFlag(OPTIONS_InferTagFromNameHasValue); }
        }

        private int dataMemberOffset;

        /// <summary>
        /// Specifies an offset to apply to [DataMember(Order=...)] markers;
        /// this is useful when working with mex-generated classes that have
        /// a different origin (usually 1 vs 0) than the original data-contract.
        /// 
        /// This value is added to the Order of each member.
        /// </summary>
        public int DataMemberOffset { get { return dataMemberOffset; } set { dataMemberOffset = value; } }

        private bool HasFlag(byte flag)
        {
            return (flags & flag) == flag;
        }

        private void SetFlag(byte flag, bool value)
        {
            if (value) flags |= flag;
            else flags = (byte)(flags & ~flag);
        }

        private byte flags;

        private const byte
            OPTIONS_InferTagFromName = 1,
            OPTIONS_InferTagFromNameHasValue = 2,
            OPTIONS_UseAqlaMembersOnly = 4;
    }
}