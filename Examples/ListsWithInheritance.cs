// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System.Collections.Generic;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples
{
    [TestFixture]
    public class ListsWithInheritance
    {
        [Test]
        public void TestBasicRoundtripViaDataClass()
        {
            Data data = new Data();
            data.Parties.Add(new Debtor());
            data.Parties.Add(new Party());
            data.Parties.Add(new Creditor());
            var clone = Serializer.DeepClone(data);

            Assert.AreEqual(3, clone.Parties.Count);
            Assert.AreEqual(typeof(Debtor), clone.Parties[0].GetType());
            Assert.AreEqual(typeof(Party), clone.Parties[1].GetType());
            Assert.AreEqual(typeof(Creditor), clone.Parties[2].GetType());
        }

        [Test]
        public void TestBasicRoundtripOfNakedList()
        {
            var list = new List<Party>();
            list.Add(new Debtor());
            list.Add(new Party());
            list.Add(new Creditor());
            var clone = Serializer.DeepClone(list);

            Assert.AreEqual(3, clone.Count);
            Assert.AreEqual(typeof(Debtor), clone[0].GetType());
            Assert.AreEqual(typeof(Party), clone[1].GetType());
            Assert.AreEqual(typeof(Creditor), clone[2].GetType());
        }

        [ProtoBuf.ProtoContract]
        public class Data
        {
            [ProtoBuf.ProtoMember(1)]
            public List<Party> Parties { get { return parties; } }

            private readonly List<Party> parties = new List<Party>();
        }

        [ProtoBuf.ProtoContract]
        [ProtoBuf.ProtoInclude(1, typeof(Party))]
        public class BaseClass
        {
        }
        [ProtoBuf.ProtoContract]
        [ProtoBuf.ProtoInclude(1, typeof(Creditor))]
        [ProtoBuf.ProtoInclude(2, typeof(Debtor))]
        public class Party : BaseClass
        {
        }
        [ProtoBuf.ProtoContract]
        public class Creditor : Party
        {
        }
        [ProtoBuf.ProtoContract]
        public class Debtor : Party
        {
        }
    }
}
