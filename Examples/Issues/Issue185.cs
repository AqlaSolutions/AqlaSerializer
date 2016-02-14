// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AqlaSerializer;
using NUnit.Framework;
using AqlaSerializer.Meta;
using System.IO;
using NUnit.Framework.Constraints;

namespace Examples.Issues
{
    public interface I { int N { get; } }

    public class O : I
    {
        public O(int n) { N = n; }
        public int N { get; private set; }
    }

    [ProtoBuf.ProtoContract]
    public class OS
    {
        public static implicit operator O(OS o)
        { return o == null ? null : new O(o.N); }
        public static implicit operator OS(O o)
        { return o == null ? null : new OS { N = o.N }; }
        [ProtoBuf.ProtoMember(1)]
        public int N { get; set; }
    }

    public class C
    {
        public static implicit operator CS(C o)
        { return o == null ? null : new CS { Unknown = o.Unknown }; }
        public static implicit operator C(CS o)
        { return o == null ? null : new C { Unknown = o.Unknown }; }
        public void PopulateRun() { Unknown = new O(43); }
        public I Unknown { get; private set; }
    }

    [ProtoBuf.ProtoContract]
    public class CS
    {
        [ProtoBuf.ProtoMember(1)]
        public I Unknown { get; set; }
    }
    [TestFixture]
    public class Issue185
    {
        [Test]
        public void ExecuteWithConstructType()
        {
            var m = RuntimeTypeModel.Create();
            m.AutoCompile = false;
            m.Add(typeof(C), false).SetSurrogate(typeof(CS));
            m.Add(typeof(O), false).SetSurrogate(typeof(OS));
            m.Add(typeof(I), false).ConstructType = typeof(O);

            var c = new C();
            c.PopulateRun();

            Func<IResolveConstraint> check = () => Throws.ArgumentException.With.Message.StartsWith("The supplied default implementation cannot be created: Examples.Issues.O");
            Assert.That(() => Test(m, c, "Runtime"), check());
            Assert.That(() => m.CompileInPlace(), check());
            Assert.That(() => Test(m, c, "CompileInPlace"), check());
            Assert.That(() => Test(m.Compile(), c, "Compile"), check());

        }
        static void Test(TypeModel model, C c, string caption)
        {
            Assert.AreEqual(43, c.Unknown.N, "braindead");
            using (var ms = new MemoryStream())
            {
                model.Serialize(ms, c);
                Assert.Greater(1, 0, "args fail");
                Assert.Greater(ms.Length, 0, "Nothing written");
                ms.Position = 0;
                var c2 = (C)model.Deserialize(ms, null, typeof(C));
                Assert.AreEqual(c.Unknown.N, c2.Unknown.N, caption);
            }
        }
        [Test]
        public void ExecuteWithSubType()
        {
            ProtoCompatibilitySettings comp=ProtoCompatibilitySettings.Default;
            // late reference mode is not allowed on surrogates
            // TODO move LateReference mode to attributes
            comp.AllowExtensionDefinitions &= ~NetObjectExtensionTypes.LateReference;
            var m = TypeModel.Create(false, comp);
            m.AutoCompile = false;
            m.Add(typeof(C), false).SetSurrogate(typeof(CS));
            m.Add(typeof(O), false).SetSurrogate(typeof(OS));
            m.Add(typeof(I), false).AddSubType(1, typeof(O));

            var c = new C();
            c.PopulateRun();

            Test(m, c, "Runtime");
            m.Compile("ExecuteWithSubType", "ExecuteWithSubType.dll");
            PEVerify.AssertValid("ExecuteWithSubType.dll");
            //m.CompileInPlace();
            Test(m, c, "CompileInPlace");
            Test(m.Compile(), c, "Compile");
        }
    }
}

