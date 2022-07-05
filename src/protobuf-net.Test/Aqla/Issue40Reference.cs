using AqlaSerializer;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace aqlaserializer.unittest.Aqla
{
    [TestFixture]
    public class Issue40Reference
    {
        private class Root
        {
            private UniqueIds2 _uniqueIds2;
            private Information _inputField;

            public UniqueIds2 InstrumentIds { get => _uniqueIds2; set => _uniqueIds2 = value; }
            public Information InputField { get => _inputField; set => _inputField = value; }
        }

        private sealed class Information
        {
            private object _data;

            public object Data { get => _data; set => _data = value; }
        }

        private class UniqueIds2
        {
        }

        [Test]
        public void Test()
        {
            var rtm = RuntimeTypeModel.Create();
            var metaType1 = rtm.Add(typeof(Root), false);
            metaType1.DefaultFormat = ValueFormat.Reference;
            metaType1.UseConstructor = false;
            metaType1.IgnoreListHandling = true;

            var metaType2 = rtm.Add(typeof(Information), false);
            metaType2.DefaultFormat = ValueFormat.Reference;
            metaType2.UseConstructor = false;
            metaType2.IgnoreListHandling = true;

            AqlaSerializer.Meta.MetaType metaType3 = rtm[typeof(Information)];
            var metaField1 = metaType3.AddField(1, "_data");
            metaField1.SetSettings(x => { x.V.Format = ValueFormat.Reference; });
            metaField1.SetSettings(x => x.V.WriteAsDynamicType = true, 0);

            AqlaSerializer.Meta.MetaType metaType4 = rtm[typeof(Root)];
            var metaField2 = metaType4.AddField(1, "_inputField");
            metaField2.SetSettings(x => { x.V.Format = ValueFormat.Reference; });

            var metaType5 = rtm.Add(typeof(UniqueIds2), false);
            metaType5.DefaultFormat = ValueFormat.Reference;
            metaType5.UseConstructor = false;
            metaType5.IgnoreListHandling = true;

            AqlaSerializer.Meta.MetaType metaType6 = rtm[typeof(UniqueIds2)];

            AqlaSerializer.Meta.MetaType metaType8 = rtm[typeof(Root)];
            var metaField5 = metaType8.AddField(2, "_uniqueIds2");
            metaField5.SetSettings(x => { x.V.Format = ValueFormat.Reference; });

            var inst = new Root();
            inst.InstrumentIds = new UniqueIds2();

            var columnData = new Information();
            columnData.Data = "test";
            inst.InputField = columnData;

            rtm.DeepClone(inst);
        }

    }
}
