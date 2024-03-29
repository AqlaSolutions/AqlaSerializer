﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
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
[assembly: AssemblyVersion("2.0.1.1170")]
[assembly: AssemblyFileVersion("2.0.1.1170")]
[assembly: InternalsVisibleTo("aqlaserializer.unittest, PublicKey=002400000480000094000000060200000024000052534131000400000100010091B11AB23561C227F083424C0162A38DA330B724B6E96C1BE6C5989BFDD5C1BA3E555D8F105DD352C2623FE6AF90F4FA3173C6120DD567283434513DA579728230E1697A156770A81B7FBF5535ECDB96D2737E74181A4D980647AE33CDFB6E0C1FF63065AE8E33BB27374090393685FF265563655DE4829B0E5C996B1CF9A3E3")]
[assembly: InternalsVisibleTo("Examples, PublicKey=002400000480000094000000060200000024000052534131000400000100010091B11AB23561C227F083424C0162A38DA330B724B6E96C1BE6C5989BFDD5C1BA3E555D8F105DD352C2623FE6AF90F4FA3173C6120DD567283434513DA579728230E1697A156770A81B7FBF5535ECDB96D2737E74181A4D980647AE33CDFB6E0C1FF63065AE8E33BB27374090393685FF265563655DE4829B0E5C996B1CF9A3E3")]
[assembly: InternalsVisibleTo("aqlaserializer.unittest, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d8fd1659485682c7c34d15f7cd8b91dd11248ab56964a88500c41755e972d36ee921afbf5e9e0fa5c03a3da2315c217bafd5ae25d08e0e57ac67ba8ea214c5aded3f23a18d348ecc3ff671599d9116e92e1cb9996be792e7051c5b87f8d59cb8b2a27dcbb7a9468ece71961608d487dc9dcaab42fa0e32e4f2d121e8a5bd5b9")]
[assembly: InternalsVisibleTo("Examples, PublicKey=00240000048000009400000006020000002400005253413100040000010001002d8fd1659485682c7c34d15f7cd8b91dd11248ab56964a88500c41755e972d36ee921afbf5e9e0fa5c03a3da2315c217bafd5ae25d08e0e57ac67ba8ea214c5aded3f23a18d348ecc3ff671599d9116e92e1cb9996be792e7051c5b87f8d59cb8b2a27dcbb7a9468ece71961608d487dc9dcaab42fa0e32e4f2d121e8a5bd5b9")]
[assembly: CLSCompliant(false)]