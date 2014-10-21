AqlaSerializer
==============

<b>Why?</b>

Protobuf-net have problems with inheritance, reference tracking and can't pack data optimally because of Protocol Buffers format compatibility. I'm reworking Protobuf-net to make a binary serializer with its own format which is dedicated to support all the .NET-specific features. 

<b>Features</b>

1. Object tree comparsion related features. 
2. Field numbering will be only as an option.

Other features later.

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

<b>Contribution</b>

I accept pull requests. By submitting your code you agree that it will be covered by the same license as the whole project. If a new version of the project uses another license you agree that your code will stay there.

If you are not sure whether your pull request will be accepted please contact me preliminary.

if you want to become a collaborator you should make a significant contribution to the project with pull requests first.

<b>Binary format description</b> (name:)(type)example_value


	//reference-tracked-objects (rto) - class and dynamic types
	//non-rto - structs
	
	// field number - byte/ushort - per type
	
	(rto_count:)(varint)20
	// all rtos, 1 - has values, 0 - null
	(has_value:)(bits)10101010101010101010
	// all rtos that has value, 1 - ref, 0 - first encounter
	(refs:)(bits)1010101010
	// all rtos with first encounter, 0 - default type, 1 - specified type
	(specified_type:)(bits)101010
	
	[:if dynamic_types allowed]
	
	// all type ids that > max will be dynamic types with index = id-max-1
	(dynamic_types_count:)(varint)1
	(dynamic_type_fullnames:)(string[])System.Guid
	
	// dynamic type mapping settings should be possible to specify with attribute on field
	
	[:endif]
	
	// data entries:
	
	--
	// rto object (first-encounter), ref_id++

	[:for types from base to current]
	
	  [:if all fields has number or used setting in MetaType]
	
	    [:for every field]
	
	       [if rto && type specified:] (type:)(varint)0 [/if]
	       (field_number:)0
	       (value:)...
	
	    [:endfor]
	
	    (end of numbered fields:)(field_number)field_number.MaxValue
	
	  [:else]
	
	    [:for every field]

	      [if rto && type specified:] (type:)(varint)0 [/if]	
	      (value:)...
	
	    [:endfor]
	
	  [:endif]
	  
	[:endfor]
	
	
	--
	// rto object ref
	// ref ids - only first-encountered rtos 
	(ref:)(varint)1
	
	
	--
	// rto object null
	// nothing
	
	--
	// value object (non-rto)
	// the same data as rto
	...
