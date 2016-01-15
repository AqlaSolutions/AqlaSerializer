// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.IO;
using NUnit.Framework;
using AqlaSerializer;


namespace Examples.Issues
{
    [TestFixture]
   public class Issue80
   {

/*===============================================================================================*/
        [ProtoBuf.ProtoContract]
        public class OmsMessage {
            public enum MessageType
            {
                None = 0,
                MSG_TYPE_CONFIRMATION = 1
            }
            [ProtoBuf.ProtoMember(1)]
            public MessageType message_type;
            [ProtoBuf.ProtoMember(2)]
            public string application_id;
            [ProtoBuf.ProtoMember(3)]
            public string symbol;
            [ProtoBuf.ProtoMember(4)]
            public string initial_qty;
            [ProtoBuf.ProtoMember(5)]
            public string limit_price;
            [ProtoBuf.ProtoMember(6)]
            public string last_fill_qty;
            [ProtoBuf.ProtoMember(7)]
            public string last_fill_price;
            [ProtoBuf.ProtoMember(8)]
            public string trader_id;

        }

       [Test]
       public void Execute()
       {
           int len32_1, len32_2, len128_1, len128_2;

           //CreateParams a proto message.
           OmsMessage omsMessage = new OmsMessage();

           omsMessage.message_type = OmsMessage.MessageType.MSG_TYPE_CONFIRMATION;
           omsMessage.application_id = "application_id";
           omsMessage.symbol = "symbol";
           omsMessage.initial_qty = "initial_qty";
           omsMessage.limit_price = "limit_price";
           omsMessage.last_fill_qty = "last_fill_qty";
           omsMessage.last_fill_price = "last_fill_price";
           omsMessage.trader_id = "trader_hid";

           MemoryStream textStream = new MemoryStream();

           AqlaSerializer.Serializer.SerializeWithLengthPrefix<OmsMessage>(textStream,
                omsMessage, AqlaSerializer.PrefixStyle.Fixed32);

           textStream.Position = 0;
           Assert.IsTrue(AqlaSerializer.Serializer.TryReadLengthPrefix(textStream.GetBuffer(), 0, 5, AqlaSerializer.PrefixStyle.Fixed32, out len32_1), "len32 - buffer");
           Assert.IsTrue(AqlaSerializer.Serializer.TryReadLengthPrefix(textStream, AqlaSerializer.PrefixStyle.Fixed32, out len32_2), "len32 - stream");

           textStream = new MemoryStream();

           AqlaSerializer.Serializer.SerializeWithLengthPrefix<OmsMessage>(textStream,
omsMessage, AqlaSerializer.PrefixStyle.Base128,0);

           textStream.Position = 0;
           Assert.IsTrue(AqlaSerializer.Serializer.TryReadLengthPrefix(textStream.GetBuffer(), 0, 5, AqlaSerializer.PrefixStyle.Base128, out len128_1), "len128 - buffer");
           Assert.IsTrue(AqlaSerializer.Serializer.TryReadLengthPrefix(textStream, AqlaSerializer.PrefixStyle.Base128, out len128_2), "len128 - stream");
           

           Assert.AreEqual(len32_1, len32_2, "len32 - stream vs buffer");
           Assert.AreEqual(len128_1, len128_2, "len128 - stream vs buffer");
           Assert.AreEqual(len128_1, len32_1, "len32 vs len128");
       }

   }
}