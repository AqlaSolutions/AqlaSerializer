// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.IO;
using AqlaSerializer.Meta;
using AqlaSerializer.unittest.Serializers;

namespace AqlaSerializer.unittest
{
    [TestFixture]
    public class MultiTypeLookupTests
    {
        [Test]
        public void TestInt32RoundTrip()
        {
            var orig = PropertyValue.Create("abc", 123);
            var intClone = Serializer.DeepClone(orig);
            Assert.AreEqual("abc", intClone.Name);
            Assert.AreEqual(123, intClone.Value);
        }
        [Test]
        public void TestStringSerialize()
        {
            var prop = PropertyValue.Create("abc", "def");
            var tm0 = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
            tm0.DeepClone(prop);
            string hex;
            using (MemoryStream ms = new MemoryStream())
            {
                var tm = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
                tm.Serialize(ms, prop);
                hex = Util.GetHex(ms.ToArray());
            }
            Assert.AreEqual(
                "32" // field 6, string
              + "05" // 5 bytes
                + "0A" // field 1, string
                + "03" // 3 bytes
                  + "646566" // "def"
              + "0A" // field 1, string
              + "03" // 3 bytes
                + "616263" // "abc"
                , hex);
        }
        [Test]
        public void TestStringRoundTrip()
        {
            var stringClone = Serializer.DeepClone(
                PropertyValue.Create("abc", "def"));
            Assert.AreEqual("abc", stringClone.Name);
            Assert.AreEqual("def", stringClone.Value);

            Serializer.PrepareSerializer<PropertyValue>();

            stringClone = Serializer.DeepClone(
                PropertyValue.Create("abc", "def"));
            Assert.AreEqual("abc", stringClone.Name);
            Assert.AreEqual("def", stringClone.Value);
        }


        [ProtoBuf.ProtoContract]
        [ProtoBuf.ProtoInclude(5, typeof(PropertyValue<int>))]
        [ProtoBuf.ProtoInclude(6, typeof(PropertyValue<string>))]
        /* etc known types */
        public abstract class PropertyValue
        {
            public static PropertyValue<T> Create<T>(string name, T value)
            {
                return new PropertyValue<T> { Name = name, Value = value };
            }

            [ProtoBuf.ProtoMember(1)]
            public string Name { get; set; }

            public abstract object UntypedValue { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public sealed class PropertyValue<T> : PropertyValue
        {
            [ProtoBuf.ProtoMember(1)]
            public T Value { get; set; }

            public override object UntypedValue
            {
                get { return this.Value; }
                set { this.Value = (T)value; }
            }
        }
    }
    
}
