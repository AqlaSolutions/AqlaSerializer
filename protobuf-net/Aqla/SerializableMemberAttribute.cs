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
    public class SerializableMemberAttribute : SerializableMemberBaseAttribute
        , IComparable
#if !NO_GENERICS
, IComparable<SerializableMemberAttribute>
#endif

    {
        /// <summary>
        /// Compare with another ProtoMemberAttribute for sorting purposes
        /// </summary>
        public int CompareTo(object other) { return CompareTo(other as SerializableMemberAttribute); }
        /// <summary>
        /// Compare with another ProtoMemberAttribute for sorting purposes
        /// </summary>
        public int CompareTo(SerializableMemberAttribute other)
        {
            if (other == null) return -1;
            if ((object)this == (object)other) return 0;
            int result = this.MemberSettings.Tag.CompareTo(other.MemberSettings.Tag);
            if (result == 0) result = string.CompareOrdinal(this.Name, other.Name);
            return result;
        }

        /// <summary>
        /// Creates a new ProtoMemberAttribute instance.
        /// </summary>
        /// <param name="tag">Specifies the unique tag used to identify this member within the type.</param>
        public SerializableMemberAttribute(int tag, MemberFormat format = 0)
            : this(tag, false, format)
        {
            
        }

        internal SerializableMemberAttribute(int tag, bool forced, MemberFormat format = 0)
            : base(0, format)
        {
            if (tag <= 0 && !forced) throw new ArgumentOutOfRangeException("tag");
            this.MemberSettings.Tag = tag;
        }
        
        internal MemberMainSettingsValue MemberSettings;
        
#if !NO_RUNTIME
        internal MemberInfo Member { get { return MemberSettings.Member; } set { MemberSettings.Member = value; } }
        internal bool TagIsPinned;
#endif
        /// <summary>
        /// Gets or sets the original name defined in the .proto; not used
        /// during serialization.
        /// </summary>
        public string Name { get { return MemberSettings.Name; } set { MemberSettings.Name = value; } }
        
        /// <summary>
        /// Gets the unique tag used to identify this member within the type.
        /// </summary>
        public int Tag => MemberSettings.Tag;

        internal void Rebase(int tag)
        {
            this.MemberSettings.Tag = tag;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this member should be considered not optional when generating Protocol Buffers schema.
        /// </summary>
        public bool IsRequiredInSchema { get { return MemberSettings.IsRequiredInSchema; } set { MemberSettings.IsRequiredInSchema = value; } }

        /// <summary>
        /// Default value will be skipped when writing; null means not specified.
        /// </summary>
        public object DefaultValue { get { return MemberSettings.DefaultValue; } set { MemberSettings.DefaultValue = value; } }
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
        // TODO nested levels?
        /// <summary>
        /// Creates a new ProtoMemberAttribute instance.
        /// </summary>
        /// <param name="tag">Specifies the unique tag used to identify this member within the type.</param>
        /// <param name="memberName">Specifies the member to be serialized.</param>
        public SerializablePartialMemberAttribute(int tag, string memberName, MemberFormat format = 0)
            : base(tag, format)
        {
            if (Helpers.IsNullOrEmpty(memberName)) throw new ArgumentNullException("memberName");
            this.MemberName = memberName;
        }
        /// <summary>
        /// The name of the member to be serialized.
        /// </summary>
        public string MemberName { get; }
    }
}
