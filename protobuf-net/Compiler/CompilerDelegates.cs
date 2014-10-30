// Modified by Vladyslav Taranov for AqlaSerializer, 2014
#if FEAT_COMPILER
namespace AqlaSerializer.Compiler
{
    internal delegate void ProtoSerializer(object value, ProtoWriter dest);
    internal delegate object ProtoDeserializer(object value, ProtoReader source);
}
#endif