// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using AqlaSerializer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Examples.Issues
{
    [TestFixture]
    public class SO18663361
    {
        [Ignore("Not introduced with AqlaSerializer")]
        [Test]
        public void ExecuteFloat()
        {
            var obj = new MarketDataEntry_Float { EntryPrice = 123.45F };
            var clone = Serializer.DeepClone(obj);
            Assert.AreEqual(123.45F, clone.EntryPrice);
        }

        [Ignore("Not introduced with AqlaSerializer")]
        [Test]
        public void ExecuteDouble()
        {
            var obj = new MarketDataEntry_Double { EntryPrice = 123.45 };
            var clone = Serializer.DeepClone(obj);
            Assert.AreEqual(123.45, clone.EntryPrice);
        }

        [Ignore("Not introduced with AqlaSerializer")]
        [Test]
        public void ExecuteDecimal()
        {
            var obj = new MarketDataEntry_Decimal { EntryPrice = 123.45M };
            var clone = Serializer.DeepClone(obj);
            Assert.AreEqual(123.45M, clone.EntryPrice);
        }

        [Ignore("Not introduced with AqlaSerializer")]
        [Test]
        public void ExecuteFloat2()
        {
            var obj = new CreditMarketDataEntry_Float { EntryPrice = 123.45F };
            var clone = Serializer.DeepClone(obj);
            Assert.AreEqual(123.45F, clone.EntryPrice);
        }

        [Ignore("Not introduced with AqlaSerializer")]
        [Test]
        public void ExecuteDouble2()
        {
            var obj = new CreditMarketDataEntry_Double { EntryPrice = 123.45 };
            var clone = Serializer.DeepClone(obj);
            Assert.AreEqual(123.45, clone.EntryPrice);
        }

        [Ignore("Not introduced with AqlaSerializer")]
        [Test]
        public void ExecuteDecimal2()
        {
            var obj = new CreditMarketDataEntry_Decimal { EntryPrice = 123.45M };
            var clone = Serializer.DeepClone(obj);
            Assert.AreEqual(123.45M, clone.EntryPrice);
        }

        [global::System.Serializable, global::ProtoBuf.ProtoContract(Name = @"MarketDataEntry")]
        [ProtoBuf.ProtoInclude(1, typeof(CreditMarketDataEntry_Float))]
        public partial class MarketDataEntry_Float : global::AqlaSerializer.Extensible
        {
            // some other properties

            private float _EntryPrice;
            [global::ProtoBuf.ProtoMember(270, IsRequired = true, Name = @"EntryPrice", DataFormat = global::ProtoBuf.DataFormat.FixedSize)]
            public float EntryPrice
            {
                get { return _EntryPrice; }
                set { _EntryPrice = value; }
            }
        }
        [global::System.Serializable, global::ProtoBuf.ProtoContract(Name = @"MarketDataEntry")]
        [ProtoBuf.ProtoInclude(1, typeof(CreditMarketDataEntry_Double))]
        public partial class MarketDataEntry_Double : global::AqlaSerializer.Extensible
        {
            // some other properties

            private double _EntryPrice;
            [global::ProtoBuf.ProtoMember(270, IsRequired = true, Name = @"EntryPrice", DataFormat = global::ProtoBuf.DataFormat.FixedSize)]
            public double EntryPrice
            {
                get { return _EntryPrice; }
                set { _EntryPrice = value; }
            }
        }
        [global::System.Serializable, global::ProtoBuf.ProtoContract(Name = @"MarketDataEntry")]
        [ProtoBuf.ProtoInclude(1, typeof(CreditMarketDataEntry_Decimal))]
        public partial class MarketDataEntry_Decimal : global::AqlaSerializer.Extensible
        {
            // some other properties

            private decimal _EntryPrice;
            [global::ProtoBuf.ProtoMember(270, IsRequired = true, Name = @"EntryPrice", DataFormat = global::ProtoBuf.DataFormat.FixedSize)]
            public decimal EntryPrice
            {
                get { return _EntryPrice; }
                set { _EntryPrice = value; }
            }
        }

        [Serializable]
        [ProtoBuf.ProtoContract]
        public sealed class CreditMarketDataEntry_Double : MarketDataEntry_Double
        { }

        [Serializable]
        [ProtoBuf.ProtoContract]
        public sealed class CreditMarketDataEntry_Float : MarketDataEntry_Float
        { }

        [Serializable]
        [ProtoBuf.ProtoContract]
        public sealed class CreditMarketDataEntry_Decimal : MarketDataEntry_Decimal
        { }
    }
}
