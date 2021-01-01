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
    /// Declares a member to be used in protocol-buffer serialization, using
    /// the given Tag. A DataFormat may be used to optimise the serialization
    /// format (for instance, using zigzag encoding for negative numbers, or 
    /// fixed-length encoding for large values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field,
        AllowMultiple = true, Inherited = true)]
    public class SerializableMemberAttribute : SerializableMemberAttributeBase
    {
        /// <summary>
        /// Creates a new ProtoMemberAttribute instance.
        /// </summary>
        /// <param name="tag">Specifies the unique tag used to identify this member within the type.</param>
        public SerializableMemberAttribute(int tag, ValueFormat format = 0)
            : base(tag, format)
        {
            Init(tag);
        }

        void Init(int tag)
        {

            this.MemberSettings.Tag = tag;
        }

        internal MemberMainSettingsValue MemberSettings;
        
        /// <summary>
        /// Gets or sets the original name defined in the .proto; not used
        /// during serialization.
        /// </summary>
        public string Name { get { return MemberSettings.Name; } set { MemberSettings.Name = value; } }
        
        /// <summary>
        /// Gets the unique tag used to identify this member within the type.
        /// </summary>
        public int Tag => MemberSettings.Tag;
        
        /// <summary>
        /// Gets or sets a value indicating whether this member should be considered not optional when generating Protocol Buffers schema.
        /// </summary>
        public bool IsRequiredInSchema { get { return MemberSettings.IsRequiredInSchema; } set { MemberSettings.IsRequiredInSchema = value; } }

        /// <summary>
        /// Default value will be skipped when writing; null means not specified.
        /// </summary>
        public object DefaultValue { get; set; }
    }

    /// <summary>
    /// Declares a member to be used in protocol-buffer serialization, using
    /// the given Tag and MemberName. This allows ProtoBuf.ProtoMemberAttribute usage
    /// even for partial classes where the individual members are not
    /// under direct control.
    /// A DataFormat may be used to optimise the serialization
    /// format (for instance, using zigzag encoding for negative numbers, or 
    /// fixed-length encoding for large values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class,
            AllowMultiple = true, Inherited = false)]
    public sealed class SerializablePartialMemberAttribute : SerializableMemberAttribute
    {
        /// <summary>
        /// Creates a new ProtoMemberAttribute instance.
        /// </summary>
        /// <param name="tag">Specifies the unique tag used to identify this member within the type.</param>
        /// <param name="memberName">Specifies the member to be serialized.</param>
        public SerializablePartialMemberAttribute(int tag, string memberName, ValueFormat format = 0)
            : base(tag, format)
        {
            Init(memberName);
        }
        
        void Init(string memberName)
        {

            if (string.IsNullOrEmpty(memberName)) throw new ArgumentNullException(nameof(memberName));
            this.MemberName = memberName;
        }

        /// <summary>
        /// The name of the member to be serialized.
        /// </summary>
        public string MemberName { get; private set; }
    }
}
