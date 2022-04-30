AqlaSerializer
==============
It is a fast and portable binary serializer designed to be easily used on your existing code with minimal changes on a wide range of .NET platforms. With AqlaSerializer you can store objects as a small in size binary data (far smaller than xml). And it's more CPU effective than BinaryFormatter and other core .NET serializers (which could be unavailable on your target platform).

Basically this is a fork of well known data serializer <a href="https://github.com/mgravell/protobuf-net">protobuf-net</a>.

AqlaSerializer is an *object* serializer, it's primary goal is to support important .NET features like nested collections, references, etc. See also <a href="https://github.com/AqlaSolutions/AqlaSerializer/wiki/Comparison-with-protobuf-net-and-migration">comparison with protobuf-net page</a>. And it still supports Google Protocol Buffers format in compatibility mode.

It is a free open source project in which you can participiate.

The implementation is compatible with most of the .NET family, including .NET 3.5/4.0/4.5, .NET Standard 2.1 (.NET Core 3/3.1, .NET 5, .NET 6), Windows Phone 8, Silverlight 5, Android, iOS, UAP. The code is heavily based on Marc Gravell's protobuf-net but there are a lot of improvements and fixes.

Nuget: <a href="https://www.nuget.org/packages/aqlaserializer/">aqlaserializer</a>.

<a href="https://github.com/AqlaSolutions/AqlaSerializer/blob/master/Licence.txt">License</a> is same as for protobuf-net.

## Usage

The usage is very simple; at the most basic level, simply read from a stream or write to a stream:

	[SerializableType]
	class Person 
	{
	    public int Id { get; set; }
	    public string Name { get; set: }
	}


	Serializer.Serialize(outputStream, person);
	
	...
	
	var person = Serializer.Deserialize<Person>(inputStream);

Read <b><a href="https://github.com/AqlaSolutions/AqlaSerializer/wiki">Getting started</a></b>

## We need your help
I'm trying to merge the current state of protobuf-net main branch but it's a lot of work. If you'd like to use the newest features of protobuf-net like proto3 support on AqlaSerializer please either contribute to the merging process by issuing a pull request or by <a href="https://btc.com/BC1QMRGHMP4VURV8Y9C8KS2RF58CTF8EXXPSTW2AP3">a BTC donation</a> so I can put more time myself into doing the merge. You can also help by spreading the word about this project so it can have more attention.

Currently I'm not much into .NET development and doing the merge just because I want to keep AqlaSerializer for the community even when I'm not actively using the serializer anymore. Nowadays performance is often neglected by using text-based serialization like json, xml and pure reflection. I want to improve this situation with AqlaSerializer. I don't know about a better alternative that is fast (I mean really fast), binary, supports objects graphs and works on AOT platforms (not mentioning other improvements) so I want to keep it up to date with the newest technologies like .NET 5, C#9 and proto3. But the lack of interest and support from the community takes away my motivation.

Note that the current version of AqlaSerializer is still viable to use. It doesn't contain the newest features and improvements from protobuf-net but remember that AqlaSerializer have a lot of its own stuff like well-done reference tracking which is unavailable in protobuf-net.
 
<a href="https://github.com/AqlaSolutions/AqlaSerializer/issues/21">Merging details</a>
