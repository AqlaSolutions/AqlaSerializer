using System;
using AqlaSerializer.Serializers;

namespace AqlaSerializer.Meta
{
    internal interface IValueSerializerBuilder
    {
        IProtoSerializerWithWireType BuildValueFinalSerializer(ValueSerializationSettings settings, bool isMemberOrNested, out WireType wireType);

        IProtoSerializerWithWireType TryGetCoreSerializer(
            BinaryDataFormat dataFormat, Type type, out WireType defaultWireType,
            ref bool tryAsReference, bool dynamicType, bool appendCollection, bool isPackedCollection, bool allowComplexTypes, bool tryAsLateRef, ref object defaultValue);
    }

    static class ValueSerializerBuilderExtensions
    {
        public static IProtoSerializerWithWireType TryGetSimpleCoreSerializer(this IValueSerializerBuilder builder, BinaryDataFormat dataFormat, Type type, out WireType defaultWireType)
        {
            object dummy = null;
            bool tryAsReference = false;
            return builder.TryGetCoreSerializer(dataFormat, type, out defaultWireType, ref tryAsReference, false, false, false, false, false, ref dummy);
        }

    }
}