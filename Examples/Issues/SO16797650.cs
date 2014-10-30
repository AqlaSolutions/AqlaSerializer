// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using System.IO;

namespace Examples.Issues
{
    [TestFixture]
    public class SO16797650
    {
        [ProtoBuf.ProtoContract]
        public abstract class MessageBase
        {
            [ProtoBuf.ProtoMember(1)]
            public string ErrorMessage { get; set; }

            public abstract int Type { get; }
        }

        [ProtoBuf.ProtoContract]
        public class Echo : MessageBase
        {
            public const int ID = 1;

            public override int Type
            {
                get { return ID; }
            }

            [ProtoBuf.ProtoMember(1)]
            public string Message { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class Foo : MessageBase { public override int Type { get { return 42; } } }
        [ProtoBuf.ProtoContract]
        public class Bar : MessageBase { public override int Type { get { return 43; } } }
        [Test]
        public void AddSubtypeAtRuntime()
        {
            var messageBase = RuntimeTypeModel.Default[typeof(MessageBase)];
            // this could be explicit in code, or via some external config file
            // that you process at startup
            messageBase.AddSubType(10, typeof(Echo)); // would need to **reliably** be 10
            messageBase.AddSubType(11, typeof(Foo));
            messageBase.AddSubType(12, typeof(Bar)); // etc

            // test it...
            Echo echo = new Echo { Message = "Some message", ErrorMessage = "XXXXX" };
            MessageBase echo1;
            using (var ms = new MemoryStream())
            {
                Serializer.NonGeneric.Serialize(ms, echo);
                ms.Position = 0;
                echo1 = (MessageBase)Serializer.NonGeneric.Deserialize(typeof(MessageBase), ms);
            }
            Assert.AreSame(echo.GetType(), echo1.GetType());
            Assert.AreEqual(echo.ErrorMessage, echo1.ErrorMessage);
            Assert.AreEqual(echo.Message, ((Echo)echo1).Message);
        }
    }
}
