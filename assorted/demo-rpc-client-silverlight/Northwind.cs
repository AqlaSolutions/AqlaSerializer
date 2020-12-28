// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using AqlaSerializer;

namespace demo_rpc_client_silverlight.Northwind
{
    [ProtoBuf.ProtoContract(DataMemberOffset = 1)]
    partial class Customer {}

    [ProtoBuf.ProtoContract(DataMemberOffset = 1)]
    partial class Order {}

    [ProtoBuf.ProtoContract(DataMemberOffset = 1)]
    [ProtoBuf.ProtoPartialMember(1, "OrderID")]
    [ProtoBuf.ProtoPartialMember(2, "ProductID")]
    [ProtoBuf.ProtoPartialMember(3, "UnitPrice")]
    partial class Order_Detail { }
}
