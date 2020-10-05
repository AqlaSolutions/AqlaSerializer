AqlaSerializer
==============
It is a fast and portable binary serializer designed to be easily used on your existing code with minimal changes on a wide range of .NET platforms. With AqlaSerializer you can store objects as a small in size binary data (far smaller than xml). And it's more CPU effective than BinaryFormatter and other core .NET serializers (which could be unavailable on your target platform).

Basically this is a fork of well known data serializer <a href="https://github.com/mgravell/protobuf-net">protobuf-net</a>.

AqlaSerializer is an *object* serializer, it's primary goal is to support important .NET features like nested collections, references, etc. See also <a href="https://github.com/AqlaSolutions/AqlaSerializer/wiki/Comparison-with-protobuf-net-and-migration">comparison with protobuf-net page</a>. And it still supports Google Protocol Buffers format in compatibility mode.

It is a free open source project in which you can participiate.

Status: 
though it's not actively developed now I consider it <b>stable</b> for all supported platforms. There are no major issues that require fixing. But I'm not sure about Core CLR support, it might require some testing.

Nuget: <a href="https://www.nuget.org/packages/aqlaserializer/">aqlaserializer</a>.

See also <a href="https://github.com/AqlaSolutions/AqlaSerializer/wiki">wiki</a>.

See also <a href="https://github.com/AqlaSolutions/AqlaSerializer/blob/master/Licence.txt">License.txt</a>.
