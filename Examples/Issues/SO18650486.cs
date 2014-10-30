// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;

namespace Examples.Issues
{

    [TestFixture]
    public class SO18650486
    {
        [Test]
        public void Execute()
        {
#if DEBUG
            const int OuterLoop = 5;
#else
            const int OuterLoop = 500;
#endif
            var model = TypeModel.Create();
            model.AutoCompile = false;
            // Execute(OuterLoop, model, "RT");
            model.CompileInPlace();
            Execute(OuterLoop, model, "CIP");
            Execute(OuterLoop, model.Compile(), "C");
            var ext = model.Compile("SO18650486", "SO18650486.dll");
            PEVerify.AssertValid("SO18650486.dll");
            Execute(OuterLoop, ext, "EXT");
        }
        private static void Execute(int count, TypeModel model, string caption)
        {
            const int InnerLoop = 1000;
            object lockObj = new object();
            var average = 0d;
            var min = double.MaxValue;
            var max = double.MinValue;
            int complete = 0;
            model.DeepClone(Create()); // warm-up
            Parallel.For(0, count, i =>
            {
                var classThree = Create();
                var counter = Stopwatch.StartNew();
                using (var ms = new MemoryStream())
                {
                    for (int j = 0; j < InnerLoop; j++)
                    {
                        ms.SetLength(0);
                        model.Serialize(ms, classThree);
                        ms.Position = 0;
                        var des = model.Deserialize(ms, null, typeof(ClassThree));
                        var aaa = des;
                    }
                    counter.Stop();
                }
                

                var elapsed = counter.Elapsed.TotalMilliseconds;
                double currentAverage;
                lock (lockObj)
                {
                    complete++;
                    average += elapsed;
                    var oldMin = min;
                    min = Math.Min(min, elapsed);
                    max = Math.Max(max, elapsed);
                    currentAverage = average / complete;
                    if (min != oldMin || (complete % 500) == 0)
                    {
                        Trace.WriteLine(string.Format("{5}\tCycle {0}: {1:N2} ms - avg: {2:N2} ms - min: {3:N2} - max: {4:N2}", i, elapsed, currentAverage, min, max, caption));
                    }
                }                
            });
            Trace.WriteLine(string.Format("{5}\tComplete {0}: avg: {2:N2} ms - min: {3:N2} - max: {4:N2}", complete, 0, average / complete, min, max, caption));
        }
        public enum EnumOne
        {
            One = 1,
            Two = 2,
            Three = 3
        }

        [Flags]
        public enum EnumTwo
        {
            One = 1,
            Two = 2,
            Three = 4
        }

        [ProtoBuf.ProtoContract, ProtoBuf.ProtoInclude(51, typeof(ClassSix))]
        public class ClassOne
        {
            // properties

            [ProtoBuf.ProtoMember(1)]
            public int p_i1 { set; get; }

            [ProtoBuf.ProtoMember(2)]
            public uint p_i2 { set; get; }

            [ProtoBuf.ProtoMember(3)]
            public long p_l1 { set; get; }

            [ProtoBuf.ProtoMember(4)]
            public ulong p_l2 { set; get; }

            [ProtoBuf.ProtoMember(5)]
            public string p_s { set; get; }

            [ProtoBuf.ProtoMember(6)]
            public float p_f { set; get; }

            [ProtoBuf.ProtoMember(7)]
            public double p_d { set; get; }

            [ProtoBuf.ProtoMember(8)]
            public bool p_bl { set; get; }

            [ProtoBuf.ProtoMember(9)]
            public DateTime p_dt { set; get; }

            [ProtoBuf.ProtoMember(10)]
            public decimal p_m { set; get; }

            [ProtoBuf.ProtoMember(11)]
            public byte p_b1 { set; get; }

            [ProtoBuf.ProtoMember(12)]
            public sbyte p_b2 { set; get; }

            [ProtoBuf.ProtoMember(13)]
            public char p_c { set; get; }

            [ProtoBuf.ProtoMember(14)]
            public short p_s1 { set; get; }

            [ProtoBuf.ProtoMember(15)]
            public ushort p_s2 { set; get; }

            [ProtoBuf.ProtoMember(16)]
            public TimeSpan p_ts { set; get; }

            [ProtoBuf.ProtoMember(17)]
            public Guid p_id { set; get; }

            [ProtoBuf.ProtoMember(18)]
            public Uri p_uri { set; get; }

            [ProtoBuf.ProtoMember(19)]
            public byte[] p_ba { set; get; }

            [ProtoBuf.ProtoMember(20)]
            public Type p_t { set; get; }

            [ProtoBuf.ProtoMember(21)]
            public string[] p_sa { set; get; }

            [ProtoBuf.ProtoMember(22)]
            public int[] p_ia { set; get; }

            [ProtoBuf.ProtoMember(23)]
            public EnumOne p_e1 { set; get; }

            [ProtoBuf.ProtoMember(24)]
            public EnumTwo p_e2 { set; get; }

            [ProtoBuf.ProtoMember(25)]
            public List<ClassFive> p_list { set; get; }

            // fields

            [ProtoBuf.ProtoMember(26)]
            public int f_i1 = 0;

            [ProtoBuf.ProtoMember(27)]
            public uint f_i2 = 0;

            [ProtoBuf.ProtoMember(28)]
            public long f_l1 = 0L;

            [ProtoBuf.ProtoMember(29)]
            public ulong f_l2 = 0UL;

            [ProtoBuf.ProtoMember(30)]
            public string f_s = string.Empty;

            [ProtoBuf.ProtoMember(31)]
            public float f_f = 0f;

            [ProtoBuf.ProtoMember(32)]
            public double f_d = 0d;

            [ProtoBuf.ProtoMember(33)]
            public bool f_bl = false;

            [ProtoBuf.ProtoMember(34)]
            public DateTime f_dt = DateTime.MinValue;

            [ProtoBuf.ProtoMember(35)]
            public decimal f_m = 0m;

            [ProtoBuf.ProtoMember(36)]
            public byte f_b1 = 0;

            [ProtoBuf.ProtoMember(37)]
            public sbyte f_b2 = 0;

            [ProtoBuf.ProtoMember(38)]
            public char f_c = (char)0;

            [ProtoBuf.ProtoMember(39)]
            public short f_s1 = 0;

            [ProtoBuf.ProtoMember(40)]
            public ushort f_s2 = 0;

            [ProtoBuf.ProtoMember(41)]
            public TimeSpan f_ts = TimeSpan.Zero;

            [ProtoBuf.ProtoMember(42)]
            public Guid f_id = Guid.Empty;

            [ProtoBuf.ProtoMember(43)]
            public Uri f_uri = null;

            [ProtoBuf.ProtoMember(44)]
            public byte[] f_ba = null;

            [ProtoBuf.ProtoMember(45)]
            public Type f_t = null;

            [ProtoBuf.ProtoMember(46)]
            public string[] f_sa = null;

            [ProtoBuf.ProtoMember(47)]
            public int[] f_ia = null;

            [ProtoBuf.ProtoMember(48)]
            public EnumOne f_e1 = 0;

            [ProtoBuf.ProtoMember(49)]
            public EnumTwo f_e2 = 0;

            [ProtoBuf.ProtoMember(50)]
            public List<ClassFive> f_list = null;
        }

        [ProtoBuf.ProtoContract]
        public class ClassSix : ClassOne
        {

        }

        [ProtoBuf.ProtoContract]
        public class ClassTwo
        {
        }

        [ProtoBuf.ProtoContract]
        public interface IClass
        {
            [ProtoBuf.ProtoMember(1)]
            string ss
            {
                set;
                get;
            }
            [ProtoBuf.ProtoMember(2)]
            ClassOne one
            {
                set;
                get;
            }
        }

        [ProtoBuf.ProtoContract]
        public class ClassThree : IClass
        {
            [ProtoBuf.ProtoMember(1)]
            public string ss { set; get; }

            [ProtoBuf.ProtoMember(2)]
            public ClassOne one { set; get; }

            [ProtoBuf.ProtoMember(3)]
            public ClassSix two { set; get; }
        }

        [ProtoBuf.ProtoContract]
        public class ClassFour
        {
            [ProtoBuf.ProtoMember(1)]
            public string ss { set; get; }

            [ProtoBuf.ProtoMember(2)]
            public ClassOne one { set; get; }
        }

        [ProtoBuf.ProtoContract]
        public class ClassFive
        {
            [ProtoBuf.ProtoMember(1)]
            public int i { set; get; }

            [ProtoBuf.ProtoMember(2)]
            public string s { set; get; }
        }
        private static ClassThree Create()
        {
            var classOne = new ClassSix()
            {
                // properties
                p_i1 = -123,
                p_i2 = 456,
                p_l1 = -456,
                p_l2 = 123,
                p_s = "str",
                p_f = 12.34f,
                p_d = 56.78d,
                p_bl = true,
                p_dt = DateTime.Now.AddMonths(-1),
                p_m = 90.12m,
                p_b1 = 12,
                p_b2 = -34,
                p_c = 'c',
                p_s1 = -21,
                p_s2 = 43,
                p_ts = new TimeSpan(12, 34, 56),
                p_id = Guid.NewGuid(),
                p_uri = new Uri("http://www.google.com"),
                p_ba = new[] { (byte)1, (byte)3, (byte)2 },
                p_t = typeof(ClassTwo),
                p_sa = new[] { "aaa", "bbb", "ccc" },
                p_ia = new[] { 7, 4, 9 },
                p_e1 = EnumOne.Three,
                p_e2 = EnumTwo.One | EnumTwo.Two,
                p_list = new List<ClassFive>(new[]
                {
                    new ClassFive()
                    {
                        i = 1,
                        s = "1"
                    },
                    new ClassFive()
                    {
                        i = 2,
                        s = "2"
                    }
                }),
                // fields
                f_i1 = -123,
                f_i2 = 456,
                f_l1 = -456,
                f_l2 = 123,
                f_s = "str",
                f_f = 12.34f,
                f_d = 56.78d,
                f_bl = true,
                f_dt = DateTime.Now.AddMonths(-1),
                f_m = 90.12m,
                f_b1 = 12,
                f_b2 = -34,
                f_c = 'c',
                f_s1 = -21,
                f_s2 = 43,
                f_ts = new TimeSpan(12, 34, 56),
                f_id = Guid.NewGuid(),
                f_uri = new Uri("http://www.google.com"),
                f_ba = new[] { (byte)1, (byte)3, (byte)2 },
                f_t = typeof(ClassTwo),
                f_sa = new[] { "aaa", "bbb", "ccc" },
                f_ia = new[] { 7, 4, 9 },
                f_e1 = EnumOne.Three,
                f_e2 = EnumTwo.One | EnumTwo.Two,
                f_list = new List<ClassFive>(new[]
                {
                    new ClassFive()
                    {
                        i = 1,
                        s = "1"
                    },
                    new ClassFive()
                    {
                        i = 2,
                        s = "2"
                    }
                })
            };
            var classThree = new ClassThree()
            {
                ss = "333",
                one = classOne,
                two = classOne
            };
            return classThree;
        }
    }
}
