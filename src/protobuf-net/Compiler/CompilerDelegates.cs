using ProtoBuf.Serializers;

namespace ProtoBuf.Compiler
{
    internal delegate void ProtoSerializer<T>(ref ProtoWriter.State state, T value);
    internal delegate T ProtoDeserializer<T>(ref ProtoReader.State state, T value);
    internal delegate T ProtoSubTypeDeserializer<T>(ref ProtoReader.State state, SubTypeState<T> value) where T : class;
}// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if FEAT_COMPILER
namespace AqlaSerializer.Compiler
{
    internal delegate void ProtoSerializer(ProtoWriter dest, ref ProtoWriter.State state, object value);
    internal delegate object ProtoDeserializer(ProtoReader source, ref ProtoReader.State state, object value);
}
#endif