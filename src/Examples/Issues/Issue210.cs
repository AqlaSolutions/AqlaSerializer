using System.IO;
using NUnit.Framework;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Examples.Issues
{
    public class Issue210
    {
        [ProtoContract]
        public class ProtoTestOne
        {
            //This causes the exception
            static ProtoTestOne()
            {
            }

            public ProtoTestOne()
            {
            }
        }

        [Test]
        public void ShouldDeserializeWithStaticConstructor()
        {
            var typeModel = RuntimeTypeModel.Create();
            typeModel.AutoCompile = true;
#pragma warning disable CS0618
            typeModel.Deserialize(new MemoryStream(), null, typeof(ProtoTestOne));
#pragma warning restore CS0618
        }
    }
}