using System;
using AqlaSerializer.Serializers;

namespace AqlaSerializer.Meta
{
    internal interface IValueSerializerBuilder
    {
        IProtoSerializerWithWireType BuildValueFinalSerializer(ValueSerializationSettings settings, bool isMemberOrNested, out WireType wireType);

        IProtoSerializerWithWireType TryGetCoreSerializer(BinaryDataFormat dataFormat, Type type, out WireType defaultWireType,
                                                                          bool tryAsReference, bool dynamicType, bool overwriteList, bool allowComplexTypes);

        IProtoSerializerWithWireType TryGetCoreSerializer(BinaryDataFormat dataFormat, Type type, out WireType defaultWireType,
                                                                          ref bool tryAsReference, bool dynamicType, bool overwriteList, bool isPackedCollection, bool allowComplexTypes, bool tryAsLateRef, ref object defaultValue);
        
    }
}