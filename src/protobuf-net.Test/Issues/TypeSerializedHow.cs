using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using ProtoBuf;

namespace AqlaSerializer.Issues
{
    public class TypeSerializedHow
    {
        [Test]
        public void TypeRoundtrips()
        {
            var obj = new ModelWithTypeMember
            {
                Id = 123,
                SomeType = typeof(Uri) // arbitrary
            };

            var clone = Serializer.DeepClone(obj);
            Assert.AreNotSame(obj, clone);
            Assert.AreEqual(123, clone.Id);
            Assert.Same(typeof(Uri), clone.SomeType);
        }

        [Test]
        public void ProveTypeEquivalence()
        {
            var obj = new ModelWithTypeMember
            {
                Id = 123,
                SomeType = typeof(Uri) // arbitrary
            };

            var clone = Serializer.ChangeType<ModelWithTypeMember,EquivModel>(obj);
            Assert.AreEqual(123, clone.Id);
            Assert.AreEqual(obj.SomeType.AssemblyQualifiedName, clone.SomeType);
        }

        [Test]
        public void TypeGeneratesProto()
        {
            var proto = Serializer.GetProto<ModelWithTypeMember>();
            Assert.AreEqual(@"package AqlaSerializer.Issues;

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
