﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("aqlaserializer (3.5 extensions)")]
[assembly: AssemblyDescription("Fast and portable serializer for .NET - extension methods")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("aqlaserializer")]
[assembly: AssemblyCopyright("See https://github.com/AqlaSolutions/AqlaSerializer")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("d7dd679d-4a03-4dce-9585-689cf7a1f7f0")]

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
[assembly: AssemblyVersion("2.0.1.1031")]
#if !CF
[assembly: AssemblyFileVersion("2.0.1.1031")]
#endif
[assembly: CLSCompliant(true)]