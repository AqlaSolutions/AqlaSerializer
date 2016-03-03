// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using NUnit.Framework;
using AqlaSerializer;
using AqlaSerializer.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using AqlaSerializer.Settings;

namespace Examples.Issues
{
    [TestFixture]
    public class SO14436606
    {
        [Serializable]
        [ProtoBuf.ProtoContract]
        public class A
        {
        }

        [Serializable]
        [ProtoBuf.ProtoContract]
        public class B
        {
            [ProtoBuf.ProtoMember(1, AsReference = true)]
            public A A { get; set; }

            [ProtoBuf.ProtoMember(2, AsReference = true)]
            public Dictionary<int, A> Items { get; set; }

            public B()
            {
                Items = new Dictionary<int, A>();
            }
        }
        [ProtoBuf.ProtoContract(AsReferenceDefault=true)]
        public class A_WithDefaultRef
        {
        }

        [ProtoBuf.ProtoContract]
        public class B_WithDefaultRef
        {
            [ProtoBuf.ProtoMember(1, AsReference = true)] // yes, AsReferenceDefault is applied only when adding missing members
            public A_WithDefaultRef A { get; set; }

            [ProtoBuf.ProtoMember(2)]
            public Dictionary<int, A_WithDefaultRef> Items { get; set; }

            public B_WithDefaultRef()
            {
                Items = new Dictionary<int, A_WithDefaultRef>();
            }
        }

        [ProtoBuf.ProtoContract]
        struct RefPair<TKey,TValue> {
            [ProtoBuf.ProtoMember(1)]
            public TKey Key {get; private set;}
            [ProtoBuf.ProtoMember(2, AsReference = true)]
            public TValue Value {get; private set;}
            public RefPair(TKey key, TValue value) : this() {
                Key = key;
                Value = value;
            }
            public static implicit operator KeyValuePair<TKey,TValue> (RefPair<TKey,TValue> val) {
                return new KeyValuePair<TKey,TValue>(val.Key, val.Value);
            }
            public static implicit operator RefPair<TKey,TValue> (KeyValuePair<TKey,TValue> val) {
                return new RefPair<TKey,TValue>(val.Key, val.Value);
            }
        }

        [Test]
        public void VerifyModelViaDefaultRef_AFirst()
        {
            var model = CreateDefaultRefModel(true, false);
            Assert.IsTrue(model[typeof(A_WithDefaultRef)].AsReferenceDefault, "A:AsReferenceDefault - A first");
            Assert.IsTrue(model[typeof(B_WithDefaultRef)][1].GetSettingsCopy().Format == ValueFormat.Reference, "B.A:AsReference  - A first");

        }
        [Test]
        public void VerifyModelViaDefaultRef_BFirst()
        {
            var model = CreateDefaultRefModel(false, false);
            Assert.IsTrue(model[typeof(A_WithDefaultRef)].AsReferenceDefault, "A:AsReferenceDefault - B first");
            Assert.IsTrue(model[typeof(B_WithDefaultRef)][1].GetSettingsCopy().Format == ValueFormat.Reference, "B.A:AsReference  - B first");
        }

        [Test]
        public void ThreeApproachesAreCompatible()
        {
            string surrogate, fields, defaultRef_AFirst, defaultRef_BFirst;
            string defaultRef_AFirst_proto;
            string fields_proto;
            string surrogate_proto;
            // Items should be not as reference but element A.Value should
            
            using (var ms = new MemoryStream())
            {
                RuntimeTypeModel m = CreateDefaultRefModel(true, true);
                m.Serialize(ms, CreateB_WithDefaultRef());
                defaultRef_AFirst = BitConverter.ToString(ms.GetBuffer(), 0, (int)ms.Length);
                defaultRef_AFirst_proto = m.GetDebugSchema(typeof(B_WithDefaultRef));
            }
            using (var ms = new MemoryStream())
            {
                CreateDefaultRefModel(false, true).Serialize(ms, CreateB_WithDefaultRef());
                defaultRef_BFirst = BitConverter.ToString(ms.GetBuffer(), 0, (int)ms.Length);
            }
            using (var ms = new MemoryStream())
            {
                RuntimeTypeModel m = CreateSurrogateModel(true);
                m.Serialize(ms, CreateB());
                surrogate = BitConverter.ToString(ms.GetBuffer(), 0, (int)ms.Length);
                surrogate_proto = m.GetDebugSchema(typeof(B));
            }

            using (var ms = new MemoryStream())
            {
                RuntimeTypeModel m = CreateFieldsModel(true);
                m.Serialize(ms, CreateB());
                fields = BitConverter.ToString(ms.GetBuffer(), 0, (int)ms.Length);
                fields_proto = m.GetDebugSchema(typeof(B));
            }
            
            Assert.AreEqual(surrogate, fields, "fields vs surrogate");
            Assert.AreEqual(surrogate, defaultRef_AFirst, "default-ref (A-first) vs surrogate");
            Assert.AreEqual(surrogate, defaultRef_BFirst, "default-ref (B-first) vs surrogate");
        }

        [Test]
        public void ExecuteHackedViaDefaultRef()
        {
            ExecuteAllModes_WithDefaultRef(CreateDefaultRefModel(true, false), "ExecuteHackedViaDefaultRef - A first");
            ExecuteAllModes_WithDefaultRef(CreateDefaultRefModel(false, false), "ExecuteHackedViaDefaultRef - B first");
        }

        [Test]
        public void ExecuteHackedViaFields()
        {
            RuntimeTypeModel tm = CreateFieldsModel(false);
            tm.SkipCompiledVsNotCheck = true;
            ExecuteAllModes(tm, standalone: false);
        }

        static RuntimeTypeModel CreateDefaultRefModel(bool aFirst, bool comp)
        {
            ProtoCompatibilitySettingsValue fullComp = ProtoCompatibilitySettingsValue.FullCompatibility;
            fullComp.SuppressValueEnhancedFormat = false;
            var model = TypeModel.Create(false, comp ? fullComp : ProtoCompatibilitySettingsValue.Incompatible);
            if (comp)
            {
                model.SkipForcedLateReference = true;
            }
            // otherwise will not be compatible
            model.SkipForcedAdvancedVersioning = true;
            if (aFirst)
            {
                model.Add(typeof(A_WithDefaultRef), true);
                model.Add(typeof(B_WithDefaultRef), true);
            }
            else
            {
                model.Add(typeof(B_WithDefaultRef), true);
                model.Add(typeof(A_WithDefaultRef), true);
            }
            
            ValueMember f = model[typeof(B_WithDefaultRef)][2];
            MemberLevelSettingsValue s = f.GetSettingsCopy(0);
            Assert.That(s.Format, Is.EqualTo(ValueFormat.NotSpecified));
            s.Format = ValueFormat.Compact;
            f.SetSettings(s);

            model.AutoCompile = false;

            return model;
        }
        static RuntimeTypeModel CreateFieldsModel(bool comp)
        {
            ProtoCompatibilitySettingsValue fullComp = ProtoCompatibilitySettingsValue.FullCompatibility;
            fullComp.SuppressValueEnhancedFormat = false;
            var model = TypeModel.Create(false, comp ? fullComp : ProtoCompatibilitySettingsValue.Incompatible);
            if (comp)
                model.SkipForcedLateReference = true;
            model.SkipForcedAdvancedVersioning = true;
            model.AutoCompile = false;
            var type = model.Add(typeof(KeyValuePair<int, A>), false);
            model.SkipCompiledVsNotCheck = true;
            type.Add(1, "key");
            type.AddField(2, "value").AsReference = true;

            model[typeof(B)][2].AsReference = false; // or just remove AsReference on Items
            return model;
        }
        static RuntimeTypeModel CreateSurrogateModel(bool comp)
        {
            ProtoCompatibilitySettingsValue fullComp = ProtoCompatibilitySettingsValue.FullCompatibility;
            fullComp.SuppressValueEnhancedFormat = false;
            var model = TypeModel.Create(false, comp ? fullComp : ProtoCompatibilitySettingsValue.Incompatible);
            model.SkipForcedAdvancedVersioning = true;
            if (comp)
                model.SkipForcedLateReference = true;
            model.AutoCompile = false;
            model[typeof(B)][2].AsReference = false; // or just remove AsReference on Items

            // this is the evil bit:
            model[typeof(KeyValuePair<int, A>)].SetSurrogate(typeof(RefPair<int, A>));
            model[typeof(KeyValuePair<int, A>)].AsReferenceDefault = true;
            return model;
        }

        [Test]
        public void ExecuteHackedViaSurrogate()
        {
            ExecuteAllModes(CreateSurrogateModel(true));
        }

        void ExecuteAllModes_WithDefaultRef(RuntimeTypeModel model, [CallerMemberName] string caller = null, bool standalone = false)
        {
            Execute_WithDefaultRef(model, "Runtime");
            Execute_WithDefaultRef(model, "CompileInPlace");
            if (standalone)
            {
                Execute_WithDefaultRef(model.Compile(), "Compile");
                model.Compile(caller, caller + ".dll");
                PEVerify.AssertValid(caller + ".dll");
            }
        }
        void ExecuteAllModes(RuntimeTypeModel model, [CallerMemberName] string caller = null, bool standalone = false)
        {
            Execute(model, "Runtime");
            Execute(model, "CompileInPlace");
            if (standalone)
            {
                Execute(model.Compile(), "Compile");
                model.Compile(caller, caller + ".dll");
                PEVerify.AssertValid(caller + ".dll");
            }
        }

        [Serializable]
        [ProtoBuf.ProtoContract]
        public class C
        {
            [ProtoBuf.ProtoMember(2, AsReference = true)]
            public List<Tuple<int, A>> Items { get; set; }

            public C()
            {
                Items = new List<Tuple<int, A>>();
            }
        }

        [Test]
        public void TuplesAsReference()
        {
            var obj = new C();
            var t = Tuple.Create(1, new A {});
            obj.Items.Add(t);
            obj.Items.Add(t);
            var clone = Serializer.DeepClone(obj);
            Assert.AreSame(clone.Items[0], clone.Items[1]);
        }

        [Ignore("AqlaSerializer is more tolerant to references")]
        [Test, ExpectedException(typeof(InvalidOperationException), ExpectedMessage = "AsReference cannot be used with value-types; please see http://stackoverflow.com/q/14436606/")]
        public void AreObjectReferencesSameAfterDeserialization()
        {
            var model = TypeModel.Create();
            model.AutoCompile = false;
            ExecuteAllModes(model);
        }

        static B CreateB()
        {
            A a = new A();
            B b = new B();

            b.A = a;

            b.Items.Add(1, a);
            return b;
        }
        private void Execute(TypeModel model, string caption)
        {
            var b = CreateB();

            Assert.AreSame(b.A, b.Items[1], caption + ":Original");

            B deserializedB = (B)model.DeepClone(b);

            Assert.AreSame(deserializedB.A, deserializedB.Items[1], caption + ":Clone");
        }
        static B_WithDefaultRef CreateB_WithDefaultRef()
        {
            A_WithDefaultRef a = new A_WithDefaultRef();
            B_WithDefaultRef b = new B_WithDefaultRef();

            b.A = a;

            b.Items.Add(1, a);
            return b;
        }
        private void Execute_WithDefaultRef(TypeModel model, string caption)
        {
            var b = CreateB_WithDefaultRef();

            Assert.AreSame(b.A, b.Items[1], caption + ":Original");

            B_WithDefaultRef deserializedB = (B_WithDefaultRef)model.DeepClone(b);

            Assert.AreSame(deserializedB.A, deserializedB.Items[1], caption + ":Clone");
        }
    }
}
