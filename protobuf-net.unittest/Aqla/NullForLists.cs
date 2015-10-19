using System.Collections.Generic;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class NullForLists
    {
        [SerializableType]
        public class ArrayContainerClass
        {
            public Data[] Data { get; set; }
            public int[] Data2 { get; set; }
        }

        [SerializableType]
        public class ListContainerClass
        {
            public List<Data> Data { get; set; }
            public List<int> Data2 { get; set; }
        }

        [SerializableType]
        public class Data
        {
            public int PublicProperty { get; set; }
            public int PublicProperty2 { get; set; }
        }

        RuntimeTypeModel _model;

        [Test]
        public void TestEmptyArrays([Values(false, true)] bool compiled)
        {
            _model = TypeModel.Create();
            _model.AutoCompile = compiled;
            var obj = new ArrayContainerClass() { Data = new Data[0], Data2 = new int[0] };
            var clone = _model.DeepClone(obj);

            Assert.IsNotNull(clone.Data);
            Assert.AreEqual(0, clone.Data.Length);
            Assert.AreEqual(0, clone.Data2.Length);
        }

        [Test]
        public void TestNonEmptyArrays([Values(false, true)] bool compiled)
        {
            _model = TypeModel.Create();
            _model.AutoCompile = compiled;
            var obj = new ArrayContainerClass() { Data = new[] { new Data() { PublicProperty = 234, PublicProperty2 = 567 } }, Data2 = new[] { 1 } };
            var clone = _model.DeepClone(obj);

            Assert.IsNotNull(clone.Data);
            Assert.AreEqual(1, clone.Data.Length);
            Assert.AreEqual(1, clone.Data2.Length);
        }

        [Test]
        public void TestNullArrays([Values(false, true)] bool compiled)
        {
            _model = TypeModel.Create();
            _model.AutoCompile = compiled;
            var obj = new ArrayContainerClass();
            var clone = _model.DeepClone(obj);

            Assert.IsNull(clone.Data);
            Assert.IsNull(clone.Data2);
        }

        [Test]
        public void TestEmptyLists([Values(false, true)] bool compiled)
        {
            _model = TypeModel.Create();
            _model.AutoCompile = compiled;
            var obj = new ListContainerClass() { Data = new List<Data>(), Data2 = new List<int>() };
            var clone = _model.DeepClone(obj);

            Assert.IsNotNull(clone.Data);
            Assert.AreEqual(0, clone.Data.Count);
            Assert.AreEqual(0, clone.Data2.Count);
        }

        [Test]
        public void TestNullLists([Values(false, true)] bool compiled)
        {
            _model = TypeModel.Create();
            _model.AutoCompile = compiled;
            var obj = new ListContainerClass();
            var clone = _model.DeepClone(obj);

            Assert.IsNull(clone.Data);
            Assert.IsNull(clone.Data2);
        }
    }
}