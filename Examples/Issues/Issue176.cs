// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DAL;

using NUnit.Framework;

using AqlaSerializer.Meta;
using System.Data.Linq;
using System.Collections;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue176
    {
        [Test]
        public void TestOrderLineGetDeserializedAndAttachedToOrder([Values(false,true)] bool compile)
        {
            byte[] fileBytes = File.ReadAllBytes(@"NWind\nwind.proto.bin");

            RuntimeTypeModel ordersModel = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
            ordersModel.AutoCompile = false;
            
            Database database = (Database)ordersModel.Deserialize(new MemoryStream(fileBytes), null, typeof(Database));
            List<Order> orders = database.Orders;

            ordersModel.AutoCompile = compile;
            ordersModel.SkipCompiledVsNotCheck = true;

            DbMetrics("From File", orders);

            var roundTrippedOrders = (List<Order>)ordersModel.DeepClone(orders);
            Assert.AreNotSame(orders, roundTrippedOrders);
            DbMetrics("Round trip", roundTrippedOrders);
            Assert.AreEqual(orders.SelectMany(o => o.Lines).Count(),
                roundTrippedOrders.SelectMany(o => o.Lines).Count(), "total count");
        }

        static void DbMetrics(string caption, IList<Order> orders)
        {
            int count = orders.Count();
            int lines = orders.SelectMany(ord => ord.Lines).Count();
            int totalQty = orders.SelectMany(ord => ord.Lines)
                    .Sum(line => line.Quantity);
            decimal totalValue = orders.SelectMany(ord => ord.Lines)
                    .Sum(line => line.Quantity * line.UnitPrice);

            Console.WriteLine("{0}\torders {1}; lines {2}; units {3}; value {4:C}",
                              caption, count, lines, totalQty, totalValue);
        }

    }
}
