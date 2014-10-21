// Modified by Vladyslav Taranov for AqlaSerializer, 2014

using ProtoBuf;
namespace LateLoaded
{
    [ProtoContract]
    [ProtoInclude(2, typeof(Bar))]
    public class Foo
    {
        [ProtoMember(1)]
        public string BaseProp { get; set; }
    }

    [ProtoContract]
    public class Bar : Foo
    {
        [ProtoMember(1)]
        public string ChildProp { get; set; }
    }
}
