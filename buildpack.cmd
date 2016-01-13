@%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild all.build /p:WarningLevel=0
@packages\NuGet.CommandLine.2.0.40000\tools\NuGet.exe pack Nuget\aqlaserializer.nuspec
pause