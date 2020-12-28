using Microsoft.SqlServer.Server;
using System.Data.SqlTypes;
using System;
using AqlaSerializer;

namespace SqlClr
{
    [ProtoBuf.ProtoContract]
    [SqlUserDefinedTypeAttribute(Format.UserDefined, IsByteOrdered=true,
        IsFixedLength = false, MaxByteSize=1024)]
    public sealed class MyProtoUdt : INullable, IBinarySerialize
    {
        public bool IsNull { get { return false; } }
        public static MyProtoUdt Null() { return null; }

        public static MyProtoUdt Parse(string value) {
            throw new NotImplementedException();
        }

        void IBinarySerialize.Read(System.IO.BinaryReader r) {
            Serializer.Merge<MyProtoUdt>(r.BaseStream, this);
        }

        void IBinarySerialize.Write(System.IO.BinaryWriter w) {
            Serializer.Serialize<MyProtoUdt>(w.BaseStream, this);
        }
        [ProtoBuf.ProtoMember(3)]
        public int ShoeSize { get; set; }
        [ProtoBuf.ProtoMember(4)]
        public DateTime DateOfBirth { get; set; }
        [ProtoBuf.ProtoMember(5)]
        public bool IsActive { get; set; }
        [ProtoBuf.ProtoMember(6)]
        public decimal Balance { get; set; }
        [ProtoBuf.ProtoMember(7)]
        public float Ratio { get; set; }
    }


    [ProtoBuf.ProtoContract]
    [SqlUserDefinedTypeAttribute(Format.Native, IsByteOrdered = true)]
    public sealed class MyBasicUdt : INullable
    {
        public bool IsNull { get { return false; } }
        public static MyBasicUdt Null() { return null; }

        public static MyBasicUdt Parse(string value)
        {
            throw new NotImplementedException();
        }

        public int ShoeSize { get; set; }
        public DateTime DateOfBirth { get; set; }
        public bool IsActive { get; set; }
        public decimal Balance { get; set; }
        public float Ratio { get; set; }
    }
}
