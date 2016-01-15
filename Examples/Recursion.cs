// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.IO;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples
{
    [ProtoBuf.ProtoContract]
    public class RecursiveObject
    {
        [ProtoBuf.ProtoMember(1)]
        public RecursiveObject Yeuch { get; set; }
    }
    [TestFixture]
    public class Recursion
    {
        [Test, ExpectedException(typeof(ProtoException))]
        public void BlowUp()
        {
            RecursiveObject obj = new RecursiveObject();
            obj.Yeuch = obj;
            Serializer.Serialize(Stream.Null, obj);
        }
    }
}
