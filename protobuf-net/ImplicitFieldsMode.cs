// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;

namespace AqlaSerializer
{
    /// <summary>
    /// Specifies the method used to infer field tags for members of the type
    /// under consideration. Tags are deduced using the invariant alphabetic
    /// sequence of the members' names; this makes implicit field tags very brittle,
    /// and susceptible to changes such as field names (normally an isolated
    /// change).
    /// </summary>
    public enum ImplicitFieldsMode
    {
        /// <summary>
        /// No members are serialized implicitly; all members require a suitable
        /// attribute such as [ProtoMember]. This is the recmomended mode for
        /// most scenarios.
        /// </summary>
        None = 0,
        /// <summary>
        /// Public properties and fields are eligible for implicit serialization;
        /// this treats the public API as a contract. Ordering beings from ImplicitFirstTag.
        /// </summary>
        PublicFieldsAndProperties = 1,
        /// <summary>
        /// Public and non-public fields are eligible for implicit serialization;
        /// this acts as a state/implementation serializer. Ordering beings from ImplicitFirstTag.
        /// </summary>
        AllFields = 2,
        /// <summary>
        /// Public and non-public properties are eligible for implicit serialization;
        /// this acts as a state/implementation serializer. Ordering beings from ImplicitFirstTag.
        /// </summary>
        AllProperties = 3,
        /// <summary>
        /// Public and non-public properties are eligible for implicit serialization;
        /// this acts as a state/implementation serializer. Ordering beings from ImplicitFirstTag.
        /// </summary>
        AllFieldsAndProperties = 4,
        /// <summary>
        /// Public fields are eligible for implicit serialization
        /// </summary>
        PublicFields = 5,
        /// <summary>
        /// Public properties are eligible for implicit serialization
        /// </summary>
        PublicProperties = 6
    }
}
