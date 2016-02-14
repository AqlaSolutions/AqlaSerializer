// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using AqlaSerializer;

namespace Examples.TestNumbers
{
    [ProtoBuf.ProtoContract]
    public class NumRig
    {
        [ProtoBuf.ProtoMember(1, DataFormat=ProtoBuf.DataFormat.Default)]
        public int Int32Default { get; set; }
        [ProtoBuf.ProtoMember(2, DataFormat = ProtoBuf.DataFormat.ZigZag)]
        public int Int32ZigZag { get; set; }
        [ProtoBuf.ProtoMember(3, DataFormat = ProtoBuf.DataFormat.TwosComplement)]
        public int Int32TwosComplement { get; set; }
        [ProtoBuf.ProtoMember(4, DataFormat = ProtoBuf.DataFormat.FixedSize)]
        public int Int32FixedSize { get; set; }

        [ProtoBuf.ProtoMember(5, DataFormat = ProtoBuf.DataFormat.Default)]
        public uint UInt32Default { get; set; }
        [ProtoBuf.ProtoMember(7, DataFormat = ProtoBuf.DataFormat.TwosComplement)]
        public uint UInt32TwosComplement { get; set; }
        [ProtoBuf.ProtoMember(8, DataFormat = ProtoBuf.DataFormat.FixedSize)]
        public uint UInt32FixedSize { get; set; }

        [ProtoBuf.ProtoMember(9, DataFormat = ProtoBuf.DataFormat.Default)]
        public long Int64Default { get; set; }
        [ProtoBuf.ProtoMember(10, DataFormat = ProtoBuf.DataFormat.ZigZag)]
        public long Int64ZigZag { get; set; }
        [ProtoBuf.ProtoMember(11, DataFormat = ProtoBuf.DataFormat.TwosComplement)]
        public long Int64TwosComplement { get; set; }
        [ProtoBuf.ProtoMember(12, DataFormat = ProtoBuf.DataFormat.FixedSize)]
        public long Int64FixedSize { get; set; }

        [ProtoBuf.ProtoMember(13, DataFormat = ProtoBuf.DataFormat.Default)]
        public ulong UInt64Default { get; set; }
        [ProtoBuf.ProtoMember(15, DataFormat = ProtoBuf.DataFormat.TwosComplement)]
        public ulong UInt64TwosComplement { get; set; }
        [ProtoBuf.ProtoMember(16, DataFormat = ProtoBuf.DataFormat.FixedSize)]
        public ulong UInt64FixedSize { get; set; }
        
        [ProtoBuf.ProtoMember(17)]
        public string Foo { get; set; }
    }

    [ProtoBuf.ProtoContract]
    public class ZigZagInt32
    {
        [ProtoBuf.ProtoMember(1, DataFormat = ProtoBuf.DataFormat.ZigZag)]
        [DefaultValue(123456)]
        public int Foo { get; set; }
    }
    [ProtoBuf.ProtoContract]
    public class TwosComplementInt32
    {
        [ProtoBuf.ProtoMember(1, DataFormat = ProtoBuf.DataFormat.TwosComplement)]
        [DefaultValue(123456)]
        public int Foo { get; set; }
    }

    [ProtoBuf.ProtoContract]
    public class TwosComplementUInt32
    {
        [ProtoBuf.ProtoMember(1, DataFormat = ProtoBuf.DataFormat.TwosComplement)]
        public uint Foo { get; set; }
    }
    
    [ProtoBuf.ProtoContract]
    public class ZigZagInt64
    {
        [ProtoBuf.ProtoMember(1, DataFormat = ProtoBuf.DataFormat.ZigZag)]
        public long Foo { get; set; }
    }


    [TestFixture]
    public class SignTests
    {
        [Test]
        public void RoundTripBigPosativeZigZagInt64()
        {
            ZigZagInt64 obj = new ZigZagInt64 { Foo = 123456789 },
                clone = Serializer.DeepClone(obj);
            Assert.AreEqual(obj.Foo, clone.Foo);
        }

        [Test]
        public void RoundTripBigPosativeZigZagInt64ForDateTime()
        {
            // this test to simulate a typical DateTime value
            ZigZagInt64 obj = new ZigZagInt64 { Foo = 1216669168515 },
                clone = Serializer.DeepClone(obj);
            Assert.AreEqual(obj.Foo, clone.Foo);
        }
        
        [Test]
        public void RoundTripBigNegativeZigZagInt64() {
            ZigZagInt64 obj = new ZigZagInt64 { Foo = -123456789 },
                clone = Serializer.DeepClone(obj);
            clone = Serializer.DeepClone(obj);
            Assert.AreEqual(obj.Foo, clone.Foo);
        }
        [Test]
        public void TestSignTwosComplementInt32_0()
        {
            Assert.IsTrue(Program.CheckBytes(new TwosComplementInt32 { Foo = 0 }, 0x08, 0x00), "0");
        }
        [Test]
        public void TestSignTwosComplementInt32_Default()
        {
            Assert.IsTrue(Program.CheckBytes(new TwosComplementInt32 { Foo = 123456 }), "123456");
        }
        [Test]
        public void TestSignTwosComplementInt32_1()
        {
            Assert.IsTrue(Program.CheckBytes(new TwosComplementInt32 { Foo = 1 }, 0x08, 0x01), "+1");
        }
        [Test]
        public void TestSignTwosComplementInt32_2()
        {
            Assert.IsTrue(Program.CheckBytes(new TwosComplementInt32 { Foo = 2 }, 0x08, 0x02), "+2");
        }
        [Test]
        public void TestSignTwosComplementInt32_m1()
        {
            Assert.IsTrue(Program.CheckBytes(new TwosComplementInt32 { Foo = -1 }, 0x08, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01), "-1");
        }
        [Test]
        public void TestSignTwosComplementInt32_m2()
        {
            Assert.IsTrue(Program.CheckBytes(new TwosComplementInt32 { Foo = -2 }, 0x08, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01), "-2");
        }
        [Test]
        public void TestSignZigZagInt32_0()
        {
            Assert.IsTrue(Program.CheckBytes(new ZigZagInt32 { Foo = 0 }, 0x08, 0x00), "0");
        }
        [Test]
        public void TestSignZigZagInt32_Default()
        {
            Assert.IsTrue(Program.CheckBytes(new ZigZagInt32 { Foo = 123456 }), "123456");
        }
        [Test]
        public void TestSignZigZagInt32_1()
        {
            Assert.IsTrue(Program.CheckBytes(new ZigZagInt32 { Foo = 1 }, 0x08, 0x02), "+1");
        }
        [Test]
        public void TestSignZigZagInt32_2()
        {
            Assert.IsTrue(Program.CheckBytes(new ZigZagInt32 { Foo = 2 }, 0x08, 0x04), "+2");
        }
        [Test]
        public void TestSignZigZagInt32_m1()
        {
            Assert.IsTrue(Program.CheckBytes(new ZigZagInt32 { Foo = -1 }, 0x08, 0x01), "-1");
        }
        [Test]
        public void TestSignZigZagInt32_m2()
        {
            Assert.IsTrue(Program.CheckBytes(new ZigZagInt32 { Foo = -2 }, 0x08, 0x03), "-2");
        }        
        [Test]
        public void TestSignZigZagInt32_2147483647()
        {
            // encoding doc gives numbers in terms of uint equivalent
            ZigZagInt32 zz = new ZigZagInt32 { Foo = 2147483647 }, clone = Serializer.DeepClone(zz);
            Assert.AreEqual(zz.Foo, clone.Foo, "Roundtrip");
            TwosComplementUInt32 tc = Serializer.ChangeType<ZigZagInt32, TwosComplementUInt32>(zz);
            Assert.AreEqual(4294967294, tc.Foo);
        }
        [Test]
        public void TestSignZigZagInt32_m2147483648()
        {
            // encoding doc gives numbers in terms of uint equivalent
            ZigZagInt32 zz = new ZigZagInt32 { Foo = -2147483648 }, clone = Serializer.DeepClone(zz);
            Assert.AreEqual(zz.Foo, clone.Foo, "Roundtrip");
            TwosComplementUInt32 tc = Serializer.ChangeType<ZigZagInt32, TwosComplementUInt32>(zz);
            Assert.AreEqual(4294967295, tc.Foo);
        }
        
        [Test, ExpectedException(typeof(EndOfStreamException))]
        public void TestEOF()
        {
            Program.Build<ZigZagInt32>(0x08); // but no payload for field 1
        }
        
        [Test, ExpectedException(typeof(OverflowException))]
        public void TestOverflow()
        {
            Program.Build<ZigZagInt32>(0x08, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
        }

        [Test]
        public void SweepBitsInt32()
        {
            NumRig rig = new NumRig();
            const string SUCCESS = "bar";
            rig.Foo = SUCCESS; // to help test stream ending prematurely
            for (int i = 0; i < 32; i++)
            {
                int bigBit = i == 0 ? 0 : (1 << i - 1);
                for (int j = 0; j <= i; j++)
                {
                    int smallBit = 1 << j;
                    int val = bigBit | smallBit;
                    rig.Int32Default
                        = rig.Int32FixedSize
                        = rig.Int32TwosComplement
                        = rig.Int32ZigZag
                        = val;

                    NumRig clone = Serializer.DeepClone(rig);
                    Assert.AreEqual(val, rig.Int32Default);
                    Assert.AreEqual(val, rig.Int32FixedSize);
                    Assert.AreEqual(val, rig.Int32TwosComplement);
                    Assert.AreEqual(val, rig.Int32ZigZag);
                    Assert.AreEqual(SUCCESS, rig.Foo);
                }
            }
        }

        [Test]
        public void SweepBitsInt64KnownTricky()
        {
            try
            {
                int i = 31, j = 31;
                long bigBit = i == 0 ? 0 : (1 << i - 1);
                long smallBit = 1 << j;
                long val = bigBit | smallBit;
                NumRig rig = new NumRig();
                rig.Int64Default // 9 => 72
                    = rig.Int64FixedSize // 12 => 97?
                    = rig.Int64TwosComplement // 11 => 88
                    = rig.Int64ZigZag // 10 => 80
                    = val;
                const string SUCCESS = "bar";
                rig.Foo = SUCCESS; // to help test stream ending prematurely

                MemoryStream ms = new MemoryStream();
                Serializer.Serialize(ms, rig);
                byte[] raw = ms.ToArray();
                ms.Position = 0;
                NumRig clone = Serializer.Deserialize<NumRig>(ms);

                Assert.AreEqual(val, clone.Int64Default, "Default");
                Assert.AreEqual(val, clone.Int64FixedSize, "FixedSize");
                Assert.AreEqual(val, clone.Int64ZigZag, "ZigZag");
                Assert.AreEqual(val, clone.Int64TwosComplement, "TwosComplement");
                Assert.AreEqual(SUCCESS, clone.Foo, "EOF check");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.StackTrace);
                Assert.Fail(ex.Message);
            }
        }

        [Test]
        public void SweepBitsInt64()
        {
            NumRig rig = new NumRig();
            const string SUCCESS = "bar";
            rig.Foo = SUCCESS; // to help test stream ending prematurely
            for (int i = 0; i < 64; i++)
            {
                long bigBit = i == 0 ? 0 : (1 << i - 1);
                for (int j = 0; j <= i; j++)
                {
                    long smallBit = 1 << j;
                    long val = bigBit | smallBit;
                    rig.Int64Default
                        = rig.Int64FixedSize
                        = rig.Int64TwosComplement
                        = rig.Int64ZigZag
                        = val;

                    NumRig clone = Serializer.DeepClone(rig);
                    Assert.AreEqual(val, clone.Int64Default, "Default");
                    Assert.AreEqual(val, clone.Int64FixedSize, "FixedSize");
                    Assert.AreEqual(val, clone.Int64ZigZag, "ZigZag");
                    Assert.AreEqual(val, clone.Int64TwosComplement, "TwosComplement");
                    Assert.AreEqual(SUCCESS, clone.Foo, "EOF check: " + val.ToString());
                }
            }
        }
    }
}
