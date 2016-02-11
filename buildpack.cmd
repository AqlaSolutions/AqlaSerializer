"%Systemdrive%\Program Files (x86)\MSBuild\14.0\Bin\MsBuild.exe" all.build /p:WarningLevel=0
@packages\NuGet.CommandLine.2.0.40000\tools\NuGet.exe pack Nuget\aqlaserializer.nuspec
pause