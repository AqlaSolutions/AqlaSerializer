// Modified by Vladyslav Taranov for AqlaSerializer, 2014

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace MonoDto
{
    public static class MyModel
    {
        public static TypeModel CreateSerializer()
        {

            var model = TypeModel.Create();
            model.AutoCompile = false;
            var type = Type.GetType("MonoDto.OrderHeader, MonoDto");
            model.Add(type, true);
            type = Type.GetType("MonoDto.OrderDetail, MonoDto");
            model.Add(type, true);
            return model; //.Compile();
        }
    }
    [ProtoBuf.ProtoContract]
    public class OrderHeader
    {
        [ProtoBuf.ProtoMember(1)] public int Id { get; set; }
        [ProtoBuf.ProtoMember(2)] public string CustomerRef { get; set; }
        [ProtoBuf.ProtoMember(3)] public DateTime OrderDate { get; set; }
        [ProtoBuf.ProtoMember(4)] public DateTime DueDate { get; set; }
        private List<OrderDetail> lines;
        [ProtoBuf.ProtoMember(5)] public List<OrderDetail> Lines {
            get { return lines ?? (lines = new List<OrderDetail>()); }
        }
    }
    [ProtoBuf.ProtoContract]
    public class OrderDetail {
        [ProtoBuf.ProtoMember(1)] public int LineNumber { get; set; }
        [ProtoBuf.ProtoMember(2)] public string SKU { get; set; }
        [ProtoBuf.ProtoMember(3)] public int Quantity { get; set; }
        [ProtoBuf.ProtoMember(4)] public decimal UnitPrice { get; set; }
        [ProtoBuf.ProtoMember(5)] public string Notes { get; set; }
    }
}
