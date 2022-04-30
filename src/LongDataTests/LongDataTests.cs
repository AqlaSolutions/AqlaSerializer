using AqlaSerializer.Meta;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;


namespace AqlaSerializer.LongDataTests
{
    public class LongDataTests
    {
        [ProtoContract]
        public class MyModelInner
        {
            [ProtoMember(1)]
            public int Id { get; set; }

            [ProtoMember(2)]
            public string SomeString { get; set; }
            public override int GetHashCode()
            {
                int hash = -12323424;
                hash = (hash * -17) + Id.GetHashCode();
                hash = (hash * -17) + (SomeString?.GetHashCode() ?? 0);
                return hash;
            }
        }

        [ProtoContract]
        public class MyModelOuter
        {
            [ProtoMember(1)]
            public List<MyModelInner> Items { get; } = new List<MyModelInner>();

            public override int GetHashCode()
            {
                int hash = -12323424;
                if (Items != null)
                {
                    hash = (hash * -17) + Items.Count.GetHashCode();
                    foreach (var item in Items)
                    {
                        hash = (hash * -17) + (item?.GetHashCode() ?? 0);
                    }
                }
                return hash;
            }
        }

        [ProtoContract]
        public class MyModelWrapper
        {
            public override int GetHashCode()
            {
                int hash = -12323424;
                hash = (hash * -17) + (Group?.GetHashCode() ?? 0);
                return hash;
            }
            [ProtoMember(2, DataFormat = DataFormat.Group)]
            public MyModelOuter Group { get; set; }
        }
        static MyModelOuter CreateOuterModel(int count)
        {
            var obj = new MyModelOuter();
            for (int i = 0; i < count; i++)
                obj.Items.Add(new MyModelInner { Id = i, SomeString = "a long string that will be repeated lots and lots of times in the output data" });
            return obj;
        }

        private readonly ITestOutputHelper _output;

        public LongDataTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Test]//(Skip="long running")]
        public void CanSerializeLongData()
        {
            _output.WriteLine($"PID: {Process.GetCurrentProcess().Id}");
            const string path = "large.data";
            var watch = Stopwatch.StartNew();
            const int COUNT = 50000000;

            _output.WriteLine($"Creating model with {COUNT} items...");
            var outer = CreateOuterModel(COUNT);
            watch.Stop();
            _output.WriteLine($"Created in {watch.ElapsedMilliseconds}ms");

            var model = new MyModelWrapper { Group = outer };
            int oldHash = model.GetHashCode();
            var rtm = TypeModel.Create();
            
            rtm.Add(typeof(MyModelOuter), true);
            rtm.Add(typeof(MyModelInner), true);
            rtm.Add(typeof(MyModelWrapper), true);
            rtm.CompileInPlace();
            //if (false)
            {
                using (var file = File.Create(path))
                {
                    Console.Write("Serializing...");
                    watch = Stopwatch.StartNew();
                    rtm.Serialize(file, model);
                    watch.Stop();
                    _output.WriteLine($"Wrote: {COUNT} in {file.Length >> 20} MiB ({file.Length / COUNT} each), {watch.ElapsedMilliseconds}ms");
                }
            }
            using (var file = File.OpenRead(path))
            {
                _output.WriteLine($"Deserializing {file.Length >> 20} MiB");
                watch = Stopwatch.StartNew();
                var clone = rtm.Deserialize<MyModelWrapper>(file);
                watch.Stop();
                var newHash = clone.GetHashCode();
                _output.WriteLine($"{oldHash} vs {newHash}, {newHash == oldHash}, {watch.ElapsedMilliseconds}ms");
                Assert.AreEqual(oldHash, newHash);
            }
        }
    }
}
