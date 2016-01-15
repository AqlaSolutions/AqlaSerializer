// Modified by Vladyslav Taranov for AqlaSerializer, 2016

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using AqlaSerializer;

namespace MetroDto
{
    [DataContract, ProtoBuf.ProtoContract]
    public class TestClass
    {
        [DataMember(Order = 1), ProtoBuf.ProtoMember(1)]
        public int IntVal { get; set; }
        [DataMember(Order = 2), ProtoBuf.ProtoMember(2)]
        public string StrVal { get; set; }
        [DataMember(Order = 3), ProtoBuf.ProtoMember(3)]
        public MyEnum EnumVal { get; set; }
    }

    public enum MyEnum
    {
        Foo,
        Bar
    }
}
