AqlaSerializer
==============
It is a fast and portable binary serializer designed to be easily used on your existing code with minimal changes on a wide range of .NET platforms. With AqlaSerializer you can store objects as a small in size binary data (far smaller than xml). And it's more CPU effective than BinaryFormatter and other core .NET serializers (which could be unavailable on your target platform).

Basically this is a fork of well known data serializer <a href="https://github.com/mgravell/protobuf-net">protobuf-net</a>.

AqlaSerializer is an *object* serializer, it's primary goal is to support important .NET features like nested collections, references, etc. See also <a href="https://github.com/AqlaSolutions/AqlaSerializer/wiki/Comparison-with-protobuf-net-and-migration">comparison with protobuf-net page</a>. And it still supports Google Protocol Buffers format in compatibility mode.

It is a free open source project in which you can participiate.

The implementation is compatible with most of the .NET family, including .NET 2.0/3.0/3.5/4.0, .NET Standard 2.1 (.NET Core 3, .NET 5), Windows Phone 8, Silverlight, Xamarin.Android, etc. The code is heavily based on Marc Gravell's protobuf-net but there are a lot of improvements and fixes.

Status: 
though it's not actively developed now I consider it <b>stable</b> for all supported platforms. There are no major issues that require fixing.  The NetStandard version is tested with same unit tests on .NET 5 runtime and it works well.

Nuget: <a href="https://www.nuget.org/packages/aqlaserializer/">aqlaserializer</a>.

See also <a href="https://github.com/AqlaSolutions/AqlaSerializer/wiki">wiki</a>.

See also <a href="https://github.com/AqlaSolutions/AqlaSerializer/blob/master/Licence.txt">License.txt</a>.

## Advanced  examples

* <a href="https://github.com/AqlaSolutions/AqlaSerializer/blob/master/protobuf-net.unittest/AqlaAttributes/ImplicitFields.cs">Implicit fields</a>
* <a href="https://github.com/AqlaSolutions/AqlaSerializer/blob/master/protobuf-net.unittest/Aqla/LinkedListAsLateReference.cs">LateReference mode for big LinkedLists to avoid stack overflow</a>
* <a href="https://github.com/AqlaSolutions/AqlaSerializer/blob/master/protobuf-net.unittest/Aqla/NestedLevelsTest.cs">Control of nested elements serialization behavior</a>
* <a href="https://github.com/AqlaSolutions/AqlaSerializer/blob/master/protobuf-net.unittest/Aqla/AddTypes.cs">Types registration from code</a>
