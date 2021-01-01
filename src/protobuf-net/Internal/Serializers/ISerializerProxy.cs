// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME

namespace AqlaSerializer.Serializers
{
    interface ISerializerProxy
    {
        bool IsSerializerReady { get; }
        IProtoSerializerWithWireType Serializer { get; }
    }
}
