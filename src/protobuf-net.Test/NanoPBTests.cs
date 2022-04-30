using Google.Protobuf.Reflection;
using NanoPB;
using ProtoBuf.Meta;
using System.Linq;
using NUnit.Framework;

namespace ProtoBuf.Test
{
    public class NanoPBTests
    {
        [Test]
        public void CanParseCustomOptionsFromExternalSchema()
        {
            var set = new FileDescriptorSet();
            set.AddImportPath("./Schemas");
            Assert.True(set.Add("nanopb_test.proto"));
            set.Process();
            CollectionAssert.IsEmpty(set.GetErrors());
            var bar = set.Files.Single(x => x.Name == "nanopb_test.proto")
                .MessageTypes.Single()
                .Fields.Single(x => x.Number == 3);

            // normally you'd just use an "if (Extensible.TryGetValue(...)" here; I'm proving it for the test
            Assert.True(Extensible.TryGetValue<NanoPBOptions>(RuntimeTypeModel.Default, bar.Options, 1010, out var options));
            Assert.True(options.ShouldSerializeMaxSize()); // this is "actively set" vs "set via the default" etc
            Assert.AreEqual(42, options.MaxSize);
        }
    }
}
