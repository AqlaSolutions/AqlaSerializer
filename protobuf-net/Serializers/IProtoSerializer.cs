// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
#endif

namespace AqlaSerializer.Serializers
{
    // field header consists of a field number and a wire type (up to 8 cases) and it may be encoded as 1 byte (for first 0..15 field numbers)
    // So rules are:
    // 1. each serializer/decorator except Root should always assume that the field number was already written before
    // 2. each serializer/decorator which calls multiple serializers inside should write field numbers for each of them (and read field headers also)
    // 3. some "AutoType" serializers assume that also a wire type was already written too and their behavior may vary depending on that type (should that be precompiled?)
    // 4. no serializers except Root should write a new field header until the previously header from top is completed and its value is written
    // 5. non-"AutoType" serializer may "cancel" current field when writing; in such case that field won't be present in stream 
    // and Read() method won't be called for that field at all when deserializing which may cause keeping the current value of property (e.g. null for array members)

    /// <summary>
    /// Expects field number to be set. Will set its own wire type. All high-level serializers should assume they are in a field and always set wire type 
    /// </summary>
    interface IProtoSerializerWithWireType : IProtoSerializer
    {
        
    }

    /// <summary>
    /// Expects field number *and* field type to be set; it will NOT complete field with wiretype so the field header should be fully set for them
    /// </summary>
    interface IProtoSerializerWithAutoType : IProtoSerializer
    {
        
    }
    
    interface IProtoSerializer
    {
        /// <summary>
        /// The type that this serializer is intended to work for.
        /// </summary>
        Type ExpectedType { get; }

#if !FEAT_IKVM
        /// <summary>
        /// Perform the steps necessary to serialize this data.
        /// </summary>
        /// <param name="value">The value to be serialized.</param>
        /// <param name="dest">The writer entity that is accumulating the output data.</param>
        void Write(object value, ProtoWriter dest);

        /// <summary>
        /// Perform the steps necessary to deserialize this data.
        /// </summary>
        /// <param name="value">The current value, if appropriate.</param>
        /// <param name="source">The reader providing the input data.</param>
        /// <returns>The updated / replacement value.</returns>
        object Read(object value, ProtoReader source);
#endif
        /// <summary>
        /// Indicates whether a Read operation <em>replaces</em> the existing value, or
        /// <em>extends</em> the value. If false, the "value" parameter to Read is
        /// discarded, and should be passed in as null.
        /// </summary>
        bool RequiresOldValue { get; }
        /// <summary>
        /// Now all Read operations return a value (although most do); if false no
        /// value should be expected.
        /// </summary>
        bool ReturnsValue { get; }
        
#if FEAT_COMPILER



        /// <summary>Emit the IL necessary to perform the given actions
        /// to serialize this data.
        /// </summary>
        /// <param name="ctx">Details and utilities for the method being generated.</param>
        /// <param name="valueFrom">The source of the data to work against;
        /// If the value is only needed once, then LoadValue is sufficient. If
        /// the value is needed multiple times, then note that a "null"
        /// means "the top of the stack", in which case you should create your
        /// own copy - GetLocalWithValue.</param>
        void EmitWrite(Compiler.CompilerContext ctx, Compiler.Local valueFrom);

        /// <summary>
        /// Emit the IL necessary to perform the given actions to deserialize this data.
        /// </summary>
        /// <param name="ctx">Details and utilities for the method being generated.</param>
        /// <param name="entity">For nested values, the instance holding the values; note
        /// that this is not always provided - a null means not supplied. Since this is always
        /// a variable or argument, it is not necessary to consume this value.</param>
        void EmitRead(Compiler.CompilerContext ctx, Compiler.Local entity);
#endif
    }
}
#endif