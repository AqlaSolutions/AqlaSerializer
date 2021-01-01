﻿using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using ProtoBuf;

namespace AqlaSerializer.Issues
{
    public class TypeSerializedHow
    {
        [Fact]
        public void TypeRoundtrips()
        {
            var obj = new ModelWithTypeMember
            {
                Id = 123,
                SomeType = typeof(Uri) // arbitrary
            };

            var clone = Serializer.DeepClone(obj);
            Assert.NotSame(obj, clone);
            Assert.Equal(123, clone.Id);
            Assert.Same(typeof(Uri), clone.SomeType);
        }

        [Fact]
        public void ProveTypeEquivalence()
        {
            var obj = new ModelWithTypeMember
            {
                Id = 123,
                SomeType = typeof(Uri) // arbitrary
            };

            var clone = Serializer.ChangeType<ModelWithTypeMember,EquivModel>(obj);
            Assert.Equal(123, clone.Id);
            Assert.Equal(obj.SomeType.AssemblyQualifiedName, clone.SomeType);
        }

        [Fact]
        public void TypeGeneratesProto()
        {
            var proto = Serializer.GetProto<ModelWithTypeMember>();
            Assert.Equal(@"package AqlaSerializer.Issues;

message ModelWithTypeMember {
   optional int32 Id = 1 [default = 0];
   optional string SomeType = 2;
}
", proto);
        }
        [ProtoContract]
        public class ModelWithTypeMember
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public Type SomeType { get; set; }
        }
        [ProtoContract]
        public class EquivModel
        {
            [ProtoMember(1)]
            public int Id { get; set; }
            [ProtoMember(2)]
            public string SomeType { get; set; }
        }

    }
}
