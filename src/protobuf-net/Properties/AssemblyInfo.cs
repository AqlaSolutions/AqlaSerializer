
using ProtoBuf;
// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf.Meta;
using System.Runtime.CompilerServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("aqlaserializer")]
[assembly: AssemblyDescription("Fast and portable serializer for .NET")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyProduct("aqlaserializer")]
[assembly: AssemblyCopyright("See https://github.com/AqlaSolutions/AqlaSerializer")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

#if !PORTABLE
// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("224e5fc5-09f7-4fe3-a0a3-cf72b9f3593e")]
#endif

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("2.0.1.0")]
#if !CF
[assembly: AssemblyFileVersion("2.0.1.0")]
#endif
#if !FX11
[assembly: InternalsVisibleTo("aqlaserializer.unittest, PublicKey=002400000480000094000000060200000024000052534131000400000100010091B11AB23561C227F083424C0162A38DA330B724B6E96C1BE6C5989BFDD5C1BA3E555D8F105DD352C2623FE6AF90F4FA3173C6120DD567283434513DA579728230E1697A156770A81B7FBF5535ECDB96D2737E74181A4D980647AE33CDFB6E0C1FF63065AE8E33BB27374090393685FF265563655DE4829B0E5C996B1CF9A3E3")]
[assembly: InternalsVisibleTo("Examples, PublicKey=002400000480000094000000060200000024000052534131000400000100010091B11AB23561C227F083424C0162A38DA330B724B6E96C1BE6C5989BFDD5C1BA3E555D8F105DD352C2623FE6AF90F4FA3173C6120DD567283434513DA579728230E1697A156770A81B7FBF5535ECDB96D2737E74181A4D980647AE33CDFB6E0C1FF63065AE8E33BB27374090393685FF265563655DE4829B0E5C996B1CF9A3E3")]

#endif

[assembly: CLSCompliant(false)]
[assembly: TypeForwardedTo(typeof(TypeModel))]
[assembly: TypeForwardedTo(typeof(ProtoReader))]
[assembly: TypeForwardedTo(typeof(ProtoWriter))]
[assembly: TypeForwardedTo(typeof(SerializationContext))]
[assembly: TypeForwardedTo(typeof(SubItemToken))]
[assembly: TypeForwardedTo(typeof(WireType))]
[assembly: TypeForwardedTo(typeof(PrefixStyle))]
[assembly: TypeForwardedTo(typeof(IExtensible))]
[assembly: TypeForwardedTo(typeof(IExtension))]
[assembly: TypeForwardedTo(typeof(IExtensionResettable))]
[assembly: TypeForwardedTo(typeof(TypeFormatEventArgs))]
[assembly: TypeForwardedTo(typeof(TypeFormatEventHandler))]
[assembly: TypeForwardedTo(typeof(ProtoTypeCode))]
[assembly: TypeForwardedTo(typeof(DataFormat))]
[assembly: TypeForwardedTo(typeof(ProtoException))]
[assembly: TypeForwardedTo(typeof(DiscriminatedUnion128))]
[assembly: TypeForwardedTo(typeof(DiscriminatedUnion128Object))]
[assembly: TypeForwardedTo(typeof(DiscriminatedUnion32))]
[assembly: TypeForwardedTo(typeof(DiscriminatedUnion32Object))]
[assembly: TypeForwardedTo(typeof(DiscriminatedUnion64))]
[assembly: TypeForwardedTo(typeof(DiscriminatedUnion64Object))]
[assembly: TypeForwardedTo(typeof(DiscriminatedUnionObject))]
[assembly: TypeForwardedTo(typeof(BclHelpers))]
[assembly: TypeForwardedTo(typeof(TimeSpanScale))]
[assembly: TypeForwardedTo(typeof(BufferExtension))]
[assembly: TypeForwardedTo(typeof(Extensible))]

[assembly: TypeForwardedTo(typeof(ProtoBeforeDeserializationAttribute))]
[assembly: TypeForwardedTo(typeof(ProtoBeforeSerializationAttribute))]
[assembly: TypeForwardedTo(typeof(ProtoAfterDeserializationAttribute))]
[assembly: TypeForwardedTo(typeof(ProtoAfterSerializationAttribute))]
[assembly: TypeForwardedTo(typeof(ProtoContractAttribute))]
[assembly: TypeForwardedTo(typeof(ProtoEnumAttribute))]
[assembly: TypeForwardedTo(typeof(ProtoConverterAttribute))]
[assembly: TypeForwardedTo(typeof(ProtoIncludeAttribute))]
[assembly: TypeForwardedTo(typeof(ProtoIgnoreAttribute))]
[assembly: TypeForwardedTo(typeof(ProtoMapAttribute))]
[assembly: TypeForwardedTo(typeof(ProtoMemberAttribute))]
[assembly: TypeForwardedTo(typeof(MemberSerializationOptions))]
[assembly: TypeForwardedTo(typeof(ProtoPartialMemberAttribute))]
[assembly: TypeForwardedTo(typeof(ProtoPartialIgnoreAttribute))]
[assembly: TypeForwardedTo(typeof(ImplicitFields))]
[assembly: TypeForwardedTo(typeof(IProtoInput<>))]
[assembly: TypeForwardedTo(typeof(IProtoOutput<>))]
[assembly: TypeForwardedTo(typeof(IMeasuredProtoOutput<>))]
[assembly: TypeForwardedTo(typeof(MeasureState<>))]
[assembly: TypeForwardedTo(typeof(CompatibilityLevel))]
[assembly: TypeForwardedTo(typeof(CompatibilityLevelAttribute))]

#if PLAT_SKIP_LOCALS_INIT
[module: SkipLocalsInit]
#endif