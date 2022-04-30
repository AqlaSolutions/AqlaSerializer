// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.ComponentModel;

namespace AqlaSerializer
{
    /// <summary>Specifies a method on the root-contract in an hierarchy to be invoked before serialization.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
#if !SILVERLIGHT && !MONODROID && !IOS && !PORTABLE
    [ImmutableObject(true)]
#endif
    public sealed class BeforeSerializationCallbackAttribute : Attribute { }

    /// <summary>Specifies a method on the root-contract in an hierarchy to be invoked after serialization.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
#if !SILVERLIGHT && !MONODROID && !IOS && !PORTABLE
    [ImmutableObject(true)]
#endif
    public sealed class AfterSerializationCallbackAttribute : Attribute { }

    /// <summary>Specifies a method on the root-contract in an hierarchy to be invoked before deserialization.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
#if !SILVERLIGHT && !MONODROID && !IOS && !PORTABLE
    [ImmutableObject(true)]
#endif
    public sealed class BeforeDeserializationCallbackAttribute : Attribute { }

    /// <summary>Specifies a method on the root-contract in an hierarchy to be invoked after deserialization.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
#if !SILVERLIGHT && !MONODROID && !IOS && !PORTABLE
    [ImmutableObject(true)]
#endif
    public sealed class AfterDeserializationCallbackAttribute : Attribute { }
}
