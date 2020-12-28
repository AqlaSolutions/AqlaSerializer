// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Examples.Issues
{
    [TestFixture]
    public class DeserializeExtensible
    {
        [Test]
        public void Execute()
        {
            var model = TypeModel.Create();
            model.AutoCompile = false;
            Execute(model, "Runtime");
            model.CompileInPlace();
            Execute(model, "CompileInPlace");
            Execute(model.Compile(), "Compile");
        }
        private void Execute(TypeModel model, string caption)
        {
            var large = new LargeType { Foo = 1, Bar = "abc" };
            SmallType small;
            using(var ms = new MemoryStream())
            {
                model.Serialize(ms, large);
                ms.Position = 0;
                small = (SmallType) model.Deserialize(ms, null, typeof(SmallType));
            }
            Assert.IsNotNull(small, caption);
        }
        [ProtoBuf.ProtoContract]
        public class LargeType {
            [ProtoBuf.ProtoMember(1)]
            public int Foo {get;set;}

            [ProtoBuf.ProtoMember(2)]
            public string Bar {get;set;}
        }
        [ProtoBuf.ProtoContract]
        public class SmallType : Extensible {
            [ProtoBuf.ProtoMember(3)]
            public string Blab {get;set;}
        }
    }    
}
