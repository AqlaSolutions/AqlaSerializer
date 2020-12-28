using System;

namespace AqlaSerializer.Meta
{
    [Flags]
    public enum AttributeType
    {
        None = 0,
        Aqla = 1,
        ProtoBuf = 2,
        Xml = 4,
        DataContract = 8,
        SystemSerializable = 16,
        SystemNonSerialized = 32,

        Default = Aqla | ProtoBuf | Xml | DataContract | SystemNonSerialized,
    }
}