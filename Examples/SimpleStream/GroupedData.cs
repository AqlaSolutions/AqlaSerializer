﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.Collections.Generic;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using Examples.Ppt;

namespace Examples.SimpleStream
{
    [ProtoBuf.ProtoContract]
    class NoddyExtends : Extensible { }

    [ProtoBuf.ProtoContract]
    class Noddy
    {
        [ProtoBuf.ProtoMember(2)]
        public int Foo { get; set; }
    }

    [TestFixture]
    public class GroupedData
    {
        [Test]
        public void TestGroup()
        {
            Test3 t3 = Program.Build<Test3>(0x1B, 0x08, 0x96, 0x01, 0x1C);// [start group 3] [test1] [end group 3]
            Assert.AreEqual(150, t3.C.A);
        }
        
        [Test]
        public void TestGroupAsExtension()
        {
            NoddyExtends ne = Program.Build<NoddyExtends>(0x1B, 0x08, 0x96, 0x01, 0x1C);// [start group 3] [test1] [end group 3]

            Assert.IsTrue(Program.CheckBytes(ne, 0x1B, 0x08, 0x96, 0x01, 0x1C), "Round trip");
            var tm = TypeModel.Create(false, ProtoCompatibilitySettings.FullCompatibility);
            
            Test1 t1 = Extensible.GetValue<Test1>(tm, ne, 3);
            Assert.IsNotNull(t1, "Got an object?");
            Assert.AreEqual(150, t1.A, "Value");
        }

        [Test]
        public void TestGroupIgnore()
        {
            // 0x1B = 11 011 = start group 3
            // 0x08 = 1000 = varint 1
            // 0x96 0x01 = 10010110 = 150
            // 0x1c = 011 100 = end group 3
            // 0x10 = 10 000 = varint 2
            // 0x96 0x01 = 10010110 = 150
            Noddy no = Program.Build<Noddy>(0x1B, 0x08, 0x96, 0x01, 0x1C, 0x10, 0x96, 0x01);
            Assert.AreEqual(150, no.Foo);
        }

        [Test, ExpectedException(typeof(ProtoException))]
        public void TestUnterminatedGroup()
        {
            Test3 t3 = Program.Build<Test3>(0x1B, 0x08, 0x96, 0x01 );// [start group 3] [test1]
        }
        [Test, ExpectedException(typeof(ProtoException))]
        public void TestWrongGroupClosed()
        {
            Test3 t3 = Program.Build<Test3>( 0x1B, 0x08, 0x96, 0x01, 0x24 );// [start group 3] [test1] [end group 4]
        }

        [ProtoBuf.ProtoContract]
        class Test3List
        {
            [ProtoBuf.ProtoMember(3)]
            public List<Test1> C { get; set; }
        }

        [ProtoBuf.ProtoContract]
        class Test1List
        {
            [ProtoBuf.ProtoMember(1)]
            public List<int> A { get; set; }
        }
        
        [Test]
        public void TestEntityList()
        {
            Test3List t3 = Program.Build<Test3List>(
                0x1B, 0x08, 0x96, 0x01, 0x1C, // start 3: A=150; end 3
                0x1B, 0x08, 0x82, 0x01, 0x1C);// start 3: A=130; end 3
            Assert.AreEqual(2, t3.C.Count);
            Assert.AreEqual(150, t3.C[0].A);
            Assert.AreEqual(130, t3.C[1].A);
        }

        [Test, ExpectedException(typeof(ProtoException))]
        public void TestPrimativeList()
        {
            Test1List t1 = Program.Build<Test1List>(0x0B, 0x96, 0x01, 0x0C); // [start:1] [150] [end:1]
        }
    }
}
