using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NUnit.Framework;
using ProtoBuf;
using ProtoBuf.Meta;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class Issue30SurrogateTest
    {
        private MemoryStream _objectBytes;
        private AqlaSerializer.Meta.RuntimeTypeModel _model;
        
        [Test]
        public void Execute()
        {
            _model = AqlaSerializer.Meta.TypeModel.Create();
            _model.Add(typeof(Test), false).SetSurrogate(typeof(TestSurrogate));
            //_model.Add(typeof(Test), false).Add("Data", "Parent");
            Serialize();
            Deserialize();
        }

        private void Deserialize()
        {
            _objectBytes.Position = 0;
            Test test = (Test)_model.Deserialize(_objectBytes, null, typeof(Test));
            Console.WriteLine("Deserialized: {0}", test.Data);
        }

        private void Serialize()
        {
            _objectBytes = new MemoryStream();
            Test test1 = new Test();
            test1.Data = 45;
            Test test2 = new Test();
            test2.Data = 32;
            test1.Parent = test2;
            _model.Serialize(_objectBytes, test1);
        }
    }

    public class Test
    {
        public int Data;
        public Test Parent;
    }

    [ProtoContract(AsReferenceDefault = true)]
    public class TestSurrogate
    {
        [ProtoMember(1)]
        int Data;

        [ProtoMember(2, AsReference = true)]
        Test Parent = null;

        public static explicit operator Test(TestSurrogate surrogate)
        {
            if (surrogate == null) return null;
            Test test = new Test();
            test.Data = surrogate.Data;
            test.Parent = surrogate.Parent;
            return test;
        }

        public static explicit operator TestSurrogate(Test original)
        {
            if (original == null) return null;
            TestSurrogate surrogate = new TestSurrogate();
            surrogate.Data = original.Data;
            surrogate.Parent = original.Parent;
            return surrogate;
        }
    }
}