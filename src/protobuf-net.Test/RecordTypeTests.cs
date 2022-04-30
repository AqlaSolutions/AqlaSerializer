using ProtoBuf.Meta;
using NUnit.Framework;

namespace ProtoBuf.Test
{
    public class RecordTypeTests
    {
        public partial record PositionalRecord(string FirstName, string LastName, int Count);

        [Test]
        public void PositionalRecordTypeCtorResolve()
        {
            var ctor = MetaType.ResolveTupleConstructor(typeof(PositionalRecord), out var members);
            Assert.NotNull(ctor);
            Assert.AreEqual(3, members.Length);
            Assert.AreEqual(nameof(PositionalRecord.FirstName), members[0].Name);
            Assert.AreEqual(nameof(PositionalRecord.LastName), members[1].Name);
            Assert.AreEqual(nameof(PositionalRecord.Count), members[2].Name);
        }

        [Test]
        public void CanRoundTripPositionalRecord()
        {
            var obj = new PositionalRecord("abc", "def", 123);
            var model = RuntimeTypeModel.Create();
            var clone = model.DeepClone(obj);
            Assert.AreNotSame(obj, clone);
            Assert.AreEqual("abc", clone.FirstName);
            Assert.AreEqual("def", clone.LastName);
            Assert.AreEqual(123, clone.Count);
        }
    }
}
