﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016

using AqlaSerializer;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SignedSerializer, PublicKey="
    + "002400000480000094000000060200000024000052534131000400000100010009ed9caa457bfc"
    + "205716c3d4e8b255a63ddf71c9e53b1b5f574ab6ffdba11e80ab4b50be9c46d43b75206280070d"
    + "dba67bd4c830f93f0317504a76ba6a48243c36d2590695991164592767a7bbc4453b34694e31e2"
    + "0815a096e4483605139a32a76ec2fef196507487329c12047bf6a68bca8ee9354155f4d01daf6e"
    + "ec5ff6bc")]

namespace SignedDto
{
    [ProtoBuf.ProtoContract]
    internal class MyType
    {
        [ProtoBuf.ProtoMember(1)]
        public string Foo { get; set; }
        [ProtoBuf.ProtoMember(2)]
        internal string field;

        internal bool ShouldSerializefield() { return true; }

        [ProtoBuf.ProtoAfterSerialization]
        internal void OnAfterSerialized()
        {}
    }
}
