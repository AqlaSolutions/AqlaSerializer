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
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public abstract class SerializableMemberAttributeBase : Attribute
    {
        public MemberLevelSettingsValue LevelSettings;
        
        protected SerializableMemberAttributeBase(int level, ValueFormat format = 0)
        {
            Level = level;
            LevelSettings.Format = format;
        }

        public int Level { get; set; }

        /// <summary>
        /// Allows to use multiple attributes with different settings for each model
        /// </summary>
        public object ModelId { get; set; }

        /// <summary>
        /// Embeds the type information into the stream, allowing usage with types not known in advance. Is not supported in LateReference mode.
        /// </summary>
        public bool DynamicType { get { return LevelSettings.WriteAsDynamicType.Value; } set { LevelSettings.WriteAsDynamicType = value; } }

        public bool DynamicTypeHasValue => LevelSettings.WriteAsDynamicType.HasValue;

        /// <summary>
        /// Used to specify member format which affects supported features and output size
        /// </summary>
        /// <remarks>
        /// <para>default - null: <br/>
        /// Peek format based on member type and <see cref="RuntimeTypeModel"/> settings. Will try to choose Enhanced when its settings are enabled.
        /// </para>
        /// <para>false: <br/>
        /// Write and read as plain field without advanced features. <br/>
        /// Versioning won't support switching to <see cref="Enhanced"/> format. <br/>
        /// Not supported settings will be ignored.
        /// </para>
        /// <para>true: <br/>
        /// Use reference and null support if applicable. Versioning won't support switching to <see cref="Compact"/> format.<br/>
        /// Supports <see cref="SerializableMemberAttribute.DynamicType"/>. <br/>
        /// Not supported settings will be ignored.</para>
        /// The reason why DynamicType is on properties is because it's considered of the same EnhancedMode.Reference format.
        /// </remarks>
        public ValueFormat Format { get { return LevelSettings.Format; } set { LevelSettings.Format = value; } }
        
        /// <summary>
        /// Default collection implementation
        /// </summary>
        public Type CollectionConcreteType { get { return LevelSettings.Collection.ConcreteType; } set { LevelSettings.Collection.ConcreteType = value; } }

        /// <summary>
        /// The data-format to be used when encoding this value.
        /// </summary>
        public BinaryDataFormat ContentBinaryFormatHint { get { return LevelSettings.ContentBinaryFormatHint.Value; } set { LevelSettings.ContentBinaryFormatHint = value; } }

        public bool ContentBinaryFormatHintHasValue => LevelSettings.ContentBinaryFormatHint.HasValue;

        /// <summary>
        /// Supported collection features
        /// </summary>
        public CollectionFormat CollectionFormat { get { return LevelSettings.Collection.Format; } set { LevelSettings.Collection.Format = value; } }

        /// <summary>
        /// The type of object for each item in the list (especially useful with ArrayList)
        /// </summary>
        public Type CollectionItemType { get { return LevelSettings.Collection.ItemType; } set { LevelSettings.Collection.ItemType = value; } }

        /// <summary>
        /// Indicates whether this field should *append* to existing values (the default is true, meaning *replace*).
        /// This option only applies to list/array data.
        /// </summary>
        [Obsolete("Collection append may be not supported in later versions, overwrite mode is recommended")]
        public bool CollectionAppend { get { return LevelSettings.Collection.Append.Value; } set { LevelSettings.Collection.Append = value; } }

        public bool CollectionAppendHasValue => LevelSettings.Collection.Append.HasValue;
    }
}