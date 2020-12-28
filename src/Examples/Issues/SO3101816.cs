// Modified by Vladyslav Taranov for AqlaSerializer, 2016

//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using NUnit.Framework;
//using AqlaSerializer;
//using AqlaSerializer.Meta;

//namespace Examples.Issues
//{
//    [TestFixture]
//    public class SO3101816
//    {
//        [ProtoBuf.ProtoContract]
//        public class A
//        {
//            [ProtoBuf.ProtoMember(1)]
//            public IB B { get; set; }
//        }

//        public interface IB
//        {
//        }

//        [ProtoBuf.ProtoContract]
//        public class B : IB
//        {
//            [ProtoBuf.ProtoMember(1)]
//            public int SomeProperty { get; set; }
//        }


//        [Test]
//        public void Test()
//        {
//            var a = new A { B = new B() };
//            var model = TypeModel.Create();
//            model.Add(typeof(B), )
//            using (var m = new MemoryStream())
//            {
//                Serializer.NonGeneric.Serialize(, a);
//            }
//        }
//    }
//}
