// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using System.IO;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue199
    {
        [ProtoBuf.ProtoContract]
        public class DodgyDefault
        {
            public DodgyDefault()
            {
                Value = true;
            }
            [ProtoBuf.ProtoMember(1)]
            public bool Value { get; set; }
        }

        [Test]
        public void CompareWithWithoutImplicitDefaults()
        {
            var with = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
            var without = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
            without.AutoCompile = with.AutoCompile = false;
            with.UseImplicitZeroDefaults = true;
            without.UseImplicitZeroDefaults = false;
                        

            Test(with, without, "Runtime");
            with.CompileInPlace();
            without.CompileInPlace();
            Test(with, without, "CompileInPlace");
            Test(with.Compile(), without.Compile(), "Compile");
        }

        [Test, ExpectedException(typeof(InvalidOperationException))]
        public void TryToDisableDefualtsOnDefault()
        {
            RuntimeTypeModel.Default.UseImplicitZeroDefaults = false;
        }
        [Test]
        public void CanEnalbeDefualtsOnDefault()
        {
            RuntimeTypeModel.Default.UseImplicitZeroDefaults = true;
            Assert.IsTrue(RuntimeTypeModel.Default.UseImplicitZeroDefaults);
        }
        static void Test(TypeModel with, TypeModel without, string message)
        {
            var obj = new DodgyDefault { Value = false };

            DodgyDefault c1 = (DodgyDefault)with.DeepClone(obj);
            Assert.IsTrue(c1.Value, message);
            DodgyDefault c2 = (DodgyDefault)without.DeepClone(obj);
            Assert.IsFalse(c2.Value, message);

            using (var ms = new MemoryStream())
            {
                with.Serialize(ms, obj);
                Assert.AreEqual(0, ms.Length, message);
            }
            using (var ms = new MemoryStream())
            {
                without.Serialize(ms, obj);

                Assert.AreEqual(2, ms.Length, message);
            }
        }
    }
}
