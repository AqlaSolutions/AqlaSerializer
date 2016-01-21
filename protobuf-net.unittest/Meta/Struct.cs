// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer.Serializers;
using AqlaSerializer.unittest.Serializers;
using AqlaSerializer.Compiler;
using System.IO;
namespace AqlaSerializer.unittest.Meta
{
    public struct CustomerStruct
    {
        private int id;
        public int Id { get { return id; } set { id = value; } }
        public string Name;
    }
    [TestFixture]
    public class TestCustomerStruct
    {
        [Test]
        public void RunStruct()
        {
            var model = AqlaSerializer.Meta.TypeModel.Create();
            var t = model.Add(typeof(CustomerStruct), false);
            t.AddField(1, "Id");
            t.AddField(2, "Name");

            CustomerStruct before = new CustomerStruct { Id = 123, Name = "abc" };
            CustomerStruct after = (CustomerStruct)model.DeepClone(before);
            Assert.AreEqual(before.Id, after.Id);
            Assert.AreEqual(before.Name, after.Name);
        }
    }
}
