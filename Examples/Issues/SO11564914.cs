// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples.Issues
{
    [TestFixture]
    public class SO11564914
    {
        [Test]
        public void SerializeFromProtobufCSharpPortShouldGiveUsefulMessage()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => {
                var obj = new BlockHeader();
                Serializer.Serialize(Stream.Null, obj);
            });
            Assert.That(ex.Message, Is.EqualTo("Are you mixing protobuf-net and protobuf-csharp-port? See http://stackoverflow.com/q/11564914; type: Examples.Issues.SO11564914+BlockHeader"));
        }
        [Test]
        public void DeserializeFromProtobufCSharpPortShouldGiveUsefulMessage()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => {
                var obj = new BlockHeader();
                Serializer.Deserialize<BlockHeader>(Stream.Null);
            });
            Assert.That(ex.Message, Is.EqualTo("Are you mixing protobuf-net and protobuf-csharp-port? See http://stackoverflow.com/q/11564914; type: Examples.Issues.SO11564914+BlockHeader"));
        }

        public sealed partial class BlockHeader : GeneratedMessage<BlockHeader, BlockHeader.Builder>
        {
            // yada yada yada
            public class Builder
            {
                
            }
        }

        public class GeneratedMessage<TFoo, TBar>
        {
        }
        
    }
}
