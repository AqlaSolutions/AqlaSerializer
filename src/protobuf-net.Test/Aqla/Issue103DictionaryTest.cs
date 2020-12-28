using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using AqlaSerializer.Meta;
using NUnit.Framework;
using ProtoBuf;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class Issue103DictionaryTest
    {
        [ProtoContract]
        public class foo
        {
            [ProtoMember(1)]
            public Dictionary<string, string> bar { get; set; }
        }

        [Test]
        public void Test()
        {
            var protoObj = new foo
            {
                bar = new Dictionary<string, string>
                {
                    { "a", "1" },
                    { "b", "2" }
                }
            };

            var protoWorker = AqlaSerializer.Meta.TypeModel.Create();
            var model = protoWorker.Add(
                typeof(foo),
                true);

            model.GetFields()[0].SupportNull = true;

            var m = new MemoryStream();

            protoWorker.Serialize(
                m,
                protoObj);

            m.Position = 0;

            var retVal = (foo)protoWorker.Deserialize(
                m,
                null,
                typeof(foo));

            foreach (var entry in retVal.bar)
            {
                Debug.WriteLine(
                    string.Format(
                        "{0} - {1}",
                        entry.Key,
                        entry.Value));
            }
        }
    }
}