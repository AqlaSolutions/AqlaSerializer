// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using AqlaSerializer.Meta;
using System.IO;
using AqlaSerializer;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue184
    {
        [Test]
        public void CanCreateUsableEnumerableMetaType()
        {
            var model = TypeModel.Create();
            model.Add(typeof(IEnumerable<int>), false);
            model.CompileInPlace();
        }
        [Test]
        public void CantCreateMetaTypeForInbuilt()
        {
            var ex = Assert.Throws<ArgumentException>(() => {
                var model = TypeModel.Create();
                model.Add(typeof(decimal), false);
                model.CompileInPlace();
            });
            Assert.That(ex.Message, Is.EqualTo("Data of this type has inbuilt behaviour, and cannot be added to a model in this way: System.Decimal"));
        }
        [Test]
        public void CantSurrogateLists()
        {
            var ex = Assert.Throws<ArgumentException>(() => {
                var model = TypeModel.Create();
                model.Add(typeof(IList<int>), false).SetSurrogate(typeof(InnocentType));
                model.CompileInPlace();
            });
            StringAssert.StartsWith("Repeated data (a list, collection, etc) has inbuilt behaviour and cannot use a surrogate (System.Collections.Generic.IList`1[[System.Int32, ", ex.Message);
        }
        [Test]
        public void ListAsSurrogate()
        {
            var ex = Assert.Throws<ArgumentException>(() => {
                var model = TypeModel.Create();
                model.Add(typeof(IMobileObject), false).SetSurrogate(typeof(MobileList<int>));
                model.CompileInPlace();
            });
            Assert.That(ex.Message, Is.EqualTo("Repeated data (a list, collection, etc) has inbuilt behaviour and cannot be used as a surrogate"));
        }


        public interface IMobileObject { }
        public class InnocentType // nothing that looks like a list
        {
            
        }
        public class MobileList<T> : List<T>, IMobileObject
        {
            public override bool Equals(object obj) { return this.SequenceEqual((IEnumerable<T>)obj); }
            public override int GetHashCode()
            {
                return 0; // not being used in a dictionary - to heck with it
            }
        }
        [ProtoBuf.ProtoContract]
        public class A : IMobileObject
        {
            [ProtoBuf.ProtoMember(1)]
            public int X { get; set; }
            public override bool Equals(object obj) { return ((A)obj).X == X; }
            public override int GetHashCode()
            {
                return 0; // not being used in a dictionary - to heck with it
            }
            public override string ToString()
            {
                return X.ToString();
            }
        }
        [ProtoBuf.ProtoContract]
        public class B
        {
            [ProtoBuf.ProtoMember(1)]
            public List<IMobileObject> Objects { get; set; }
        }


    }
}
