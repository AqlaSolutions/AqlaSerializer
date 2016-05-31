using System.IO;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class NoVersioningTest
    {
        [Test]
        public void SizeTest()
        {
            var tm = CreateModel();
            var normal = CreateModel(true);
            var foo = Foo.Create();
            using (var ms = new MemoryStream())
            {
                tm.Serialize(ms, foo);
                long sizeTm = ms.Length;
                ms.SetLength(0);
                normal.Serialize(ms, foo);
                long sizeNormal = ms.Length;
                Assert.That(sizeTm, Is.LessThanOrEqualTo(sizeNormal - 3 - 3 - 3)); // 3 Foo not ref fields, 3 Bar fields, 3 versioning footer
            }
        }

        [Test]
        public void CorrectnessTest()
        {
            var tm = CreateModel();
            var foo = Foo.Create();
            var clone = tm.DeepClone(foo);
            Assert.That(foo, Is.EqualTo(clone));
        }

        [Test]
        public void SchemaTest()
        {
            var tm = CreateModel();
            var s = tm.GetDebugSchema(typeof(Foo));
            Assert.That(s, Is.EqualTo(@"Root : Foo
 -> NetObject : Foo = AsReference, UseConstructor
 -> ModelType : Foo
 -> LinkTo [AqlaSerializer.unittest.Aqla.NoVersioningTest+Foo]

AqlaSerializer.unittest.Aqla.NoVersioningTest+Foo:
Type : Foo = [no-versioning]
{
    #3: [header-ignored] 
     -> Foo.A
     -> WireType : Int32 = Variant
     -> Int32
    ,
    #3: [header-ignored] 
     -> Foo.B
     -> WireType : Int32 = Variant
     -> Int32
    ,
    #3
     -> Foo.Bar
     -> NetObject : Bar = AsReference, UseConstructor, WithNullWireType
     -> ModelType : Bar
     -> LinkTo [AqlaSerializer.unittest.Aqla.NoVersioningTest+Bar]
    ,
    #3: [header-ignored] 
     -> Foo.C
     -> WireType : Int32 = Variant
     -> Int32
}


AqlaSerializer.unittest.Aqla.NoVersioningTest+Bar:
Type : Bar = [no-versioning]
{
    #3: [header-ignored] 
     -> Bar.A
     -> WireType : Int32 = Variant
     -> Int32
    ,
    #3: [header-ignored] 
     -> Bar.B
     -> WireType : Int32 = Variant
     -> Int32
    ,
    #3: [header-ignored] 
     -> Bar.C
     -> WireType : Int32 = Variant
     -> Int32
}


"));
        }

        static RuntimeTypeModel CreateModel(bool versioning = false)
        {
            var comp = ProtoCompatibilitySettingsValue.Default;
            comp.UseVersioning = versioning;
            var tm = TypeModel.Create(true, comp);
            return tm;
        }


        [SerializableType]
        public class Foo
        {
            public Bar Bar { get; set; }
            public int A { get; set; }
            public int B { get; set; }
            public int C { get; set; }

            public static Foo Create()
            {
                return new Foo() { A = 1, B = 2, C = 3, Bar = Bar.Create() };
            }

            protected bool Equals(Foo other)
            {
                return Equals(Bar, other.Bar) && A == other.A && B == other.B && C == other.C;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Foo)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (Bar != null ? Bar.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ A;
                    hashCode = (hashCode * 397) ^ B;
                    hashCode = (hashCode * 397) ^ C;
                    return hashCode;
                }
            }

            public static bool operator ==(Foo left, Foo right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(Foo left, Foo right)
            {
                return !Equals(left, right);
            }
        }

        [SerializableType]
        public class Bar
        {
            public int A { get; set; }
            public int B { get; set; }
            public int C { get; set; }

            protected bool Equals(Bar other)
            {
                return A == other.A && B == other.B && C == other.C;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Bar)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = A;
                    hashCode = (hashCode * 397) ^ B;
                    hashCode = (hashCode * 397) ^ C;
                    return hashCode;
                }
            }

            public static bool operator ==(Bar left, Bar right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(Bar left, Bar right)
            {
                return !Equals(left, right);
            }

            public static Bar Create()
            {
                return new Bar() { A = 1, B = 2, C = 3 };
            }
        }

    }
}