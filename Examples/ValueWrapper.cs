// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Examples
{
    [ProtoBuf.ProtoContract]
    public class FieldData
    {
        public FieldData() {}
        public FieldData(object value) {
            Value = value;}
        public object Value { get; set; }

        private bool Is<T>() {
            return Value != null && Value is T;
        }
        private T Get<T>() {
            return Is<T>() ? (T) Value : default(T);
        }
        [ProtoBuf.ProtoMember(1)]
        private int ValueInt32
        {
            get { return Get<int>(); }
            set { Value = value; }
        }
        private bool ValueInt32Specified { get { return Is<int>(); } }

        [ProtoBuf.ProtoMember(2)]
        private float ValueSingle
        {
            get { return Get<float>();}
            set { Value = value; }
        }
        private bool ValueSingleSpecified { get { return Is<float>(); } }

        [ProtoBuf.ProtoMember(3)]
        private double ValueDouble
        {
            get { return Get<double>(); ; }
            set { Value = value; }
        }
        private bool ValueDoubleSpecified { get { return Is<double>(); } }

        // etc for expected types
    }

    [ProtoBuf.ProtoContract]
    public class FieldDataViaNullable
    {
        public FieldDataViaNullable() { }
        public FieldDataViaNullable(object value)
        {
            Value = value;
        }
        public object Value { get; set; }

        private T? Get<T>() where T : struct
        {
            return (Value != null && Value is T) ? (T?)Value : (T?)null;
        }
        [ProtoBuf.ProtoMember(1)]
        private int? ValueInt32
        {
            get { return Get<int>(); }
            // you should not cause side effects in setters:
            // null *will* be assigned here if null support is enabled
            // so check previousValue (null) != newValue (null)
            // deserializer can't each time get old value and check property -
            // it's both ineffficient and may have even worse side effects
            // like breaking compatibility with datacontract
            set { if (ValueInt32 != value) Value = value; }
        }
        [ProtoBuf.ProtoMember(2)]
        private float? ValueSingle
        {
            get { return Get<float>(); }
            set { if (ValueSingle != value) Value = value; }
        }
        [ProtoBuf.ProtoMember(3)]
        private double? ValueDouble
        {
            get { return Get<double>(); ; }
            set { if (ValueDouble != value) Value = value; }
        }
        // etc for expected types
    }

    [ProtoBuf.ProtoContract]
    public class Int32Simple
    {
        [ProtoBuf.ProtoMember(1)]
        public int Value { get; set; }
    }
    [ProtoBuf.ProtoContract]
    public class SingleSimple
    {
        [ProtoBuf.ProtoMember(2)]
        public float Value { get; set; }
    }
    [ProtoBuf.ProtoContract]
    public class DoubleSimple
    {
        [ProtoBuf.ProtoMember(3)]
        public double Value { get; set; }
    }

    [TestFixture]
    public class ValueWrapperTests
    {
        static byte[] GetBytes<T>(T item)
        {
            MemoryStream ms = new MemoryStream();
            var tm = TypeModel.Create(false, ProtoCompatibilitySettings.FullCompatibility);
            tm.Serialize(ms, item);
            return ms.ToArray();
        }
        [Test]
        public void TestRaw()
        {
            Assert.IsTrue(Program.CheckBytes(new FieldData()), "Empty");
            Assert.AreEqual(null, Serializer.DeepClone(new FieldData()).Value);

        }
        [Test]
        public void TestInt32()
        {

            Assert.IsTrue(Program.CheckBytes(new FieldData {Value = 123},
                                             GetBytes(new Int32Simple {Value = 123})), "Int32");
            Assert.AreEqual(123, Serializer.DeepClone(new FieldData(123)).Value);
        }
        [Test]
        public void TestSingle()
        {
            Assert.IsTrue(Program.CheckBytes(new FieldData {Value = 123.45F},
                                             GetBytes(new SingleSimple {Value = 123.45F})), "Single");
            Assert.AreEqual(123.45F, Serializer.DeepClone(new FieldData(123.45F)).Value);

        }
        [Test]
        public void TestDouble()
        {
            Assert.IsTrue(Program.CheckBytes(new FieldData { Value = 123.45 },
                GetBytes(new DoubleSimple { Value = 123.45 })), "Double");
            Assert.AreEqual(123.45, Serializer.DeepClone(new FieldData(123.45)).Value);
        }

    }

    [TestFixture]
    public class ValueWrapperTestsViaNullable
    {
        static byte[] GetBytes<T>(T item)
        {
            MemoryStream ms = new MemoryStream();
            var tm = TypeModel.Create(false, ProtoCompatibilitySettings.FullCompatibility);
            tm.Serialize(ms, item);
            return ms.ToArray();
        }
        [Test]
        public void TestRaw()
        {
            Assert.IsTrue(Program.CheckBytes(new FieldDataViaNullable()), "Empty");
            Assert.AreEqual(null, Serializer.DeepClone(new FieldDataViaNullable()).Value);

        }

        [Test]
        public void TestInt32()
        {

            Assert.IsTrue(Program.CheckBytes(new FieldDataViaNullable { Value = 123 },
                                             GetBytes(new Int32Simple { Value = 123 })), "Int32");
            FieldDataViaNullable copy = Serializer.DeepClone(new FieldDataViaNullable(123));
            Assert.AreEqual(123, copy.Value);
        }
        [Test]
        public void TestSingle()
        {
            Assert.IsTrue(Program.CheckBytes(new FieldDataViaNullable {Value = 123.45F},
                                             GetBytes(new SingleSimple {Value = 123.45F})), "Single");
            Assert.AreEqual(123.45F, Serializer.DeepClone(new FieldDataViaNullable(123.45F)).Value);

        }
        [Test]
        public void TestDouble()
        {
            Assert.IsTrue(Program.CheckBytes(new FieldDataViaNullable { Value = 123.45 },
                GetBytes(new DoubleSimple { Value = 123.45 })), "Double");
            Assert.AreEqual(123.45, Serializer.DeepClone(new FieldDataViaNullable(123.45)).Value);
        }

    }
}
