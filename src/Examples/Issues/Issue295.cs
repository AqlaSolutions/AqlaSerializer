// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.Collections.Generic;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue295
    {
        [ProtoBuf.ProtoContract(SkipConstructor = true), ProtoBuf.ProtoInclude(500, typeof(Plant))]
        public class Asset
        {
            public Asset()
            {
                AllAssets = new List<Asset>();
                ChildAssets = new List<Asset>();
            }
            [ProtoBuf.ProtoMember(1)]
            public List<Asset> AllAssets { get; private set; }

            [ProtoBuf.ProtoMember(2)]
            public List<Asset> AssetHierarcy { get; private set; }

            [ProtoBuf.ProtoMember(3)]
            public List<Asset> ChildAssets { get; private set; }
        }
        [ProtoBuf.ProtoContract(SkipConstructor = true)]
        public class Plant : Asset
        {
            [ProtoBuf.ProtoMember(105)]
            public Asset Blowers { get; set; }
        }

        [Test]
        public void Execute()
        {
            Asset asset = new Plant {Blowers = new Asset(), ChildAssets = {new Plant()}};
            var clone = Serializer.DeepClone(asset);
        }
    }
}
