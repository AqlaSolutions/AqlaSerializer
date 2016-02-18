using System.Collections.Generic;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace AqlaSerializer.unittest.Aqla
{
    [TestFixture]
    public class Issue91Converter
    {
        [SerializableType]
        public class STag
        {
            [SerializableMember(1)]
            public int Type;

            //I apply AsReference decoration in run-time while configuring the model.
            //Don't think the moment really matters.
            [SerializableMember(2, MemberFormat.Enhanced, EnhancedMode.Reference)]
            public object Value;
        }

        [SerializableType]
        public struct SObjectSurrogate
        {
            [SerializableMember(1)]
            public int DataType;

            [SerializableMember(2)]
            public byte[] Data;

            [SerializableMember(3)]
            public string EnumType;

            [SurrogateConverter]
            public static SObjectSurrogate SerializeTagValue(object iTagValue)
            {
                return new SObjectSurrogate()
                {
                    Data = new byte[1],
                    DataType = 12345,
                    EnumType = "abcd"
                };
            }

            [SurrogateConverter]
            public static object DeserializeTagValue(SObjectSurrogate iSerializedTagValue)
            {
                return new LegacyObject(1);
            }
        }

        public class LegacyObject
        {
            public LegacyObject(int data)
            {

            }
        }

        // TODO late reference is not supported for surrogate subtypes
        [Test]
        public void Execute([Values(false, true)] bool compile)
        {
            var comp = ProtoCompatibilitySettings.Default;
            comp.AllowExtensionDefinitions &= ~NetObjectExtensionTypes.LateReference;
            var model = TypeModel.Create(false, comp);
            model.AutoCompile = compile;
            model.Add(typeof(SObjectSurrogate), true);
            model.Add(typeof(STag), true);
            model.Add(typeof(object), false).SetSurrogate(typeof(SObjectSurrogate));
            var obj = new STag() { Type = 123, Value = new LegacyObject(1) };

            var clone = model.DeepClone(obj);
            Assert.AreEqual(obj.Type, clone.Type);
            Assert.IsNotNull(obj.Value);
        }
    }
}