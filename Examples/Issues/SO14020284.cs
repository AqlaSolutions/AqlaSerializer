// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Examples.Issues
{
    [Ignore("Inheritance convertation is not supported in AqlaSerializer")]
    [TestFixture]
    public class SO14020284
    {
        [Test]
        public void Execute()
        {
            var model = RuntimeTypeModel.Create();
            model.AutoCompile = false;
            Execute(model, "Runtime");
            model.CompileInPlace();
            Execute(model, "CompileInPlace");
            //Execute(model.Compile(), "Compile");

            model.Compile("SO14020284", "SO14020284.dll");
            PEVerify.AssertValid("SO14020284.dll");

        }
        public void Execute(TypeModel model, string caption)
        {
            try
            {
                var ms = new MemoryStream();
                model.Serialize(ms, new EncapsulatedOuter { X = 123, Inner = new EncapsulatedInner { Y = 456 } });
                ms.Position = 0;
                var obj = (InheritedChild)model.Deserialize(ms, null, typeof(InheritedBase));
                Assert.AreEqual(123, obj.X, caption);
                Assert.AreEqual(456, obj.Y, caption);
            }
            catch (Exception ex)
            {
                Assert.Fail(caption + ":" + ex.Message);
            }
        }
        [ProtoBuf.ProtoContract]
        public class EncapsulatedOuter
        {
            [ProtoBuf.ProtoMember(10)]
            public EncapsulatedInner Inner { get; set; }

            [ProtoBuf.ProtoMember(1)]
            public int X { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class EncapsulatedInner
        {
            [ProtoBuf.ProtoMember(1)]
            public int Y { get; set; }
        }
        [ProtoBuf.ProtoContract]
        [ProtoBuf.ProtoInclude(10, typeof(InheritedChild))]
        public class InheritedBase
        {
            [ProtoBuf.ProtoMember(1)]
            public int X { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class InheritedChild : InheritedBase
        {
            [ProtoBuf.ProtoMember(1)]
            public int Y { get; set; }
        }
    }
}
