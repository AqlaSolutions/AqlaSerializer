// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.Runtime.Serialization;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Examples.SimpleStream
{
    [TestFixture]
    public class NullTypes
    {
        [DataContract]
        class TypeWithNulls
        {
            [DataMember(Order = 1)]
            public int? Foo { get; set; }
        }
        
        [Test]
        public void TestNull()
        {
            TypeWithNulls twn = new TypeWithNulls { Foo = null },
                clone = TypeModel.Create(false, ProtoCompatibilitySettings.FullCompatibility).DeepClone(twn);
            Assert.IsNull(twn.Foo);
            Assert.IsTrue(Program.CheckBytes(twn, new byte[0]));
        }
        
        [Test]
        public void TestNotNull()
        {
            TypeWithNulls twn = new TypeWithNulls { Foo = 150 },
                clone = TypeModel.Create(false, ProtoCompatibilitySettings.FullCompatibility).DeepClone(twn);
            Assert.IsNotNull(twn.Foo);
            Assert.IsTrue(Program.CheckBytes(twn, 0x08, 0x96, 0x01));
        }
    }
}
