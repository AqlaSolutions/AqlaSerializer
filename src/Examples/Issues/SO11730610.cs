// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.Collections.Generic;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples.Issues
{
    [TestFixture]
    public class SO11730610
    {
        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(101, typeof(BaseClassInfo))]
        public interface IDataRow
        {
            [ProtoBuf.ProtoMember(1)]
            int Evil { get; set; }
        }


        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(101, typeof(SomeClassRow))]
        public class BaseClassInfo : IDataRow
        {
            [ProtoBuf.ProtoMember(1)]
            public int SomeId { get; set; }

            // this is serialized by the IDataRow "base"
            int IDataRow.Evil { get; set; }
        }

        // This version of the public class won't work because it defines the collection with the
        // strongly type name i.e. List<Some
        [ProtoBuf.ProtoContract]
        public class SomeResponse
        {
            [ProtoBuf.ProtoMember(1)]
            public List<SomeClassRow> Rows { get; set; }

            //...
        }

        // SomeClassRow is in turn an IDataRow.
        [ProtoBuf.ProtoContract]
        public class SomeClassRow : BaseClassInfo
        {
            [ProtoBuf.ProtoMember(1)]
            public string Name { get; set; }

            [ProtoBuf.ProtoMember(2)]
            public int Value { get; set; }

            //...  
        }

        [Test]
        public void Execute()
        {
            var resp = new SomeResponse
            {
                Rows = new List<SomeClassRow>
                {
                    new SomeClassRow { Name = "abc", SomeId = 123, Value = 456 },
                    new SomeClassRow { Name = "def", SomeId = 789, Value = 101112},
                    new SomeClassRow { Name = "ghi", SomeId = 131415, Value = 161718},
                }
            };

            ((IDataRow) resp.Rows[0]).Evil = 1;
            ((IDataRow) resp.Rows[1]).Evil = 2;
            ((IDataRow) resp.Rows[2]).Evil = 3;

            var clone = Serializer.DeepClone(resp);
            Assert.AreEqual(3, clone.Rows.Count);
            Assert.AreEqual(456, clone.Rows[0].Value);
            Assert.AreEqual("def", clone.Rows[1].Name);
            Assert.AreEqual(131415, clone.Rows[2].SomeId);

            Assert.AreEqual(1, ((IDataRow) resp.Rows[0]).Evil);
            Assert.AreEqual(2, ((IDataRow) resp.Rows[1]).Evil);
            Assert.AreEqual(3, ((IDataRow) resp.Rows[2]).Evil);
        }

        //// But, if the response is defined as follows then the WCF deserializes correcty and ALWAYS.
        //[ProtoBuf.ProtoContract]
        //public class SomeResponse
        //{
        //    [ProtoBuf.ProtoMember(1)]
        //    public List<IDataRow> Rows { get; set; }

        //    //...
        //}
    }
}
