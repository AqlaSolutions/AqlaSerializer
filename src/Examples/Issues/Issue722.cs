extern alias gpb;

using System;
using NUnit.Framework;

namespace ProtoBuf.Issues
{
    public class Issue722
    {
        [Test]
        public void ReportGoogleTypesUsefully()
        {
            var obj = new HazGoogleTypes();
            var ex = Assert.Throws<InvalidOperationException>(() => Serializer.DeepClone(obj));
            Assert.AreEqual("Type 'Google.Protobuf.WellKnownTypes.FloatValue' looks like a Google.Protobuf type; it cannot be used directly with protobuf-net without manual configuration; it may be possible to generate a protobuf-net type instead; see https://protobuf-net.github.io/protobuf-net/contract_first", ex.Message);
            var inner = Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.AreEqual("No serializer defined for type: Google.Protobuf.WellKnownTypes.FloatValue", inner.Message);
        }

        [ProtoContract]
        public class HazGoogleTypes
        {
            [ProtoMember(1)]
            gpb::Google.Protobuf.WellKnownTypes.FloatValue Foo { get; set; }
        }
    }
}
