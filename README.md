AqlaSerializer
==============

<b>What is it?</b>

I'm reworking Protobuf-net to make a binary serializer with its own format which is dedicated to support all .NET specific features.

Also I'm planning to add object tree comparsion related features.

<b>What platforms are guaranteed? </b>

MS .NET 3.5+, Mono, Unity3D

<b>Will it be compatible with Protobuf-net? </b>

It will accept Protobuf-net attributes but its binary format will not be compatible with Google Protocol Buffers.

<b>Current stage</b>

Now it's the same protobuf-net but with fixes and improvement on reference tracking.

Everything is subject to change. Don't rely on API.

<b>Known issues</b>

1. Tuples as reference are not supported.

2. Array resizing when deserializing on exisiting object can break references.
