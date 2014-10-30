// Modified by Vladyslav Taranov for AqlaSerializer, 2014
#if !NO_RUNTIME

namespace AqlaSerializer.Serializers
{
    interface ISerializerProxy
    {
        IProtoSerializer Serializer { get; }
    }
}
#endif