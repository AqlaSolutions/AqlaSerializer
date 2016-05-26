#if !NO_RUNTIME
using System;
using AqlaSerializer.Serializers;

namespace AqlaSerializer.Meta
{
    interface ILateReferenceSerializerProxy
    {
        IProtoSerializerWithWireType LateReferenceSerializer { get; }
        Exception LateReferenceSerializerBuildException { get; }
    }
}
#endif