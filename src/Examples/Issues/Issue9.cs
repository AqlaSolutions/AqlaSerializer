using System.Collections.Generic;
using System.IO;
using AqlaSerializer.Meta;
using NUnit.Framework;
using ProtoBuf;
using Serializer = AqlaSerializer.Serializer;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue9
    {
        [Test]
        public void Run()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                var rtm = RuntimeTypeModel.Create();
                rtm.Serialize(ms, new OuterList());
            }
        }

        [ProtoContract]
        public class OuterList
        {
            [ProtoMember(1)]
            public List<InnerList> InnerList = new List<InnerList>();
        }

        [ProtoContract(IgnoreListHandling = true)]
        public class InnerList : IEnumerable<int>
        {
            [ProtoMember(1)]
            public List<int> innerList = new List<int>();

            public IEnumerator<int> GetEnumerator()
            {
                return this.innerList.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }
    }
}