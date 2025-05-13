AqlaSerializer
==============
It is a fast and portable binary serializer designed to be easily used on your existing code with minimal changes on a wide range of .NET platforms. With AqlaSerializer you can store objects as a small in size binary data (far smaller than xml). And it's more CPU effective than BinaryFormatter and other core .NET serializers (which could be unavailable on your target platform).

Basically this is a fork of well known data serializer <a href="https://github.com/mgravell/protobuf-net">protobuf-net</a>.

AqlaSerializer is an *object* serializer, it's primary goal is to support important .NET features like nested collections, references, etc. See also <a href="https://github.com/AqlaSolutions/AqlaSerializer/wiki/Comparison-with-protobuf-net-and-migration">comparison with protobuf-net page</a>. And it still supports Google Protocol Buffers format in compatibility mode.

It is a free open source project in which you can participiate.

The implementation is compatible with most of the .NET family, including .NET 3.5/4.0/4.5, .NET Standard 2.1 (.NET Core 3/3.1, .NET 5, .NET 6), Windows Phone 8, Silverlight 5, Android, iOS, UAP. The code is heavily based on Marc Gravell's protobuf-net but there are a lot of improvements and fixes.

Nuget: <a href="https://www.nuget.org/packages/aqlaserializer/">aqlaserializer</a>.

<a href="https://github.com/AqlaSolutions/AqlaSerializer/blob/master/Licence.txt">License</a> is same as for protobuf-net.

## Status
The project is not actively maintained anymore and not updated to the newest .NET versions. The current version of AqlaSerializer is mature and viable to use but it doesn't contain the newest features and improvements from protobuf-net. 

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
