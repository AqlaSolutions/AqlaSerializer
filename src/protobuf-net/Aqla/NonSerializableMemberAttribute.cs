// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using AqlaSerializer;

namespace AqlaSerializer
{
    /// <summary>
    /// Indicates that a member should be excluded from serialization; this
    /// is only normally used when using implict fields.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field,
        AllowMultiple = true, Inherited = true)]
    public class NonSerializableMemberAttribute : Attribute
    {
        public object ModelId { get; set; }
    }

    /// <summary>
    /// Indicates that a member should be excluded from serialization; this
    /// is only normally used when using implict fields. This allows
    /// ProtoIgnoreAttribute usage
    /// even for partial classes where the individual members are not
    /// under direct control.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class,
            AllowMultiple = true, Inherited = false)]
    public sealed class PartialNonSerializableMemberAttribute : NonSerializableMemberAttribute
    {
        /// <summary>
        /// Creates a new ProtoPartialIgnoreAttribute instance.
        /// </summary>
        /// <param name="memberName">Specifies the member to be ignored.</param>
        public PartialNonSerializableMemberAttribute(string memberName)
            : base()
        {
            if (string.IsNullOrEmpty(memberName)) throw new ArgumentNullException(nameof(memberName));
            this.MemberName = memberName;
        }
        

        /// <summary>
        /// The name of the member to be ignored.
        /// </summary>
        public string MemberName { get; }
    }
}
