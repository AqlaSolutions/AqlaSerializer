// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if FEAT_COMPILER
namespace AqlaSerializer.Compiler
{
    internal delegate void ProtoSerializer(ProtoWriter dest, ref ProtoWriter.State state, object value);
    internal delegate object ProtoDeserializer(ProtoReader source, ref ProtoReader.State state, object value);
}
#endif