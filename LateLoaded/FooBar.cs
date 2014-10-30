// Modified by Vladyslav Taranov for AqlaSerializer, 2014

using AqlaSerializer;
namespace LateLoaded
{
    [ProtoBuf.ProtoContract]
    [ProtoBuf.ProtoInclude(2, typeof(Bar))]
    public class Foo
    {
        [ProtoBuf.ProtoMember(1)]
        public string BaseProp { get; set; }
    }

    [ProtoBuf.ProtoContract]
    public class Bar : Foo
    {
        [ProtoBuf.ProtoMember(1)]
        public string ChildProp { get; set; }
    }
}
