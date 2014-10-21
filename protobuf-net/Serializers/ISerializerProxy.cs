// Modified by Vladyslav Taranov for AqlaSerializer, 2014
#if !NO_RUNTIME

namespace ProtoBuf.Serializers
{
    interface ISerializerProxy
    {
        IProtoSerializer Serializer { get; }
    }
}
#endif