// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.Runtime.Serialization;
using NUnit.Framework;
using AqlaSerializer;


namespace Examples.Issues
{
    [TestFixture]
    public class Issue27
    {
        [Test]
        public void Roundtrip()
        {
            KeyPair<int, string> pair = new KeyPair<int, string>(1, "abc");

            KeyPair<int,string> clone = Serializer.DeepClone<KeyPairProxy<int,string>>(pair);
            Assert.AreEqual(pair.Key1, clone.Key1);
            Assert.AreEqual(pair.Key2, clone.Key2);
        }

        [Test]
        public void RoundtripStruct()
        {
            KeyPair<int, string> pair = new KeyPair<int, string>(1, "abc");

            KeyPair<int,string> clone = Serializer.DeepClone<KeyPair<int,string>>(pair);
            Assert.AreEqual(pair.Key1, clone.Key1);
            Assert.AreEqual(pair.Key2, clone.Key2);
        }

        [Test]
        public void TestWrapped()
        {
            Foo foo = new Foo { Pair = new KeyPair<int, string>(1, "abc") };
            Assert.AreEqual(1, foo.Pair.Key1, "Key1 - orig");
            Assert.AreEqual("abc", foo.Pair.Key2, "Key2 - orig");
            var clone = Serializer.DeepClone(foo);
            Assert.AreEqual(1, clone.Pair.Key1, "Key1 - clone");
            Assert.AreEqual("abc", clone.Pair.Key2, "Key2 - clone");
        }

    }
    [DataContract]
    public class Foo
    {
        [DataMember(Name = "Pair", Order = 1)]
        public KeyPair<int, string> Pair { get; set; }

        // AqlaSerializer: don't do it!
        //[DataMember(Name = "Pair", Order = 1)]
        //private KeyPairProxy<int, string> PairProxy
        //{
        //    get { return Pair; }
        //    set { Pair = value; }
        //}
        // 1. deprecated, structs should work
        // 2. as reference (default for DataMember) will read property value and deserialize on it
        // 3. late ref will assign instance to property first and only then deserialize on existing ref
    }

    [DataContract]
    sealed public class KeyPairProxy<TKey1, TKey2>
    {
        [DataMember(Order = 1)]
        public TKey1 Key1 { get; set; }
        [DataMember(Order = 2)]
        public TKey2 Key2 { get; set; }

        public static implicit operator KeyPair<TKey1, TKey2> (KeyPairProxy<TKey1, TKey2> pair)
        {
            return new KeyPair<TKey1, TKey2>(pair.Key1, pair.Key2);
        }
        public static implicit operator KeyPairProxy<TKey1, TKey2>(KeyPair<TKey1, TKey2> pair)
        {
            return new KeyPairProxy<TKey1, TKey2> { Key1 = pair.Key1, Key2 = pair.Key2 };
        }
    }
   [DataContract(Namespace = "foo")]
   public struct KeyPair<TKey1, TKey2>
   {
       public KeyPair(TKey1 k1, TKey2 k2)
           : this() {
           Key1 = k1;
           Key2 = k2;
       }
       // Stupid tuple public class for datacontract
       [DataMember(Order = 1)]
       public TKey1 Key1 { get;  set; }
       [DataMember(Order = 2)]
       public TKey2 Key2 { get;  set; }

       public override string ToString() {
           return Key1.ToString() + ", " + Key2.ToString();
       }
   }

}

