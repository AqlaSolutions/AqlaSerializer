// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer.Serializers;
namespace AqlaSerializer.unittest.Serializers
{
    [TestFixture]
    public class Tag
    {
        [Test]
        public void TestBasicTags()
        {

            Util.Test("abc", nil => new TagDecorator(1, WireType.String, false, nil), "0A");
        }
    }
}
