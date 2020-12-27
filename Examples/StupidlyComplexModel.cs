// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using System;
using System.Diagnostics;
using System.Linq;
namespace Examples
{
#if DEBUG
    [Ignore("Too slow"), TestFixture]
#endif
    public class StupidlyComplexModel
    {
        [Test]
        public void TimeStupidlyComplexModel()
        {
            TimeModel<StupidlyComplexModel>(5, Test);
        }
        [Test]
        public void TimeSimpleModel()
        {
            TimeModel<SimpleModel>(100);
        }

        [ProtoBuf.ProtoContract]
        public class SimpleModel
        {
            [ProtoBuf.ProtoMember(1)] public int A {get;set;}
            [ProtoBuf.ProtoMember(2)] public float B {get;set;}
            [ProtoBuf.ProtoMember(3)] public decimal C {get;set;}
            [ProtoBuf.ProtoMember(4)] public bool D {get;set;}
            [ProtoBuf.ProtoMember(5)] public byte E {get;set;}
            [ProtoBuf.ProtoMember(6)] public long F {get;set;}
            [ProtoBuf.ProtoMember(7)] public short G {get;set;}
            [ProtoBuf.ProtoMember(8)] public double H {get;set;}
            [ProtoBuf.ProtoMember(9)] public float I {get;set;}
            [ProtoBuf.ProtoMember(10)] public uint J {get;set;}
            [ProtoBuf.ProtoMember(11)] public ulong K {get;set;}
            [ProtoBuf.ProtoMember(12)] public ushort L {get;set;}
            [ProtoBuf.ProtoMember(13)] public sbyte M {get;set;}
            [ProtoBuf.ProtoMember(14)] public DateTime N {get;set;}
            [ProtoBuf.ProtoMember(15)] public string O {get;set;}
            [ProtoBuf.ProtoMember(16)] public Type P {get;set;}
            [ProtoBuf.ProtoMember(17)] public byte[] Q {get;set;}
            [ProtoBuf.ProtoMember(18)] public SimpleModel R {get;set;}
            [ProtoBuf.ProtoMember(19)] public TimeSpan S {get;set;}
            [ProtoBuf.ProtoMember(20)] public int T {get;set;}
        }

        private static void TimeModel<T>(int count, Action<TypeModel, string> test = null)
        {
            var model = RuntimeTypeModel.Create();
            model.AutoCompile = false;
            model.Add(typeof(T), true);
            if (test != null) test(model, "Time");
            model.Compile(); // do discovery etc
            int typeCount = model.Types.Length;

            var watch = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                model.Compile();
            }
            watch.Stop();
            Console.WriteLine(string.Format("{0}: {1}ms/Compile, {2} types, {3}ms total, {4} iteratons",
                typeof(T).Name, watch.ElapsedMilliseconds / count, typeCount, watch.ElapsedMilliseconds, count));
            
        }


        [Test]
        public void TestStupidlyComplexModel()
        {
            var model = RuntimeTypeModel.Create();
            model.AutoCompile = false;
            model.Add(typeof(Outer), true);

            Test(model, "Runtime");
            model.CompileInPlace();
            Test(model, "CompileInPlace");
            Test(model.Compile(), "Compile");

            model.Compile("TestStupidlyComplexModel", "TestStupidlyComplexModel.dll");

            // wtf is [HRESULT 0x8007000E] - Недостаточно памяти для завершения операции (not enough memory for operation)?
            // but looks like everything works well
            // won't fix it for now
#if DEBUG_COMPILE || DEBUG_COMPILE_2
            PEVerify.AssertValid("TestStupidlyComplexModel.dll");
#endif
        }

        private void Test(TypeModel model, string test)
        {
            var orig = new Outer {
                Value500 = new Inner500 { Value = 123 },
                Value501 = new Inner501 { Value = 456 }
            };
            var clone = (Outer)model.DeepClone(orig);
            Assert.AreNotSame(orig, clone, test);
            
            var props = typeof(Outer).GetProperties();
            foreach (var prop in props)
            {
                switch(prop.Name)
                {
                    case "Value500":
                    case "Value501":
                        Assert.IsNotNull(prop.GetValue(orig), test + ":orig:" + prop.Name);
                        Assert.IsNotNull(prop.GetValue(clone), test + ":clone:" + prop.Name);
                        break;
                    default:
                        Assert.IsNull(prop.GetValue(orig), test + ":orig:" + prop.Name);
                        Assert.IsNull(prop.GetValue(clone), test + ":clone:" + prop.Name);
                    break;
                }
            }

            Assert.AreEqual(123, orig.Value500.Value, test + ":orig:Value500.Value");
            Assert.AreEqual(123, clone.Value500.Value, test + ":clone:Value500.Value");
            Assert.AreEqual(456, orig.Value501.Value, test + ":orig:Value501.Value");
            Assert.AreEqual(456, clone.Value501.Value, test + ":clone:Value501.Value");
          
            var clone500 = (Inner500)model.DeepClone(orig.Value500);
            var clone501 = (Inner501)model.DeepClone(orig.Value501);

            Assert.AreEqual(123, clone500.Value, test + ":clone500.Value");
            Assert.AreEqual(456, clone501.Value, test + ":clone501.Value");
          
        }
        [ProtoBuf.ProtoContract]
        public class Outer
        {
            [ProtoBuf.ProtoMember(1)]
            public Inner1 Value1 { get; set; }
            [ProtoBuf.ProtoMember(2)]
            public Inner2 Value2 { get; set; }
            [ProtoBuf.ProtoMember(3)]
            public Inner3 Value3 { get; set; }
            [ProtoBuf.ProtoMember(4)]
            public Inner4 Value4 { get; set; }
            [ProtoBuf.ProtoMember(5)]
            public Inner5 Value5 { get; set; }
            [ProtoBuf.ProtoMember(6)]
            public Inner6 Value6 { get; set; }
            [ProtoBuf.ProtoMember(7)]
            public Inner7 Value7 { get; set; }
            [ProtoBuf.ProtoMember(8)]
            public Inner8 Value8 { get; set; }
            [ProtoBuf.ProtoMember(9)]
            public Inner9 Value9 { get; set; }
            [ProtoBuf.ProtoMember(10)]
            public Inner10 Value10 { get; set; }
            [ProtoBuf.ProtoMember(11)]
            public Inner11 Value11 { get; set; }
            [ProtoBuf.ProtoMember(12)]
            public Inner12 Value12 { get; set; }
            [ProtoBuf.ProtoMember(13)]
            public Inner13 Value13 { get; set; }
            [ProtoBuf.ProtoMember(14)]
            public Inner14 Value14 { get; set; }
            [ProtoBuf.ProtoMember(15)]
            public Inner15 Value15 { get; set; }
            [ProtoBuf.ProtoMember(16)]
            public Inner16 Value16 { get; set; }
            [ProtoBuf.ProtoMember(17)]
            public Inner17 Value17 { get; set; }
            [ProtoBuf.ProtoMember(18)]
            public Inner18 Value18 { get; set; }
            [ProtoBuf.ProtoMember(19)]
            public Inner19 Value19 { get; set; }
            [ProtoBuf.ProtoMember(20)]
            public Inner20 Value20 { get; set; }
            [ProtoBuf.ProtoMember(21)]
            public Inner21 Value21 { get; set; }
            [ProtoBuf.ProtoMember(22)]
            public Inner22 Value22 { get; set; }
            [ProtoBuf.ProtoMember(23)]
            public Inner23 Value23 { get; set; }
            [ProtoBuf.ProtoMember(24)]
            public Inner24 Value24 { get; set; }
            [ProtoBuf.ProtoMember(25)]
            public Inner25 Value25 { get; set; }
            [ProtoBuf.ProtoMember(26)]
            public Inner26 Value26 { get; set; }
            [ProtoBuf.ProtoMember(27)]
            public Inner27 Value27 { get; set; }
            [ProtoBuf.ProtoMember(28)]
            public Inner28 Value28 { get; set; }
            [ProtoBuf.ProtoMember(29)]
            public Inner29 Value29 { get; set; }
            [ProtoBuf.ProtoMember(30)]
            public Inner30 Value30 { get; set; }
            [ProtoBuf.ProtoMember(31)]
            public Inner31 Value31 { get; set; }
            [ProtoBuf.ProtoMember(32)]
            public Inner32 Value32 { get; set; }
            [ProtoBuf.ProtoMember(33)]
            public Inner33 Value33 { get; set; }
            [ProtoBuf.ProtoMember(34)]
            public Inner34 Value34 { get; set; }
            [ProtoBuf.ProtoMember(35)]
            public Inner35 Value35 { get; set; }
            [ProtoBuf.ProtoMember(36)]
            public Inner36 Value36 { get; set; }
            [ProtoBuf.ProtoMember(37)]
            public Inner37 Value37 { get; set; }
            [ProtoBuf.ProtoMember(38)]
            public Inner38 Value38 { get; set; }
            [ProtoBuf.ProtoMember(39)]
            public Inner39 Value39 { get; set; }
            [ProtoBuf.ProtoMember(40)]
            public Inner40 Value40 { get; set; }
            [ProtoBuf.ProtoMember(41)]
            public Inner41 Value41 { get; set; }
            [ProtoBuf.ProtoMember(42)]
            public Inner42 Value42 { get; set; }
            [ProtoBuf.ProtoMember(43)]
            public Inner43 Value43 { get; set; }
            [ProtoBuf.ProtoMember(44)]
            public Inner44 Value44 { get; set; }
            [ProtoBuf.ProtoMember(45)]
            public Inner45 Value45 { get; set; }
            [ProtoBuf.ProtoMember(46)]
            public Inner46 Value46 { get; set; }
            [ProtoBuf.ProtoMember(47)]
            public Inner47 Value47 { get; set; }
            [ProtoBuf.ProtoMember(48)]
            public Inner48 Value48 { get; set; }
            [ProtoBuf.ProtoMember(49)]
            public Inner49 Value49 { get; set; }
            [ProtoBuf.ProtoMember(50)]
            public Inner50 Value50 { get; set; }
            [ProtoBuf.ProtoMember(51)]
            public Inner51 Value51 { get; set; }
            [ProtoBuf.ProtoMember(52)]
            public Inner52 Value52 { get; set; }
            [ProtoBuf.ProtoMember(53)]
            public Inner53 Value53 { get; set; }
            [ProtoBuf.ProtoMember(54)]
            public Inner54 Value54 { get; set; }
            [ProtoBuf.ProtoMember(55)]
            public Inner55 Value55 { get; set; }
            [ProtoBuf.ProtoMember(56)]
            public Inner56 Value56 { get; set; }
            [ProtoBuf.ProtoMember(57)]
            public Inner57 Value57 { get; set; }
            [ProtoBuf.ProtoMember(58)]
            public Inner58 Value58 { get; set; }
            [ProtoBuf.ProtoMember(59)]
            public Inner59 Value59 { get; set; }
            [ProtoBuf.ProtoMember(60)]
            public Inner60 Value60 { get; set; }
            [ProtoBuf.ProtoMember(61)]
            public Inner61 Value61 { get; set; }
            [ProtoBuf.ProtoMember(62)]
            public Inner62 Value62 { get; set; }
            [ProtoBuf.ProtoMember(63)]
            public Inner63 Value63 { get; set; }
            [ProtoBuf.ProtoMember(64)]
            public Inner64 Value64 { get; set; }
            [ProtoBuf.ProtoMember(65)]
            public Inner65 Value65 { get; set; }
            [ProtoBuf.ProtoMember(66)]
            public Inner66 Value66 { get; set; }
            [ProtoBuf.ProtoMember(67)]
            public Inner67 Value67 { get; set; }
            [ProtoBuf.ProtoMember(68)]
            public Inner68 Value68 { get; set; }
            [ProtoBuf.ProtoMember(69)]
            public Inner69 Value69 { get; set; }
            [ProtoBuf.ProtoMember(70)]
            public Inner70 Value70 { get; set; }
            [ProtoBuf.ProtoMember(71)]
            public Inner71 Value71 { get; set; }
            [ProtoBuf.ProtoMember(72)]
            public Inner72 Value72 { get; set; }
            [ProtoBuf.ProtoMember(73)]
            public Inner73 Value73 { get; set; }
            [ProtoBuf.ProtoMember(74)]
            public Inner74 Value74 { get; set; }
            [ProtoBuf.ProtoMember(75)]
            public Inner75 Value75 { get; set; }
            [ProtoBuf.ProtoMember(76)]
            public Inner76 Value76 { get; set; }
            [ProtoBuf.ProtoMember(77)]
            public Inner77 Value77 { get; set; }
            [ProtoBuf.ProtoMember(78)]
            public Inner78 Value78 { get; set; }
            [ProtoBuf.ProtoMember(79)]
            public Inner79 Value79 { get; set; }
            [ProtoBuf.ProtoMember(80)]
            public Inner80 Value80 { get; set; }
            [ProtoBuf.ProtoMember(81)]
            public Inner81 Value81 { get; set; }
            [ProtoBuf.ProtoMember(82)]
            public Inner82 Value82 { get; set; }
            [ProtoBuf.ProtoMember(83)]
            public Inner83 Value83 { get; set; }
            [ProtoBuf.ProtoMember(84)]
            public Inner84 Value84 { get; set; }
            [ProtoBuf.ProtoMember(85)]
            public Inner85 Value85 { get; set; }
            [ProtoBuf.ProtoMember(86)]
            public Inner86 Value86 { get; set; }
            [ProtoBuf.ProtoMember(87)]
            public Inner87 Value87 { get; set; }
            [ProtoBuf.ProtoMember(88)]
            public Inner88 Value88 { get; set; }
            [ProtoBuf.ProtoMember(89)]
            public Inner89 Value89 { get; set; }
            [ProtoBuf.ProtoMember(90)]
            public Inner90 Value90 { get; set; }
            [ProtoBuf.ProtoMember(91)]
            public Inner91 Value91 { get; set; }
            [ProtoBuf.ProtoMember(92)]
            public Inner92 Value92 { get; set; }
            [ProtoBuf.ProtoMember(93)]
            public Inner93 Value93 { get; set; }
            [ProtoBuf.ProtoMember(94)]
            public Inner94 Value94 { get; set; }
            [ProtoBuf.ProtoMember(95)]
            public Inner95 Value95 { get; set; }
            [ProtoBuf.ProtoMember(96)]
            public Inner96 Value96 { get; set; }
            [ProtoBuf.ProtoMember(97)]
            public Inner97 Value97 { get; set; }
            [ProtoBuf.ProtoMember(98)]
            public Inner98 Value98 { get; set; }
            [ProtoBuf.ProtoMember(99)]
            public Inner99 Value99 { get; set; }
            [ProtoBuf.ProtoMember(100)]
            public Inner100 Value100 { get; set; }
            [ProtoBuf.ProtoMember(101)]
            public Inner101 Value101 { get; set; }
            [ProtoBuf.ProtoMember(102)]
            public Inner102 Value102 { get; set; }
            [ProtoBuf.ProtoMember(103)]
            public Inner103 Value103 { get; set; }
            [ProtoBuf.ProtoMember(104)]
            public Inner104 Value104 { get; set; }
            [ProtoBuf.ProtoMember(105)]
            public Inner105 Value105 { get; set; }
            [ProtoBuf.ProtoMember(106)]
            public Inner106 Value106 { get; set; }
            [ProtoBuf.ProtoMember(107)]
            public Inner107 Value107 { get; set; }
            [ProtoBuf.ProtoMember(108)]
            public Inner108 Value108 { get; set; }
            [ProtoBuf.ProtoMember(109)]
            public Inner109 Value109 { get; set; }
            [ProtoBuf.ProtoMember(110)]
            public Inner110 Value110 { get; set; }
            [ProtoBuf.ProtoMember(111)]
            public Inner111 Value111 { get; set; }
            [ProtoBuf.ProtoMember(112)]
            public Inner112 Value112 { get; set; }
            [ProtoBuf.ProtoMember(113)]
            public Inner113 Value113 { get; set; }
            [ProtoBuf.ProtoMember(114)]
            public Inner114 Value114 { get; set; }
            [ProtoBuf.ProtoMember(115)]
            public Inner115 Value115 { get; set; }
            [ProtoBuf.ProtoMember(116)]
            public Inner116 Value116 { get; set; }
            [ProtoBuf.ProtoMember(117)]
            public Inner117 Value117 { get; set; }
            [ProtoBuf.ProtoMember(118)]
            public Inner118 Value118 { get; set; }
            [ProtoBuf.ProtoMember(119)]
            public Inner119 Value119 { get; set; }
            [ProtoBuf.ProtoMember(120)]
            public Inner120 Value120 { get; set; }
            [ProtoBuf.ProtoMember(121)]
            public Inner121 Value121 { get; set; }
            [ProtoBuf.ProtoMember(122)]
            public Inner122 Value122 { get; set; }
            [ProtoBuf.ProtoMember(123)]
            public Inner123 Value123 { get; set; }
            [ProtoBuf.ProtoMember(124)]
            public Inner124 Value124 { get; set; }
            [ProtoBuf.ProtoMember(125)]
            public Inner125 Value125 { get; set; }
            [ProtoBuf.ProtoMember(126)]
            public Inner126 Value126 { get; set; }
            [ProtoBuf.ProtoMember(127)]
            public Inner127 Value127 { get; set; }
            [ProtoBuf.ProtoMember(128)]
            public Inner128 Value128 { get; set; }
            [ProtoBuf.ProtoMember(129)]
            public Inner129 Value129 { get; set; }
            [ProtoBuf.ProtoMember(130)]
            public Inner130 Value130 { get; set; }
            [ProtoBuf.ProtoMember(131)]
            public Inner131 Value131 { get; set; }
            [ProtoBuf.ProtoMember(132)]
            public Inner132 Value132 { get; set; }
            [ProtoBuf.ProtoMember(133)]
            public Inner133 Value133 { get; set; }
            [ProtoBuf.ProtoMember(134)]
            public Inner134 Value134 { get; set; }
            [ProtoBuf.ProtoMember(135)]
            public Inner135 Value135 { get; set; }
            [ProtoBuf.ProtoMember(136)]
            public Inner136 Value136 { get; set; }
            [ProtoBuf.ProtoMember(137)]
            public Inner137 Value137 { get; set; }
            [ProtoBuf.ProtoMember(138)]
            public Inner138 Value138 { get; set; }
            [ProtoBuf.ProtoMember(139)]
            public Inner139 Value139 { get; set; }
            [ProtoBuf.ProtoMember(140)]
            public Inner140 Value140 { get; set; }
            [ProtoBuf.ProtoMember(141)]
            public Inner141 Value141 { get; set; }
            [ProtoBuf.ProtoMember(142)]
            public Inner142 Value142 { get; set; }
            [ProtoBuf.ProtoMember(143)]
            public Inner143 Value143 { get; set; }
            [ProtoBuf.ProtoMember(144)]
            public Inner144 Value144 { get; set; }
            [ProtoBuf.ProtoMember(145)]
            public Inner145 Value145 { get; set; }
            [ProtoBuf.ProtoMember(146)]
            public Inner146 Value146 { get; set; }
            [ProtoBuf.ProtoMember(147)]
            public Inner147 Value147 { get; set; }
            [ProtoBuf.ProtoMember(148)]
            public Inner148 Value148 { get; set; }
            [ProtoBuf.ProtoMember(149)]
            public Inner149 Value149 { get; set; }
            [ProtoBuf.ProtoMember(150)]
            public Inner150 Value150 { get; set; }
            [ProtoBuf.ProtoMember(151)]
            public Inner151 Value151 { get; set; }
            [ProtoBuf.ProtoMember(152)]
            public Inner152 Value152 { get; set; }
            [ProtoBuf.ProtoMember(153)]
            public Inner153 Value153 { get; set; }
            [ProtoBuf.ProtoMember(154)]
            public Inner154 Value154 { get; set; }
            [ProtoBuf.ProtoMember(155)]
            public Inner155 Value155 { get; set; }
            [ProtoBuf.ProtoMember(156)]
            public Inner156 Value156 { get; set; }
            [ProtoBuf.ProtoMember(157)]
            public Inner157 Value157 { get; set; }
            [ProtoBuf.ProtoMember(158)]
            public Inner158 Value158 { get; set; }
            [ProtoBuf.ProtoMember(159)]
            public Inner159 Value159 { get; set; }
            [ProtoBuf.ProtoMember(160)]
            public Inner160 Value160 { get; set; }
            [ProtoBuf.ProtoMember(161)]
            public Inner161 Value161 { get; set; }
            [ProtoBuf.ProtoMember(162)]
            public Inner162 Value162 { get; set; }
            [ProtoBuf.ProtoMember(163)]
            public Inner163 Value163 { get; set; }
            [ProtoBuf.ProtoMember(164)]
            public Inner164 Value164 { get; set; }
            [ProtoBuf.ProtoMember(165)]
            public Inner165 Value165 { get; set; }
            [ProtoBuf.ProtoMember(166)]
            public Inner166 Value166 { get; set; }
            [ProtoBuf.ProtoMember(167)]
            public Inner167 Value167 { get; set; }
            [ProtoBuf.ProtoMember(168)]
            public Inner168 Value168 { get; set; }
            [ProtoBuf.ProtoMember(169)]
            public Inner169 Value169 { get; set; }
            [ProtoBuf.ProtoMember(170)]
            public Inner170 Value170 { get; set; }
            [ProtoBuf.ProtoMember(171)]
            public Inner171 Value171 { get; set; }
            [ProtoBuf.ProtoMember(172)]
            public Inner172 Value172 { get; set; }
            [ProtoBuf.ProtoMember(173)]
            public Inner173 Value173 { get; set; }
            [ProtoBuf.ProtoMember(174)]
            public Inner174 Value174 { get; set; }
            [ProtoBuf.ProtoMember(175)]
            public Inner175 Value175 { get; set; }
            [ProtoBuf.ProtoMember(176)]
            public Inner176 Value176 { get; set; }
            [ProtoBuf.ProtoMember(177)]
            public Inner177 Value177 { get; set; }
            [ProtoBuf.ProtoMember(178)]
            public Inner178 Value178 { get; set; }
            [ProtoBuf.ProtoMember(179)]
            public Inner179 Value179 { get; set; }
            [ProtoBuf.ProtoMember(180)]
            public Inner180 Value180 { get; set; }
            [ProtoBuf.ProtoMember(181)]
            public Inner181 Value181 { get; set; }
            [ProtoBuf.ProtoMember(182)]
            public Inner182 Value182 { get; set; }
            [ProtoBuf.ProtoMember(183)]
            public Inner183 Value183 { get; set; }
            [ProtoBuf.ProtoMember(184)]
            public Inner184 Value184 { get; set; }
            [ProtoBuf.ProtoMember(185)]
            public Inner185 Value185 { get; set; }
            [ProtoBuf.ProtoMember(186)]
            public Inner186 Value186 { get; set; }
            [ProtoBuf.ProtoMember(187)]
            public Inner187 Value187 { get; set; }
            [ProtoBuf.ProtoMember(188)]
            public Inner188 Value188 { get; set; }
            [ProtoBuf.ProtoMember(189)]
            public Inner189 Value189 { get; set; }
            [ProtoBuf.ProtoMember(190)]
            public Inner190 Value190 { get; set; }
            [ProtoBuf.ProtoMember(191)]
            public Inner191 Value191 { get; set; }
            [ProtoBuf.ProtoMember(192)]
            public Inner192 Value192 { get; set; }
            [ProtoBuf.ProtoMember(193)]
            public Inner193 Value193 { get; set; }
            [ProtoBuf.ProtoMember(194)]
            public Inner194 Value194 { get; set; }
            [ProtoBuf.ProtoMember(195)]
            public Inner195 Value195 { get; set; }
            [ProtoBuf.ProtoMember(196)]
            public Inner196 Value196 { get; set; }
            [ProtoBuf.ProtoMember(197)]
            public Inner197 Value197 { get; set; }
            [ProtoBuf.ProtoMember(198)]
            public Inner198 Value198 { get; set; }
            [ProtoBuf.ProtoMember(199)]
            public Inner199 Value199 { get; set; }
            [ProtoBuf.ProtoMember(200)]
            public Inner200 Value200 { get; set; }
            [ProtoBuf.ProtoMember(201)]
            public Inner201 Value201 { get; set; }
            [ProtoBuf.ProtoMember(202)]
            public Inner202 Value202 { get; set; }
            [ProtoBuf.ProtoMember(203)]
            public Inner203 Value203 { get; set; }
            [ProtoBuf.ProtoMember(204)]
            public Inner204 Value204 { get; set; }
            [ProtoBuf.ProtoMember(205)]
            public Inner205 Value205 { get; set; }
            [ProtoBuf.ProtoMember(206)]
            public Inner206 Value206 { get; set; }
            [ProtoBuf.ProtoMember(207)]
            public Inner207 Value207 { get; set; }
            [ProtoBuf.ProtoMember(208)]
            public Inner208 Value208 { get; set; }
            [ProtoBuf.ProtoMember(209)]
            public Inner209 Value209 { get; set; }
            [ProtoBuf.ProtoMember(210)]
            public Inner210 Value210 { get; set; }
            [ProtoBuf.ProtoMember(211)]
            public Inner211 Value211 { get; set; }
            [ProtoBuf.ProtoMember(212)]
            public Inner212 Value212 { get; set; }
            [ProtoBuf.ProtoMember(213)]
            public Inner213 Value213 { get; set; }
            [ProtoBuf.ProtoMember(214)]
            public Inner214 Value214 { get; set; }
            [ProtoBuf.ProtoMember(215)]
            public Inner215 Value215 { get; set; }
            [ProtoBuf.ProtoMember(216)]
            public Inner216 Value216 { get; set; }
            [ProtoBuf.ProtoMember(217)]
            public Inner217 Value217 { get; set; }
            [ProtoBuf.ProtoMember(218)]
            public Inner218 Value218 { get; set; }
            [ProtoBuf.ProtoMember(219)]
            public Inner219 Value219 { get; set; }
            [ProtoBuf.ProtoMember(220)]
            public Inner220 Value220 { get; set; }
            [ProtoBuf.ProtoMember(221)]
            public Inner221 Value221 { get; set; }
            [ProtoBuf.ProtoMember(222)]
            public Inner222 Value222 { get; set; }
            [ProtoBuf.ProtoMember(223)]
            public Inner223 Value223 { get; set; }
            [ProtoBuf.ProtoMember(224)]
            public Inner224 Value224 { get; set; }
            [ProtoBuf.ProtoMember(225)]
            public Inner225 Value225 { get; set; }
            [ProtoBuf.ProtoMember(226)]
            public Inner226 Value226 { get; set; }
            [ProtoBuf.ProtoMember(227)]
            public Inner227 Value227 { get; set; }
            [ProtoBuf.ProtoMember(228)]
            public Inner228 Value228 { get; set; }
            [ProtoBuf.ProtoMember(229)]
            public Inner229 Value229 { get; set; }
            [ProtoBuf.ProtoMember(230)]
            public Inner230 Value230 { get; set; }
            [ProtoBuf.ProtoMember(231)]
            public Inner231 Value231 { get; set; }
            [ProtoBuf.ProtoMember(232)]
            public Inner232 Value232 { get; set; }
            [ProtoBuf.ProtoMember(233)]
            public Inner233 Value233 { get; set; }
            [ProtoBuf.ProtoMember(234)]
            public Inner234 Value234 { get; set; }
            [ProtoBuf.ProtoMember(235)]
            public Inner235 Value235 { get; set; }
            [ProtoBuf.ProtoMember(236)]
            public Inner236 Value236 { get; set; }
            [ProtoBuf.ProtoMember(237)]
            public Inner237 Value237 { get; set; }
            [ProtoBuf.ProtoMember(238)]
            public Inner238 Value238 { get; set; }
            [ProtoBuf.ProtoMember(239)]
            public Inner239 Value239 { get; set; }
            [ProtoBuf.ProtoMember(240)]
            public Inner240 Value240 { get; set; }
            [ProtoBuf.ProtoMember(241)]
            public Inner241 Value241 { get; set; }
            [ProtoBuf.ProtoMember(242)]
            public Inner242 Value242 { get; set; }
            [ProtoBuf.ProtoMember(243)]
            public Inner243 Value243 { get; set; }
            [ProtoBuf.ProtoMember(244)]
            public Inner244 Value244 { get; set; }
            [ProtoBuf.ProtoMember(245)]
            public Inner245 Value245 { get; set; }
            [ProtoBuf.ProtoMember(246)]
            public Inner246 Value246 { get; set; }
            [ProtoBuf.ProtoMember(247)]
            public Inner247 Value247 { get; set; }
            [ProtoBuf.ProtoMember(248)]
            public Inner248 Value248 { get; set; }
            [ProtoBuf.ProtoMember(249)]
            public Inner249 Value249 { get; set; }
            [ProtoBuf.ProtoMember(250)]
            public Inner250 Value250 { get; set; }
            [ProtoBuf.ProtoMember(251)]
            public Inner251 Value251 { get; set; }
            [ProtoBuf.ProtoMember(252)]
            public Inner252 Value252 { get; set; }
            [ProtoBuf.ProtoMember(253)]
            public Inner253 Value253 { get; set; }
            [ProtoBuf.ProtoMember(254)]
            public Inner254 Value254 { get; set; }
            [ProtoBuf.ProtoMember(255)]
            public Inner255 Value255 { get; set; }
            [ProtoBuf.ProtoMember(256)]
            public Inner256 Value256 { get; set; }
            [ProtoBuf.ProtoMember(257)]
            public Inner257 Value257 { get; set; }
            [ProtoBuf.ProtoMember(258)]
            public Inner258 Value258 { get; set; }
            [ProtoBuf.ProtoMember(259)]
            public Inner259 Value259 { get; set; }
            [ProtoBuf.ProtoMember(260)]
            public Inner260 Value260 { get; set; }
            [ProtoBuf.ProtoMember(261)]
            public Inner261 Value261 { get; set; }
            [ProtoBuf.ProtoMember(262)]
            public Inner262 Value262 { get; set; }
            [ProtoBuf.ProtoMember(263)]
            public Inner263 Value263 { get; set; }
            [ProtoBuf.ProtoMember(264)]
            public Inner264 Value264 { get; set; }
            [ProtoBuf.ProtoMember(265)]
            public Inner265 Value265 { get; set; }
            [ProtoBuf.ProtoMember(266)]
            public Inner266 Value266 { get; set; }
            [ProtoBuf.ProtoMember(267)]
            public Inner267 Value267 { get; set; }
            [ProtoBuf.ProtoMember(268)]
            public Inner268 Value268 { get; set; }
            [ProtoBuf.ProtoMember(269)]
            public Inner269 Value269 { get; set; }
            [ProtoBuf.ProtoMember(270)]
            public Inner270 Value270 { get; set; }
            [ProtoBuf.ProtoMember(271)]
            public Inner271 Value271 { get; set; }
            [ProtoBuf.ProtoMember(272)]
            public Inner272 Value272 { get; set; }
            [ProtoBuf.ProtoMember(273)]
            public Inner273 Value273 { get; set; }
            [ProtoBuf.ProtoMember(274)]
            public Inner274 Value274 { get; set; }
            [ProtoBuf.ProtoMember(275)]
            public Inner275 Value275 { get; set; }
            [ProtoBuf.ProtoMember(276)]
            public Inner276 Value276 { get; set; }
            [ProtoBuf.ProtoMember(277)]
            public Inner277 Value277 { get; set; }
            [ProtoBuf.ProtoMember(278)]
            public Inner278 Value278 { get; set; }
            [ProtoBuf.ProtoMember(279)]
            public Inner279 Value279 { get; set; }
            [ProtoBuf.ProtoMember(280)]
            public Inner280 Value280 { get; set; }
            [ProtoBuf.ProtoMember(281)]
            public Inner281 Value281 { get; set; }
            [ProtoBuf.ProtoMember(282)]
            public Inner282 Value282 { get; set; }
            [ProtoBuf.ProtoMember(283)]
            public Inner283 Value283 { get; set; }
            [ProtoBuf.ProtoMember(284)]
            public Inner284 Value284 { get; set; }
            [ProtoBuf.ProtoMember(285)]
            public Inner285 Value285 { get; set; }
            [ProtoBuf.ProtoMember(286)]
            public Inner286 Value286 { get; set; }
            [ProtoBuf.ProtoMember(287)]
            public Inner287 Value287 { get; set; }
            [ProtoBuf.ProtoMember(288)]
            public Inner288 Value288 { get; set; }
            [ProtoBuf.ProtoMember(289)]
            public Inner289 Value289 { get; set; }
            [ProtoBuf.ProtoMember(290)]
            public Inner290 Value290 { get; set; }
            [ProtoBuf.ProtoMember(291)]
            public Inner291 Value291 { get; set; }
            [ProtoBuf.ProtoMember(292)]
            public Inner292 Value292 { get; set; }
            [ProtoBuf.ProtoMember(293)]
            public Inner293 Value293 { get; set; }
            [ProtoBuf.ProtoMember(294)]
            public Inner294 Value294 { get; set; }
            [ProtoBuf.ProtoMember(295)]
            public Inner295 Value295 { get; set; }
            [ProtoBuf.ProtoMember(296)]
            public Inner296 Value296 { get; set; }
            [ProtoBuf.ProtoMember(297)]
            public Inner297 Value297 { get; set; }
            [ProtoBuf.ProtoMember(298)]
            public Inner298 Value298 { get; set; }
            [ProtoBuf.ProtoMember(299)]
            public Inner299 Value299 { get; set; }
            [ProtoBuf.ProtoMember(300)]
            public Inner300 Value300 { get; set; }
            [ProtoBuf.ProtoMember(301)]
            public Inner301 Value301 { get; set; }
            [ProtoBuf.ProtoMember(302)]
            public Inner302 Value302 { get; set; }
            [ProtoBuf.ProtoMember(303)]
            public Inner303 Value303 { get; set; }
            [ProtoBuf.ProtoMember(304)]
            public Inner304 Value304 { get; set; }
            [ProtoBuf.ProtoMember(305)]
            public Inner305 Value305 { get; set; }
            [ProtoBuf.ProtoMember(306)]
            public Inner306 Value306 { get; set; }
            [ProtoBuf.ProtoMember(307)]
            public Inner307 Value307 { get; set; }
            [ProtoBuf.ProtoMember(308)]
            public Inner308 Value308 { get; set; }
            [ProtoBuf.ProtoMember(309)]
            public Inner309 Value309 { get; set; }
            [ProtoBuf.ProtoMember(310)]
            public Inner310 Value310 { get; set; }
            [ProtoBuf.ProtoMember(311)]
            public Inner311 Value311 { get; set; }
            [ProtoBuf.ProtoMember(312)]
            public Inner312 Value312 { get; set; }
            [ProtoBuf.ProtoMember(313)]
            public Inner313 Value313 { get; set; }
            [ProtoBuf.ProtoMember(314)]
            public Inner314 Value314 { get; set; }
            [ProtoBuf.ProtoMember(315)]
            public Inner315 Value315 { get; set; }
            [ProtoBuf.ProtoMember(316)]
            public Inner316 Value316 { get; set; }
            [ProtoBuf.ProtoMember(317)]
            public Inner317 Value317 { get; set; }
            [ProtoBuf.ProtoMember(318)]
            public Inner318 Value318 { get; set; }
            [ProtoBuf.ProtoMember(319)]
            public Inner319 Value319 { get; set; }
            [ProtoBuf.ProtoMember(320)]
            public Inner320 Value320 { get; set; }
            [ProtoBuf.ProtoMember(321)]
            public Inner321 Value321 { get; set; }
            [ProtoBuf.ProtoMember(322)]
            public Inner322 Value322 { get; set; }
            [ProtoBuf.ProtoMember(323)]
            public Inner323 Value323 { get; set; }
            [ProtoBuf.ProtoMember(324)]
            public Inner324 Value324 { get; set; }
            [ProtoBuf.ProtoMember(325)]
            public Inner325 Value325 { get; set; }
            [ProtoBuf.ProtoMember(326)]
            public Inner326 Value326 { get; set; }
            [ProtoBuf.ProtoMember(327)]
            public Inner327 Value327 { get; set; }
            [ProtoBuf.ProtoMember(328)]
            public Inner328 Value328 { get; set; }
            [ProtoBuf.ProtoMember(329)]
            public Inner329 Value329 { get; set; }
            [ProtoBuf.ProtoMember(330)]
            public Inner330 Value330 { get; set; }
            [ProtoBuf.ProtoMember(331)]
            public Inner331 Value331 { get; set; }
            [ProtoBuf.ProtoMember(332)]
            public Inner332 Value332 { get; set; }
            [ProtoBuf.ProtoMember(333)]
            public Inner333 Value333 { get; set; }
            [ProtoBuf.ProtoMember(334)]
            public Inner334 Value334 { get; set; }
            [ProtoBuf.ProtoMember(335)]
            public Inner335 Value335 { get; set; }
            [ProtoBuf.ProtoMember(336)]
            public Inner336 Value336 { get; set; }
            [ProtoBuf.ProtoMember(337)]
            public Inner337 Value337 { get; set; }
            [ProtoBuf.ProtoMember(338)]
            public Inner338 Value338 { get; set; }
            [ProtoBuf.ProtoMember(339)]
            public Inner339 Value339 { get; set; }
            [ProtoBuf.ProtoMember(340)]
            public Inner340 Value340 { get; set; }
            [ProtoBuf.ProtoMember(341)]
            public Inner341 Value341 { get; set; }
            [ProtoBuf.ProtoMember(342)]
            public Inner342 Value342 { get; set; }
            [ProtoBuf.ProtoMember(343)]
            public Inner343 Value343 { get; set; }
            [ProtoBuf.ProtoMember(344)]
            public Inner344 Value344 { get; set; }
            [ProtoBuf.ProtoMember(345)]
            public Inner345 Value345 { get; set; }
            [ProtoBuf.ProtoMember(346)]
            public Inner346 Value346 { get; set; }
            [ProtoBuf.ProtoMember(347)]
            public Inner347 Value347 { get; set; }
            [ProtoBuf.ProtoMember(348)]
            public Inner348 Value348 { get; set; }
            [ProtoBuf.ProtoMember(349)]
            public Inner349 Value349 { get; set; }
            [ProtoBuf.ProtoMember(350)]
            public Inner350 Value350 { get; set; }
            [ProtoBuf.ProtoMember(351)]
            public Inner351 Value351 { get; set; }
            [ProtoBuf.ProtoMember(352)]
            public Inner352 Value352 { get; set; }
            [ProtoBuf.ProtoMember(353)]
            public Inner353 Value353 { get; set; }
            [ProtoBuf.ProtoMember(354)]
            public Inner354 Value354 { get; set; }
            [ProtoBuf.ProtoMember(355)]
            public Inner355 Value355 { get; set; }
            [ProtoBuf.ProtoMember(356)]
            public Inner356 Value356 { get; set; }
            [ProtoBuf.ProtoMember(357)]
            public Inner357 Value357 { get; set; }
            [ProtoBuf.ProtoMember(358)]
            public Inner358 Value358 { get; set; }
            [ProtoBuf.ProtoMember(359)]
            public Inner359 Value359 { get; set; }
            [ProtoBuf.ProtoMember(360)]
            public Inner360 Value360 { get; set; }
            [ProtoBuf.ProtoMember(361)]
            public Inner361 Value361 { get; set; }
            [ProtoBuf.ProtoMember(362)]
            public Inner362 Value362 { get; set; }
            [ProtoBuf.ProtoMember(363)]
            public Inner363 Value363 { get; set; }
            [ProtoBuf.ProtoMember(364)]
            public Inner364 Value364 { get; set; }
            [ProtoBuf.ProtoMember(365)]
            public Inner365 Value365 { get; set; }
            [ProtoBuf.ProtoMember(366)]
            public Inner366 Value366 { get; set; }
            [ProtoBuf.ProtoMember(367)]
            public Inner367 Value367 { get; set; }
            [ProtoBuf.ProtoMember(368)]
            public Inner368 Value368 { get; set; }
            [ProtoBuf.ProtoMember(369)]
            public Inner369 Value369 { get; set; }
            [ProtoBuf.ProtoMember(370)]
            public Inner370 Value370 { get; set; }
            [ProtoBuf.ProtoMember(371)]
            public Inner371 Value371 { get; set; }
            [ProtoBuf.ProtoMember(372)]
            public Inner372 Value372 { get; set; }
            [ProtoBuf.ProtoMember(373)]
            public Inner373 Value373 { get; set; }
            [ProtoBuf.ProtoMember(374)]
            public Inner374 Value374 { get; set; }
            [ProtoBuf.ProtoMember(375)]
            public Inner375 Value375 { get; set; }
            [ProtoBuf.ProtoMember(376)]
            public Inner376 Value376 { get; set; }
            [ProtoBuf.ProtoMember(377)]
            public Inner377 Value377 { get; set; }
            [ProtoBuf.ProtoMember(378)]
            public Inner378 Value378 { get; set; }
            [ProtoBuf.ProtoMember(379)]
            public Inner379 Value379 { get; set; }
            [ProtoBuf.ProtoMember(380)]
            public Inner380 Value380 { get; set; }
            [ProtoBuf.ProtoMember(381)]
            public Inner381 Value381 { get; set; }
            [ProtoBuf.ProtoMember(382)]
            public Inner382 Value382 { get; set; }
            [ProtoBuf.ProtoMember(383)]
            public Inner383 Value383 { get; set; }
            [ProtoBuf.ProtoMember(384)]
            public Inner384 Value384 { get; set; }
            [ProtoBuf.ProtoMember(385)]
            public Inner385 Value385 { get; set; }
            [ProtoBuf.ProtoMember(386)]
            public Inner386 Value386 { get; set; }
            [ProtoBuf.ProtoMember(387)]
            public Inner387 Value387 { get; set; }
            [ProtoBuf.ProtoMember(388)]
            public Inner388 Value388 { get; set; }
            [ProtoBuf.ProtoMember(389)]
            public Inner389 Value389 { get; set; }
            [ProtoBuf.ProtoMember(390)]
            public Inner390 Value390 { get; set; }
            [ProtoBuf.ProtoMember(391)]
            public Inner391 Value391 { get; set; }
            [ProtoBuf.ProtoMember(392)]
            public Inner392 Value392 { get; set; }
            [ProtoBuf.ProtoMember(393)]
            public Inner393 Value393 { get; set; }
            [ProtoBuf.ProtoMember(394)]
            public Inner394 Value394 { get; set; }
            [ProtoBuf.ProtoMember(395)]
            public Inner395 Value395 { get; set; }
            [ProtoBuf.ProtoMember(396)]
            public Inner396 Value396 { get; set; }
            [ProtoBuf.ProtoMember(397)]
            public Inner397 Value397 { get; set; }
            [ProtoBuf.ProtoMember(398)]
            public Inner398 Value398 { get; set; }
            [ProtoBuf.ProtoMember(399)]
            public Inner399 Value399 { get; set; }
            [ProtoBuf.ProtoMember(400)]
            public Inner400 Value400 { get; set; }
            [ProtoBuf.ProtoMember(401)]
            public Inner401 Value401 { get; set; }
            [ProtoBuf.ProtoMember(402)]
            public Inner402 Value402 { get; set; }
            [ProtoBuf.ProtoMember(403)]
            public Inner403 Value403 { get; set; }
            [ProtoBuf.ProtoMember(404)]
            public Inner404 Value404 { get; set; }
            [ProtoBuf.ProtoMember(405)]
            public Inner405 Value405 { get; set; }
            [ProtoBuf.ProtoMember(406)]
            public Inner406 Value406 { get; set; }
            [ProtoBuf.ProtoMember(407)]
            public Inner407 Value407 { get; set; }
            [ProtoBuf.ProtoMember(408)]
            public Inner408 Value408 { get; set; }
            [ProtoBuf.ProtoMember(409)]
            public Inner409 Value409 { get; set; }
            [ProtoBuf.ProtoMember(410)]
            public Inner410 Value410 { get; set; }
            [ProtoBuf.ProtoMember(411)]
            public Inner411 Value411 { get; set; }
            [ProtoBuf.ProtoMember(412)]
            public Inner412 Value412 { get; set; }
            [ProtoBuf.ProtoMember(413)]
            public Inner413 Value413 { get; set; }
            [ProtoBuf.ProtoMember(414)]
            public Inner414 Value414 { get; set; }
            [ProtoBuf.ProtoMember(415)]
            public Inner415 Value415 { get; set; }
            [ProtoBuf.ProtoMember(416)]
            public Inner416 Value416 { get; set; }
            [ProtoBuf.ProtoMember(417)]
            public Inner417 Value417 { get; set; }
            [ProtoBuf.ProtoMember(418)]
            public Inner418 Value418 { get; set; }
            [ProtoBuf.ProtoMember(419)]
            public Inner419 Value419 { get; set; }
            [ProtoBuf.ProtoMember(420)]
            public Inner420 Value420 { get; set; }
            [ProtoBuf.ProtoMember(421)]
            public Inner421 Value421 { get; set; }
            [ProtoBuf.ProtoMember(422)]
            public Inner422 Value422 { get; set; }
            [ProtoBuf.ProtoMember(423)]
            public Inner423 Value423 { get; set; }
            [ProtoBuf.ProtoMember(424)]
            public Inner424 Value424 { get; set; }
            [ProtoBuf.ProtoMember(425)]
            public Inner425 Value425 { get; set; }
            [ProtoBuf.ProtoMember(426)]
            public Inner426 Value426 { get; set; }
            [ProtoBuf.ProtoMember(427)]
            public Inner427 Value427 { get; set; }
            [ProtoBuf.ProtoMember(428)]
            public Inner428 Value428 { get; set; }
            [ProtoBuf.ProtoMember(429)]
            public Inner429 Value429 { get; set; }
            [ProtoBuf.ProtoMember(430)]
            public Inner430 Value430 { get; set; }
            [ProtoBuf.ProtoMember(431)]
            public Inner431 Value431 { get; set; }
            [ProtoBuf.ProtoMember(432)]
            public Inner432 Value432 { get; set; }
            [ProtoBuf.ProtoMember(433)]
            public Inner433 Value433 { get; set; }
            [ProtoBuf.ProtoMember(434)]
            public Inner434 Value434 { get; set; }
            [ProtoBuf.ProtoMember(435)]
            public Inner435 Value435 { get; set; }
            [ProtoBuf.ProtoMember(436)]
            public Inner436 Value436 { get; set; }
            [ProtoBuf.ProtoMember(437)]
            public Inner437 Value437 { get; set; }
            [ProtoBuf.ProtoMember(438)]
            public Inner438 Value438 { get; set; }
            [ProtoBuf.ProtoMember(439)]
            public Inner439 Value439 { get; set; }
            [ProtoBuf.ProtoMember(440)]
            public Inner440 Value440 { get; set; }
            [ProtoBuf.ProtoMember(441)]
            public Inner441 Value441 { get; set; }
            [ProtoBuf.ProtoMember(442)]
            public Inner442 Value442 { get; set; }
            [ProtoBuf.ProtoMember(443)]
            public Inner443 Value443 { get; set; }
            [ProtoBuf.ProtoMember(444)]
            public Inner444 Value444 { get; set; }
            [ProtoBuf.ProtoMember(445)]
            public Inner445 Value445 { get; set; }
            [ProtoBuf.ProtoMember(446)]
            public Inner446 Value446 { get; set; }
            [ProtoBuf.ProtoMember(447)]
            public Inner447 Value447 { get; set; }
            [ProtoBuf.ProtoMember(448)]
            public Inner448 Value448 { get; set; }
            [ProtoBuf.ProtoMember(449)]
            public Inner449 Value449 { get; set; }
            [ProtoBuf.ProtoMember(450)]
            public Inner450 Value450 { get; set; }
            [ProtoBuf.ProtoMember(451)]
            public Inner451 Value451 { get; set; }
            [ProtoBuf.ProtoMember(452)]
            public Inner452 Value452 { get; set; }
            [ProtoBuf.ProtoMember(453)]
            public Inner453 Value453 { get; set; }
            [ProtoBuf.ProtoMember(454)]
            public Inner454 Value454 { get; set; }
            [ProtoBuf.ProtoMember(455)]
            public Inner455 Value455 { get; set; }
            [ProtoBuf.ProtoMember(456)]
            public Inner456 Value456 { get; set; }
            [ProtoBuf.ProtoMember(457)]
            public Inner457 Value457 { get; set; }
            [ProtoBuf.ProtoMember(458)]
            public Inner458 Value458 { get; set; }
            [ProtoBuf.ProtoMember(459)]
            public Inner459 Value459 { get; set; }
            [ProtoBuf.ProtoMember(460)]
            public Inner460 Value460 { get; set; }
            [ProtoBuf.ProtoMember(461)]
            public Inner461 Value461 { get; set; }
            [ProtoBuf.ProtoMember(462)]
            public Inner462 Value462 { get; set; }
            [ProtoBuf.ProtoMember(463)]
            public Inner463 Value463 { get; set; }
            [ProtoBuf.ProtoMember(464)]
            public Inner464 Value464 { get; set; }
            [ProtoBuf.ProtoMember(465)]
            public Inner465 Value465 { get; set; }
            [ProtoBuf.ProtoMember(466)]
            public Inner466 Value466 { get; set; }
            [ProtoBuf.ProtoMember(467)]
            public Inner467 Value467 { get; set; }
            [ProtoBuf.ProtoMember(468)]
            public Inner468 Value468 { get; set; }
            [ProtoBuf.ProtoMember(469)]
            public Inner469 Value469 { get; set; }
            [ProtoBuf.ProtoMember(470)]
            public Inner470 Value470 { get; set; }
            [ProtoBuf.ProtoMember(471)]
            public Inner471 Value471 { get; set; }
            [ProtoBuf.ProtoMember(472)]
            public Inner472 Value472 { get; set; }
            [ProtoBuf.ProtoMember(473)]
            public Inner473 Value473 { get; set; }
            [ProtoBuf.ProtoMember(474)]
            public Inner474 Value474 { get; set; }
            [ProtoBuf.ProtoMember(475)]
            public Inner475 Value475 { get; set; }
            [ProtoBuf.ProtoMember(476)]
            public Inner476 Value476 { get; set; }
            [ProtoBuf.ProtoMember(477)]
            public Inner477 Value477 { get; set; }
            [ProtoBuf.ProtoMember(478)]
            public Inner478 Value478 { get; set; }
            [ProtoBuf.ProtoMember(479)]
            public Inner479 Value479 { get; set; }
            [ProtoBuf.ProtoMember(480)]
            public Inner480 Value480 { get; set; }
            [ProtoBuf.ProtoMember(481)]
            public Inner481 Value481 { get; set; }
            [ProtoBuf.ProtoMember(482)]
            public Inner482 Value482 { get; set; }
            [ProtoBuf.ProtoMember(483)]
            public Inner483 Value483 { get; set; }
            [ProtoBuf.ProtoMember(484)]
            public Inner484 Value484 { get; set; }
            [ProtoBuf.ProtoMember(485)]
            public Inner485 Value485 { get; set; }
            [ProtoBuf.ProtoMember(486)]
            public Inner486 Value486 { get; set; }
            [ProtoBuf.ProtoMember(487)]
            public Inner487 Value487 { get; set; }
            [ProtoBuf.ProtoMember(488)]
            public Inner488 Value488 { get; set; }
            [ProtoBuf.ProtoMember(489)]
            public Inner489 Value489 { get; set; }
            [ProtoBuf.ProtoMember(490)]
            public Inner490 Value490 { get; set; }
            [ProtoBuf.ProtoMember(491)]
            public Inner491 Value491 { get; set; }
            [ProtoBuf.ProtoMember(492)]
            public Inner492 Value492 { get; set; }
            [ProtoBuf.ProtoMember(493)]
            public Inner493 Value493 { get; set; }
            [ProtoBuf.ProtoMember(494)]
            public Inner494 Value494 { get; set; }
            [ProtoBuf.ProtoMember(495)]
            public Inner495 Value495 { get; set; }
            [ProtoBuf.ProtoMember(496)]
            public Inner496 Value496 { get; set; }
            [ProtoBuf.ProtoMember(497)]
            public Inner497 Value497 { get; set; }
            [ProtoBuf.ProtoMember(498)]
            public Inner498 Value498 { get; set; }
            [ProtoBuf.ProtoMember(499)]
            public Inner499 Value499 { get; set; }
            [ProtoBuf.ProtoMember(500)]
            public Inner500 Value500 { get; set; }
            [ProtoBuf.ProtoMember(501)]
            public Inner501 Value501 { get; set; }
            [ProtoBuf.ProtoMember(502)]
            public Inner502 Value502 { get; set; }
            [ProtoBuf.ProtoMember(503)]
            public Inner503 Value503 { get; set; }
            [ProtoBuf.ProtoMember(504)]
            public Inner504 Value504 { get; set; }
            [ProtoBuf.ProtoMember(505)]
            public Inner505 Value505 { get; set; }
            [ProtoBuf.ProtoMember(506)]
            public Inner506 Value506 { get; set; }
            [ProtoBuf.ProtoMember(507)]
            public Inner507 Value507 { get; set; }
            [ProtoBuf.ProtoMember(508)]
            public Inner508 Value508 { get; set; }
            [ProtoBuf.ProtoMember(509)]
            public Inner509 Value509 { get; set; }
            [ProtoBuf.ProtoMember(510)]
            public Inner510 Value510 { get; set; }
            [ProtoBuf.ProtoMember(511)]
            public Inner511 Value511 { get; set; }
            [ProtoBuf.ProtoMember(512)]
            public Inner512 Value512 { get; set; }
            [ProtoBuf.ProtoMember(513)]
            public Inner513 Value513 { get; set; }
            [ProtoBuf.ProtoMember(514)]
            public Inner514 Value514 { get; set; }
            [ProtoBuf.ProtoMember(515)]
            public Inner515 Value515 { get; set; }
            [ProtoBuf.ProtoMember(516)]
            public Inner516 Value516 { get; set; }
            [ProtoBuf.ProtoMember(517)]
            public Inner517 Value517 { get; set; }
            [ProtoBuf.ProtoMember(518)]
            public Inner518 Value518 { get; set; }
            [ProtoBuf.ProtoMember(519)]
            public Inner519 Value519 { get; set; }
            [ProtoBuf.ProtoMember(520)]
            public Inner520 Value520 { get; set; }
            [ProtoBuf.ProtoMember(521)]
            public Inner521 Value521 { get; set; }
            [ProtoBuf.ProtoMember(522)]
            public Inner522 Value522 { get; set; }
            [ProtoBuf.ProtoMember(523)]
            public Inner523 Value523 { get; set; }
            [ProtoBuf.ProtoMember(524)]
            public Inner524 Value524 { get; set; }
            [ProtoBuf.ProtoMember(525)]
            public Inner525 Value525 { get; set; }
            [ProtoBuf.ProtoMember(526)]
            public Inner526 Value526 { get; set; }
            [ProtoBuf.ProtoMember(527)]
            public Inner527 Value527 { get; set; }
            [ProtoBuf.ProtoMember(528)]
            public Inner528 Value528 { get; set; }
            [ProtoBuf.ProtoMember(529)]
            public Inner529 Value529 { get; set; }
            [ProtoBuf.ProtoMember(530)]
            public Inner530 Value530 { get; set; }
            [ProtoBuf.ProtoMember(531)]
            public Inner531 Value531 { get; set; }
            [ProtoBuf.ProtoMember(532)]
            public Inner532 Value532 { get; set; }
            [ProtoBuf.ProtoMember(533)]
            public Inner533 Value533 { get; set; }
            [ProtoBuf.ProtoMember(534)]
            public Inner534 Value534 { get; set; }
            [ProtoBuf.ProtoMember(535)]
            public Inner535 Value535 { get; set; }
            [ProtoBuf.ProtoMember(536)]
            public Inner536 Value536 { get; set; }
            [ProtoBuf.ProtoMember(537)]
            public Inner537 Value537 { get; set; }
            [ProtoBuf.ProtoMember(538)]
            public Inner538 Value538 { get; set; }
            [ProtoBuf.ProtoMember(539)]
            public Inner539 Value539 { get; set; }
            [ProtoBuf.ProtoMember(540)]
            public Inner540 Value540 { get; set; }
            [ProtoBuf.ProtoMember(541)]
            public Inner541 Value541 { get; set; }
            [ProtoBuf.ProtoMember(542)]
            public Inner542 Value542 { get; set; }
            [ProtoBuf.ProtoMember(543)]
            public Inner543 Value543 { get; set; }
            [ProtoBuf.ProtoMember(544)]
            public Inner544 Value544 { get; set; }
            [ProtoBuf.ProtoMember(545)]
            public Inner545 Value545 { get; set; }
            [ProtoBuf.ProtoMember(546)]
            public Inner546 Value546 { get; set; }
            [ProtoBuf.ProtoMember(547)]
            public Inner547 Value547 { get; set; }
            [ProtoBuf.ProtoMember(548)]
            public Inner548 Value548 { get; set; }
            [ProtoBuf.ProtoMember(549)]
            public Inner549 Value549 { get; set; }
            [ProtoBuf.ProtoMember(550)]
            public Inner550 Value550 { get; set; }
            [ProtoBuf.ProtoMember(551)]
            public Inner551 Value551 { get; set; }
            [ProtoBuf.ProtoMember(552)]
            public Inner552 Value552 { get; set; }
            [ProtoBuf.ProtoMember(553)]
            public Inner553 Value553 { get; set; }
            [ProtoBuf.ProtoMember(554)]
            public Inner554 Value554 { get; set; }
            [ProtoBuf.ProtoMember(555)]
            public Inner555 Value555 { get; set; }
            [ProtoBuf.ProtoMember(556)]
            public Inner556 Value556 { get; set; }
            [ProtoBuf.ProtoMember(557)]
            public Inner557 Value557 { get; set; }
            [ProtoBuf.ProtoMember(558)]
            public Inner558 Value558 { get; set; }
            [ProtoBuf.ProtoMember(559)]
            public Inner559 Value559 { get; set; }
            [ProtoBuf.ProtoMember(560)]
            public Inner560 Value560 { get; set; }
            [ProtoBuf.ProtoMember(561)]
            public Inner561 Value561 { get; set; }
            [ProtoBuf.ProtoMember(562)]
            public Inner562 Value562 { get; set; }
            [ProtoBuf.ProtoMember(563)]
            public Inner563 Value563 { get; set; }
            [ProtoBuf.ProtoMember(564)]
            public Inner564 Value564 { get; set; }
            [ProtoBuf.ProtoMember(565)]
            public Inner565 Value565 { get; set; }
            [ProtoBuf.ProtoMember(566)]
            public Inner566 Value566 { get; set; }
            [ProtoBuf.ProtoMember(567)]
            public Inner567 Value567 { get; set; }
            [ProtoBuf.ProtoMember(568)]
            public Inner568 Value568 { get; set; }
            [ProtoBuf.ProtoMember(569)]
            public Inner569 Value569 { get; set; }
            [ProtoBuf.ProtoMember(570)]
            public Inner570 Value570 { get; set; }
            [ProtoBuf.ProtoMember(571)]
            public Inner571 Value571 { get; set; }
            [ProtoBuf.ProtoMember(572)]
            public Inner572 Value572 { get; set; }
            [ProtoBuf.ProtoMember(573)]
            public Inner573 Value573 { get; set; }
            [ProtoBuf.ProtoMember(574)]
            public Inner574 Value574 { get; set; }
            [ProtoBuf.ProtoMember(575)]
            public Inner575 Value575 { get; set; }
            [ProtoBuf.ProtoMember(576)]
            public Inner576 Value576 { get; set; }
            [ProtoBuf.ProtoMember(577)]
            public Inner577 Value577 { get; set; }
            [ProtoBuf.ProtoMember(578)]
            public Inner578 Value578 { get; set; }
            [ProtoBuf.ProtoMember(579)]
            public Inner579 Value579 { get; set; }
            [ProtoBuf.ProtoMember(580)]
            public Inner580 Value580 { get; set; }
            [ProtoBuf.ProtoMember(581)]
            public Inner581 Value581 { get; set; }
            [ProtoBuf.ProtoMember(582)]
            public Inner582 Value582 { get; set; }
            [ProtoBuf.ProtoMember(583)]
            public Inner583 Value583 { get; set; }
            [ProtoBuf.ProtoMember(584)]
            public Inner584 Value584 { get; set; }
            [ProtoBuf.ProtoMember(585)]
            public Inner585 Value585 { get; set; }
            [ProtoBuf.ProtoMember(586)]
            public Inner586 Value586 { get; set; }
            [ProtoBuf.ProtoMember(587)]
            public Inner587 Value587 { get; set; }
            [ProtoBuf.ProtoMember(588)]
            public Inner588 Value588 { get; set; }
            [ProtoBuf.ProtoMember(589)]
            public Inner589 Value589 { get; set; }
            [ProtoBuf.ProtoMember(590)]
            public Inner590 Value590 { get; set; }
            [ProtoBuf.ProtoMember(591)]
            public Inner591 Value591 { get; set; }
            [ProtoBuf.ProtoMember(592)]
            public Inner592 Value592 { get; set; }
            [ProtoBuf.ProtoMember(593)]
            public Inner593 Value593 { get; set; }
            [ProtoBuf.ProtoMember(594)]
            public Inner594 Value594 { get; set; }
            [ProtoBuf.ProtoMember(595)]
            public Inner595 Value595 { get; set; }
            [ProtoBuf.ProtoMember(596)]
            public Inner596 Value596 { get; set; }
            [ProtoBuf.ProtoMember(597)]
            public Inner597 Value597 { get; set; }
            [ProtoBuf.ProtoMember(598)]
            public Inner598 Value598 { get; set; }
            [ProtoBuf.ProtoMember(599)]
            public Inner599 Value599 { get; set; }
            [ProtoBuf.ProtoMember(600)]
            public Inner600 Value600 { get; set; }
            [ProtoBuf.ProtoMember(601)]
            public Inner601 Value601 { get; set; }
            [ProtoBuf.ProtoMember(602)]
            public Inner602 Value602 { get; set; }
            [ProtoBuf.ProtoMember(603)]
            public Inner603 Value603 { get; set; }
            [ProtoBuf.ProtoMember(604)]
            public Inner604 Value604 { get; set; }
            [ProtoBuf.ProtoMember(605)]
            public Inner605 Value605 { get; set; }
            [ProtoBuf.ProtoMember(606)]
            public Inner606 Value606 { get; set; }
            [ProtoBuf.ProtoMember(607)]
            public Inner607 Value607 { get; set; }
            [ProtoBuf.ProtoMember(608)]
            public Inner608 Value608 { get; set; }
            [ProtoBuf.ProtoMember(609)]
            public Inner609 Value609 { get; set; }
            [ProtoBuf.ProtoMember(610)]
            public Inner610 Value610 { get; set; }
            [ProtoBuf.ProtoMember(611)]
            public Inner611 Value611 { get; set; }
            [ProtoBuf.ProtoMember(612)]
            public Inner612 Value612 { get; set; }
            [ProtoBuf.ProtoMember(613)]
            public Inner613 Value613 { get; set; }
            [ProtoBuf.ProtoMember(614)]
            public Inner614 Value614 { get; set; }
            [ProtoBuf.ProtoMember(615)]
            public Inner615 Value615 { get; set; }
            [ProtoBuf.ProtoMember(616)]
            public Inner616 Value616 { get; set; }
            [ProtoBuf.ProtoMember(617)]
            public Inner617 Value617 { get; set; }
            [ProtoBuf.ProtoMember(618)]
            public Inner618 Value618 { get; set; }
            [ProtoBuf.ProtoMember(619)]
            public Inner619 Value619 { get; set; }
            [ProtoBuf.ProtoMember(620)]
            public Inner620 Value620 { get; set; }
            [ProtoBuf.ProtoMember(621)]
            public Inner621 Value621 { get; set; }
            [ProtoBuf.ProtoMember(622)]
            public Inner622 Value622 { get; set; }
            [ProtoBuf.ProtoMember(623)]
            public Inner623 Value623 { get; set; }
            [ProtoBuf.ProtoMember(624)]
            public Inner624 Value624 { get; set; }
            [ProtoBuf.ProtoMember(625)]
            public Inner625 Value625 { get; set; }
            [ProtoBuf.ProtoMember(626)]
            public Inner626 Value626 { get; set; }
            [ProtoBuf.ProtoMember(627)]
            public Inner627 Value627 { get; set; }
            [ProtoBuf.ProtoMember(628)]
            public Inner628 Value628 { get; set; }
            [ProtoBuf.ProtoMember(629)]
            public Inner629 Value629 { get; set; }
            [ProtoBuf.ProtoMember(630)]
            public Inner630 Value630 { get; set; }
            [ProtoBuf.ProtoMember(631)]
            public Inner631 Value631 { get; set; }
            [ProtoBuf.ProtoMember(632)]
            public Inner632 Value632 { get; set; }
            [ProtoBuf.ProtoMember(633)]
            public Inner633 Value633 { get; set; }
            [ProtoBuf.ProtoMember(634)]
            public Inner634 Value634 { get; set; }
            [ProtoBuf.ProtoMember(635)]
            public Inner635 Value635 { get; set; }
            [ProtoBuf.ProtoMember(636)]
            public Inner636 Value636 { get; set; }
            [ProtoBuf.ProtoMember(637)]
            public Inner637 Value637 { get; set; }
            [ProtoBuf.ProtoMember(638)]
            public Inner638 Value638 { get; set; }
            [ProtoBuf.ProtoMember(639)]
            public Inner639 Value639 { get; set; }
            [ProtoBuf.ProtoMember(640)]
            public Inner640 Value640 { get; set; }
            [ProtoBuf.ProtoMember(641)]
            public Inner641 Value641 { get; set; }
            [ProtoBuf.ProtoMember(642)]
            public Inner642 Value642 { get; set; }
            [ProtoBuf.ProtoMember(643)]
            public Inner643 Value643 { get; set; }
            [ProtoBuf.ProtoMember(644)]
            public Inner644 Value644 { get; set; }
            [ProtoBuf.ProtoMember(645)]
            public Inner645 Value645 { get; set; }
            [ProtoBuf.ProtoMember(646)]
            public Inner646 Value646 { get; set; }
            [ProtoBuf.ProtoMember(647)]
            public Inner647 Value647 { get; set; }
            [ProtoBuf.ProtoMember(648)]
            public Inner648 Value648 { get; set; }
            [ProtoBuf.ProtoMember(649)]
            public Inner649 Value649 { get; set; }
            [ProtoBuf.ProtoMember(650)]
            public Inner650 Value650 { get; set; }
            [ProtoBuf.ProtoMember(651)]
            public Inner651 Value651 { get; set; }
            [ProtoBuf.ProtoMember(652)]
            public Inner652 Value652 { get; set; }
            [ProtoBuf.ProtoMember(653)]
            public Inner653 Value653 { get; set; }
            [ProtoBuf.ProtoMember(654)]
            public Inner654 Value654 { get; set; }
            [ProtoBuf.ProtoMember(655)]
            public Inner655 Value655 { get; set; }
            [ProtoBuf.ProtoMember(656)]
            public Inner656 Value656 { get; set; }
            [ProtoBuf.ProtoMember(657)]
            public Inner657 Value657 { get; set; }
            [ProtoBuf.ProtoMember(658)]
            public Inner658 Value658 { get; set; }
            [ProtoBuf.ProtoMember(659)]
            public Inner659 Value659 { get; set; }
            [ProtoBuf.ProtoMember(660)]
            public Inner660 Value660 { get; set; }
            [ProtoBuf.ProtoMember(661)]
            public Inner661 Value661 { get; set; }
            [ProtoBuf.ProtoMember(662)]
            public Inner662 Value662 { get; set; }
            [ProtoBuf.ProtoMember(663)]
            public Inner663 Value663 { get; set; }
            [ProtoBuf.ProtoMember(664)]
            public Inner664 Value664 { get; set; }
            [ProtoBuf.ProtoMember(665)]
            public Inner665 Value665 { get; set; }
            [ProtoBuf.ProtoMember(666)]
            public Inner666 Value666 { get; set; }
            [ProtoBuf.ProtoMember(667)]
            public Inner667 Value667 { get; set; }
            [ProtoBuf.ProtoMember(668)]
            public Inner668 Value668 { get; set; }
            [ProtoBuf.ProtoMember(669)]
            public Inner669 Value669 { get; set; }
            [ProtoBuf.ProtoMember(670)]
            public Inner670 Value670 { get; set; }
            [ProtoBuf.ProtoMember(671)]
            public Inner671 Value671 { get; set; }
            [ProtoBuf.ProtoMember(672)]
            public Inner672 Value672 { get; set; }
            [ProtoBuf.ProtoMember(673)]
            public Inner673 Value673 { get; set; }
            [ProtoBuf.ProtoMember(674)]
            public Inner674 Value674 { get; set; }
            [ProtoBuf.ProtoMember(675)]
            public Inner675 Value675 { get; set; }
            [ProtoBuf.ProtoMember(676)]
            public Inner676 Value676 { get; set; }
            [ProtoBuf.ProtoMember(677)]
            public Inner677 Value677 { get; set; }
            [ProtoBuf.ProtoMember(678)]
            public Inner678 Value678 { get; set; }
            [ProtoBuf.ProtoMember(679)]
            public Inner679 Value679 { get; set; }
            [ProtoBuf.ProtoMember(680)]
            public Inner680 Value680 { get; set; }
            [ProtoBuf.ProtoMember(681)]
            public Inner681 Value681 { get; set; }
            [ProtoBuf.ProtoMember(682)]
            public Inner682 Value682 { get; set; }
            [ProtoBuf.ProtoMember(683)]
            public Inner683 Value683 { get; set; }
            [ProtoBuf.ProtoMember(684)]
            public Inner684 Value684 { get; set; }
            [ProtoBuf.ProtoMember(685)]
            public Inner685 Value685 { get; set; }
            [ProtoBuf.ProtoMember(686)]
            public Inner686 Value686 { get; set; }
            [ProtoBuf.ProtoMember(687)]
            public Inner687 Value687 { get; set; }
            [ProtoBuf.ProtoMember(688)]
            public Inner688 Value688 { get; set; }
            [ProtoBuf.ProtoMember(689)]
            public Inner689 Value689 { get; set; }
            [ProtoBuf.ProtoMember(690)]
            public Inner690 Value690 { get; set; }
            [ProtoBuf.ProtoMember(691)]
            public Inner691 Value691 { get; set; }
            [ProtoBuf.ProtoMember(692)]
            public Inner692 Value692 { get; set; }
            [ProtoBuf.ProtoMember(693)]
            public Inner693 Value693 { get; set; }
            [ProtoBuf.ProtoMember(694)]
            public Inner694 Value694 { get; set; }
            [ProtoBuf.ProtoMember(695)]
            public Inner695 Value695 { get; set; }
            [ProtoBuf.ProtoMember(696)]
            public Inner696 Value696 { get; set; }
            [ProtoBuf.ProtoMember(697)]
            public Inner697 Value697 { get; set; }
            [ProtoBuf.ProtoMember(698)]
            public Inner698 Value698 { get; set; }
            [ProtoBuf.ProtoMember(699)]
            public Inner699 Value699 { get; set; }
            [ProtoBuf.ProtoMember(700)]
            public Inner700 Value700 { get; set; }
            [ProtoBuf.ProtoMember(701)]
            public Inner701 Value701 { get; set; }
            [ProtoBuf.ProtoMember(702)]
            public Inner702 Value702 { get; set; }
            [ProtoBuf.ProtoMember(703)]
            public Inner703 Value703 { get; set; }
            [ProtoBuf.ProtoMember(704)]
            public Inner704 Value704 { get; set; }
            [ProtoBuf.ProtoMember(705)]
            public Inner705 Value705 { get; set; }
            [ProtoBuf.ProtoMember(706)]
            public Inner706 Value706 { get; set; }
            [ProtoBuf.ProtoMember(707)]
            public Inner707 Value707 { get; set; }
            [ProtoBuf.ProtoMember(708)]
            public Inner708 Value708 { get; set; }
            [ProtoBuf.ProtoMember(709)]
            public Inner709 Value709 { get; set; }
            [ProtoBuf.ProtoMember(710)]
            public Inner710 Value710 { get; set; }
            [ProtoBuf.ProtoMember(711)]
            public Inner711 Value711 { get; set; }
            [ProtoBuf.ProtoMember(712)]
            public Inner712 Value712 { get; set; }
            [ProtoBuf.ProtoMember(713)]
            public Inner713 Value713 { get; set; }
            [ProtoBuf.ProtoMember(714)]
            public Inner714 Value714 { get; set; }
            [ProtoBuf.ProtoMember(715)]
            public Inner715 Value715 { get; set; }
            [ProtoBuf.ProtoMember(716)]
            public Inner716 Value716 { get; set; }
            [ProtoBuf.ProtoMember(717)]
            public Inner717 Value717 { get; set; }
            [ProtoBuf.ProtoMember(718)]
            public Inner718 Value718 { get; set; }
            [ProtoBuf.ProtoMember(719)]
            public Inner719 Value719 { get; set; }
            [ProtoBuf.ProtoMember(720)]
            public Inner720 Value720 { get; set; }
            [ProtoBuf.ProtoMember(721)]
            public Inner721 Value721 { get; set; }
            [ProtoBuf.ProtoMember(722)]
            public Inner722 Value722 { get; set; }
            [ProtoBuf.ProtoMember(723)]
            public Inner723 Value723 { get; set; }
            [ProtoBuf.ProtoMember(724)]
            public Inner724 Value724 { get; set; }
            [ProtoBuf.ProtoMember(725)]
            public Inner725 Value725 { get; set; }
            [ProtoBuf.ProtoMember(726)]
            public Inner726 Value726 { get; set; }
            [ProtoBuf.ProtoMember(727)]
            public Inner727 Value727 { get; set; }
            [ProtoBuf.ProtoMember(728)]
            public Inner728 Value728 { get; set; }
            [ProtoBuf.ProtoMember(729)]
            public Inner729 Value729 { get; set; }
            [ProtoBuf.ProtoMember(730)]
            public Inner730 Value730 { get; set; }
            [ProtoBuf.ProtoMember(731)]
            public Inner731 Value731 { get; set; }
            [ProtoBuf.ProtoMember(732)]
            public Inner732 Value732 { get; set; }
            [ProtoBuf.ProtoMember(733)]
            public Inner733 Value733 { get; set; }
            [ProtoBuf.ProtoMember(734)]
            public Inner734 Value734 { get; set; }
            [ProtoBuf.ProtoMember(735)]
            public Inner735 Value735 { get; set; }
            [ProtoBuf.ProtoMember(736)]
            public Inner736 Value736 { get; set; }
            [ProtoBuf.ProtoMember(737)]
            public Inner737 Value737 { get; set; }
            [ProtoBuf.ProtoMember(738)]
            public Inner738 Value738 { get; set; }
            [ProtoBuf.ProtoMember(739)]
            public Inner739 Value739 { get; set; }
            [ProtoBuf.ProtoMember(740)]
            public Inner740 Value740 { get; set; }
            [ProtoBuf.ProtoMember(741)]
            public Inner741 Value741 { get; set; }
            [ProtoBuf.ProtoMember(742)]
            public Inner742 Value742 { get; set; }
            [ProtoBuf.ProtoMember(743)]
            public Inner743 Value743 { get; set; }
            [ProtoBuf.ProtoMember(744)]
            public Inner744 Value744 { get; set; }
            [ProtoBuf.ProtoMember(745)]
            public Inner745 Value745 { get; set; }
            [ProtoBuf.ProtoMember(746)]
            public Inner746 Value746 { get; set; }
            [ProtoBuf.ProtoMember(747)]
            public Inner747 Value747 { get; set; }
            [ProtoBuf.ProtoMember(748)]
            public Inner748 Value748 { get; set; }
            [ProtoBuf.ProtoMember(749)]
            public Inner749 Value749 { get; set; }
            [ProtoBuf.ProtoMember(750)]
            public Inner750 Value750 { get; set; }
            [ProtoBuf.ProtoMember(751)]
            public Inner751 Value751 { get; set; }
            [ProtoBuf.ProtoMember(752)]
            public Inner752 Value752 { get; set; }
            [ProtoBuf.ProtoMember(753)]
            public Inner753 Value753 { get; set; }
            [ProtoBuf.ProtoMember(754)]
            public Inner754 Value754 { get; set; }
            [ProtoBuf.ProtoMember(755)]
            public Inner755 Value755 { get; set; }
            [ProtoBuf.ProtoMember(756)]
            public Inner756 Value756 { get; set; }
            [ProtoBuf.ProtoMember(757)]
            public Inner757 Value757 { get; set; }
            [ProtoBuf.ProtoMember(758)]
            public Inner758 Value758 { get; set; }
            [ProtoBuf.ProtoMember(759)]
            public Inner759 Value759 { get; set; }
            [ProtoBuf.ProtoMember(760)]
            public Inner760 Value760 { get; set; }
            [ProtoBuf.ProtoMember(761)]
            public Inner761 Value761 { get; set; }
            [ProtoBuf.ProtoMember(762)]
            public Inner762 Value762 { get; set; }
            [ProtoBuf.ProtoMember(763)]
            public Inner763 Value763 { get; set; }
            [ProtoBuf.ProtoMember(764)]
            public Inner764 Value764 { get; set; }
            [ProtoBuf.ProtoMember(765)]
            public Inner765 Value765 { get; set; }
            [ProtoBuf.ProtoMember(766)]
            public Inner766 Value766 { get; set; }
            [ProtoBuf.ProtoMember(767)]
            public Inner767 Value767 { get; set; }
            [ProtoBuf.ProtoMember(768)]
            public Inner768 Value768 { get; set; }
            [ProtoBuf.ProtoMember(769)]
            public Inner769 Value769 { get; set; }
            [ProtoBuf.ProtoMember(770)]
            public Inner770 Value770 { get; set; }
            [ProtoBuf.ProtoMember(771)]
            public Inner771 Value771 { get; set; }
            [ProtoBuf.ProtoMember(772)]
            public Inner772 Value772 { get; set; }
            [ProtoBuf.ProtoMember(773)]
            public Inner773 Value773 { get; set; }
            [ProtoBuf.ProtoMember(774)]
            public Inner774 Value774 { get; set; }
            [ProtoBuf.ProtoMember(775)]
            public Inner775 Value775 { get; set; }
            [ProtoBuf.ProtoMember(776)]
            public Inner776 Value776 { get; set; }
            [ProtoBuf.ProtoMember(777)]
            public Inner777 Value777 { get; set; }
            [ProtoBuf.ProtoMember(778)]
            public Inner778 Value778 { get; set; }
            [ProtoBuf.ProtoMember(779)]
            public Inner779 Value779 { get; set; }
            [ProtoBuf.ProtoMember(780)]
            public Inner780 Value780 { get; set; }
            [ProtoBuf.ProtoMember(781)]
            public Inner781 Value781 { get; set; }
            [ProtoBuf.ProtoMember(782)]
            public Inner782 Value782 { get; set; }
            [ProtoBuf.ProtoMember(783)]
            public Inner783 Value783 { get; set; }
            [ProtoBuf.ProtoMember(784)]
            public Inner784 Value784 { get; set; }
            [ProtoBuf.ProtoMember(785)]
            public Inner785 Value785 { get; set; }
            [ProtoBuf.ProtoMember(786)]
            public Inner786 Value786 { get; set; }
            [ProtoBuf.ProtoMember(787)]
            public Inner787 Value787 { get; set; }
            [ProtoBuf.ProtoMember(788)]
            public Inner788 Value788 { get; set; }
            [ProtoBuf.ProtoMember(789)]
            public Inner789 Value789 { get; set; }
            [ProtoBuf.ProtoMember(790)]
            public Inner790 Value790 { get; set; }
            [ProtoBuf.ProtoMember(791)]
            public Inner791 Value791 { get; set; }
            [ProtoBuf.ProtoMember(792)]
            public Inner792 Value792 { get; set; }
            [ProtoBuf.ProtoMember(793)]
            public Inner793 Value793 { get; set; }
            [ProtoBuf.ProtoMember(794)]
            public Inner794 Value794 { get; set; }
            [ProtoBuf.ProtoMember(795)]
            public Inner795 Value795 { get; set; }
            [ProtoBuf.ProtoMember(796)]
            public Inner796 Value796 { get; set; }
            [ProtoBuf.ProtoMember(797)]
            public Inner797 Value797 { get; set; }
            [ProtoBuf.ProtoMember(798)]
            public Inner798 Value798 { get; set; }
            [ProtoBuf.ProtoMember(799)]
            public Inner799 Value799 { get; set; }
            [ProtoBuf.ProtoMember(800)]
            public Inner800 Value800 { get; set; }
        }
        [ProtoBuf.ProtoContract]
        public class Inner1 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner2 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner3 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner4 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner5 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner6 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner7 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner8 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner9 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner10 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner11 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner12 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner13 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner14 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner15 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner16 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner17 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner18 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner19 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner20 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner21 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner22 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner23 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner24 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner25 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner26 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner27 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner28 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner29 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner30 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner31 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner32 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner33 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner34 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner35 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner36 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner37 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner38 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner39 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner40 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner41 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner42 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner43 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner44 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner45 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner46 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner47 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner48 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner49 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner50 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner51 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner52 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner53 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner54 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner55 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner56 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner57 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner58 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner59 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner60 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner61 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner62 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner63 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner64 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner65 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner66 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner67 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner68 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner69 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner70 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner71 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner72 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner73 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner74 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner75 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner76 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner77 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner78 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner79 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner80 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner81 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner82 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner83 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner84 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner85 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner86 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner87 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner88 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner89 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner90 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner91 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner92 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner93 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner94 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner95 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner96 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner97 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner98 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner99 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner100 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner101 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner102 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner103 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner104 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner105 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner106 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner107 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner108 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner109 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner110 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner111 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner112 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner113 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner114 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner115 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner116 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner117 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner118 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner119 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner120 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner121 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner122 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner123 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner124 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner125 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner126 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner127 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner128 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner129 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner130 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner131 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner132 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner133 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner134 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner135 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner136 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner137 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner138 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner139 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner140 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner141 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner142 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner143 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner144 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner145 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner146 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner147 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner148 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner149 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner150 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner151 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner152 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner153 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner154 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner155 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner156 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner157 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner158 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner159 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner160 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner161 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner162 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner163 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner164 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner165 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner166 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner167 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner168 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner169 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner170 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner171 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner172 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner173 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner174 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner175 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner176 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner177 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner178 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner179 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner180 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner181 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner182 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner183 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner184 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner185 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner186 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner187 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner188 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner189 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner190 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner191 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner192 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner193 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner194 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner195 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner196 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner197 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner198 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner199 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner200 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner201 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner202 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner203 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner204 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner205 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner206 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner207 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner208 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner209 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner210 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner211 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner212 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner213 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner214 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner215 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner216 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner217 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner218 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner219 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner220 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner221 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner222 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner223 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner224 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner225 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner226 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner227 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner228 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner229 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner230 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner231 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner232 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner233 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner234 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner235 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner236 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner237 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner238 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner239 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner240 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner241 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner242 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner243 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner244 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner245 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner246 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner247 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner248 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner249 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner250 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner251 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner252 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner253 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner254 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner255 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner256 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner257 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner258 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner259 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner260 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner261 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner262 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner263 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner264 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner265 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner266 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner267 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner268 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner269 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner270 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner271 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner272 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner273 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner274 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner275 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner276 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner277 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner278 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner279 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner280 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner281 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner282 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner283 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner284 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner285 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner286 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner287 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner288 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner289 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner290 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner291 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner292 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner293 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner294 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner295 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner296 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner297 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner298 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner299 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner300 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner301 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner302 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner303 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner304 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner305 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner306 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner307 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner308 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner309 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner310 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner311 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner312 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner313 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner314 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner315 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner316 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner317 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner318 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner319 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner320 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner321 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner322 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner323 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner324 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner325 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner326 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner327 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner328 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner329 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner330 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner331 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner332 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner333 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner334 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner335 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner336 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner337 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner338 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner339 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner340 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner341 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner342 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner343 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner344 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner345 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner346 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner347 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner348 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner349 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner350 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner351 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner352 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner353 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner354 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner355 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner356 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner357 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner358 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner359 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner360 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner361 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner362 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner363 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner364 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner365 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner366 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner367 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner368 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner369 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner370 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner371 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner372 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner373 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner374 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner375 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner376 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner377 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner378 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner379 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner380 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner381 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner382 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner383 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner384 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner385 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner386 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner387 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner388 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner389 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner390 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner391 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner392 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner393 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner394 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner395 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner396 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner397 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner398 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner399 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner400 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner401 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner402 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner403 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner404 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner405 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner406 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner407 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner408 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner409 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner410 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner411 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner412 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner413 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner414 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner415 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner416 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner417 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner418 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner419 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner420 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner421 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner422 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner423 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner424 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner425 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner426 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner427 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner428 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner429 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner430 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner431 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner432 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner433 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner434 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner435 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner436 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner437 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner438 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner439 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner440 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner441 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner442 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner443 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner444 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner445 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner446 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner447 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner448 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner449 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner450 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner451 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner452 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner453 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner454 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner455 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner456 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner457 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner458 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner459 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner460 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner461 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner462 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner463 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner464 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner465 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner466 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner467 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner468 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner469 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner470 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner471 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner472 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner473 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner474 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner475 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner476 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner477 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner478 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner479 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner480 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner481 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner482 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner483 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner484 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner485 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner486 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner487 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner488 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner489 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner490 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner491 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner492 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner493 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner494 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner495 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner496 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner497 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner498 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner499 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner500 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner501 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner502 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner503 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner504 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner505 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner506 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner507 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner508 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner509 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner510 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner511 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner512 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner513 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner514 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner515 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner516 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner517 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner518 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner519 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner520 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner521 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner522 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner523 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner524 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner525 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner526 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner527 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner528 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner529 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner530 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner531 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner532 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner533 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner534 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner535 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner536 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner537 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner538 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner539 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner540 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner541 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner542 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner543 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner544 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner545 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner546 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner547 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner548 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner549 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner550 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner551 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner552 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner553 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner554 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner555 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner556 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner557 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner558 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner559 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner560 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner561 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner562 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner563 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner564 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner565 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner566 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner567 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner568 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner569 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner570 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner571 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner572 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner573 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner574 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner575 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner576 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner577 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner578 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner579 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner580 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner581 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner582 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner583 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner584 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner585 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner586 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner587 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner588 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner589 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner590 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner591 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner592 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner593 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner594 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner595 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner596 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner597 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner598 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner599 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner600 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner601 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner602 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner603 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner604 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner605 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner606 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner607 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner608 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner609 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner610 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner611 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner612 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner613 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner614 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner615 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner616 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner617 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner618 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner619 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner620 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner621 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner622 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner623 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner624 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner625 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner626 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner627 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner628 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner629 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner630 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner631 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner632 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner633 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner634 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner635 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner636 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner637 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner638 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner639 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner640 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner641 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner642 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner643 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner644 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner645 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner646 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner647 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner648 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner649 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner650 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner651 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner652 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner653 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner654 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner655 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner656 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner657 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner658 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner659 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner660 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner661 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner662 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner663 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner664 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner665 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner666 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner667 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner668 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner669 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner670 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner671 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner672 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner673 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner674 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner675 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner676 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner677 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner678 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner679 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner680 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner681 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner682 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner683 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner684 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner685 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner686 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner687 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner688 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner689 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner690 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner691 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner692 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner693 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner694 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner695 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner696 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner697 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner698 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner699 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner700 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner701 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner702 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner703 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner704 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner705 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner706 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner707 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner708 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner709 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner710 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner711 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner712 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner713 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner714 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner715 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner716 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner717 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner718 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner719 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner720 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner721 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner722 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner723 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner724 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner725 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner726 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner727 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner728 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner729 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner730 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner731 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner732 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner733 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner734 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner735 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner736 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner737 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner738 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner739 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner740 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner741 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner742 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner743 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner744 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner745 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner746 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner747 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner748 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner749 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner750 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner751 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner752 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner753 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner754 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner755 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner756 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner757 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner758 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner759 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner760 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner761 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner762 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner763 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner764 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner765 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner766 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner767 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner768 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner769 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner770 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner771 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner772 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner773 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner774 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner775 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner776 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner777 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner778 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner779 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner780 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner781 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner782 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner783 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner784 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner785 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner786 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner787 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner788 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner789 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner790 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner791 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner792 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner793 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner794 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner795 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner796 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner797 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner798 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner799 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }
        [ProtoBuf.ProtoContract]
        public class Inner800 { [ProtoBuf.ProtoMember(1)] public int Value { get; set; } }

    }
}
