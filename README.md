AqlaSerializer
==============
It is a fast and portable binary serializer designed to be easily used on your existing code with minimal changes on a wide range of .NET platforms. With AqlaSerializer you can store objects as a small in size binary data (far smaller than xml). And it's more CPU effective than BinaryFormatter and other core .NET serializers (which could be unavailable on your target platform).

Basically this is a fork of well known protobuf-net project. Protobuf-net tries to maintain Google Protocol Buffers format compatibility and unfortunately has issues with handling some very the common .NET specific features.

AqlaSerializer project goal is not to make a Protocol Buffers compatible implementation but instead support all common .NET features.

See also <a href="https://github.com/AqlaSolutions/AqlaSerializer/wiki">wiki</a>.
