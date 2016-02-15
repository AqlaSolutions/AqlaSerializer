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
    public abstract class SerializableMemberBaseAttribute : Attribute
    {
        internal MemberLevelSettingsValue LevelSettings;

        protected SerializableMemberBaseAttribute(int level, MemberFormat format = 0)
        {
            Level = level;
            MemberFormat = format;
        }

        public int Level { get; set; }

        /// <summary>
        /// Allows to use multiple attributes with different settings for each model
        /// </summary>
        public object ModelId { get; set; }

        /// <summary>
        /// Embeds the type information into the stream, allowing usage with types not known in advance.
        /// </summary>
        public bool DynamicType { get { return LevelSettings.DynamicType; } set { LevelSettings.DynamicType = value; } }

        /// <summary>
        /// Supported features
        /// </summary>
        public MemberFormat MemberFormat { get { return LevelSettings.MemberFormat; } set { LevelSettings.MemberFormat = value; } }

        /// <summary>
        /// Indicates that the value should not be traversed recursively
        /// </summary>
        public bool? WriteAsLateReference { get { return LevelSettings.WriteAsLateReference; } set { LevelSettings.WriteAsLateReference = value; } }
        
        /// <summary>
        /// Default collection implementation
        /// </summary>
        public Type CollectionConcreteType { get { return LevelSettings.CollectionConcreteType; } set { LevelSettings.CollectionConcreteType = value; } }

        /// <summary>
        /// The data-format to be used when encoding this value.
        /// </summary>
        public BinaryDataFormat? ContentBinaryFormat { get { return LevelSettings.ContentBinaryFormat; } set { LevelSettings.ContentBinaryFormat = value; } }

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
        public bool? CollectionAppend { get { return LevelSettings.Collection.Append; } set { LevelSettings.Collection.Append = value; } }
    }
}