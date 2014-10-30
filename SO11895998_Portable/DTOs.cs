
using AqlaSerializer;
namespace SO11895998
{
    [ProtoBuf.ProtoContract]
    [ProtoBuf.ProtoInclude(2, typeof(Bar))]
    public abstract class Foo
    {
        [ProtoBuf.ProtoMember(1)]
        public int Value { get; set; }
    }

    [ProtoBuf.ProtoContract]
    public class Bar : Foo
    {
        [ProtoBuf.ProtoMember(2)]
        public string Name { get; set; }
    }
}
