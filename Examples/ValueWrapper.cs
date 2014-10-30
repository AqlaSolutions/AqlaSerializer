﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;

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
            set { Value = value; }
        }
        [ProtoBuf.ProtoMember(2)]
        private float? ValueSingle
        {
            get { return Get<float>(); }
            set { Value = value; }
        }
        [ProtoBuf.ProtoMember(3)]
        private double? ValueDouble
        {
            get { return Get<double>(); ; }
            set { Value = value; }
        }
        // etc for expected types
    }

    [ProtoBuf.ProtoContract]
    class Int32Simple
    {
        [ProtoBuf.ProtoMember(1)]
        public int Value { get; set; }
    }
    [ProtoBuf.ProtoContract]
    class SingleSimple
    {
        [ProtoBuf.ProtoMember(2)]
        public float Value { get; set; }
    }
    [ProtoBuf.ProtoContract]
    class DoubleSimple
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
            Serializer.Serialize(ms, item);
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
            Serializer.Serialize(ms, item);
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

            Assert.IsTrue(Program.CheckBytes(new FieldDataViaNullable {Value = 123},
                                             GetBytes(new Int32Simple {Value = 123})), "Int32");
            Assert.AreEqual(123, Serializer.DeepClone(new FieldDataViaNullable(123)).Value);
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
