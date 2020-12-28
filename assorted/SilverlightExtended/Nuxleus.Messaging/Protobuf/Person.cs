// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Runtime.Serialization;
using System.ComponentModel;
using AqlaSerializer;
using System.Collections.Generic;

namespace Nuxleus.Messaging.Protobuf {

    [ProtoBuf.ProtoContract]
    public class Person {
        [ProtoBuf.ProtoMember(1, Name = "Name", IsRequired = true)]
        public string Name { get; set; }
        [ProtoBuf.ProtoMember(2, Name = "ID", IsRequired = true, DataFormat = ProtoBuf.DataFormat.TwosComplement)]
        public int ID { get; set; }
        [ProtoBuf.ProtoMember(3, Name = "Email", IsRequired = true)]
        public string Email { get; set; }
        [ProtoBuf.ProtoMember(4, Name = "Phone", IsRequired = false)]
        public List<PhoneNumber> Phone { get; set; } 
    }

    [ProtoBuf.ProtoContract]
    public class PhoneNumber {
        [ProtoBuf.ProtoMember(1, Name = "Number", IsRequired = true)]
        public string Number { get; set; }

        [DefaultValue(PhoneType.HOME)]
        [ProtoBuf.ProtoMember(2, Name = "Type", IsRequired = true)]
        public PhoneType Type { get; set; }
    }

    [ProtoBuf.ProtoContract]
    public enum PhoneType { MOBILE, HOME, WORK }
}
