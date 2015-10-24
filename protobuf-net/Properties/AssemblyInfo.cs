// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Reflection;
using System.Runtime.InteropServices;
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
[assembly: AssemblyVersion("1.0.0.776")]
#if !CF
[assembly: AssemblyFileVersion("1.0.0.776")]
#endif
#if !FX11
[assembly: InternalsVisibleTo("aqlaserializer.unittest, PublicKey="
    + "0024000004800000940000000602000000240000525341310004000001000100f7065c1c81939a"
    + "43bf1ae76067234b37524c90498a92d1fa9add4d8d43c75114e263cd8a10c79b85ee1543d50642"
    + "d66e798bfff809a0e3948dac1b145fd9cdfb0b08c83b2e12a0a5bb33973a8b069a6863368a4843"
    + "9e9734ae11e5a6ebbc3e2f4b64e9251830fb2b130a00be5a33c60e9bf90cc1b957555959652b81"
    + "e1b468b6")]
#endif

[assembly: CLSCompliant(false)]