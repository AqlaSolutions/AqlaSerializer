﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if REMOTING
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using NUnit.Framework;
using AqlaSerializer;

#if NETCOREAPP
using BinaryFormatterException = System.Runtime.Serialization.SerializationException;
#else
using BinaryFormatterException = System.ArgumentNullException;
#endif

namespace Examples
{
    [ProtoBuf.ProtoContract, Serializable]
    public class RemotingEntity : ISerializable
    {
        public RemotingEntity()
        {}

        [ProtoBuf.ProtoMember(1)]
        public int Value { get; set; }

        public bool WasSerialized { get; private set; }
        public bool WasDeserialized { get; private set; }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            Serializer.Serialize(info, context, this);
            WasSerialized = true;
        }
        protected RemotingEntity(SerializationInfo info, StreamingContext context)
        {
            Serializer.Merge(info, context, this);
            WasDeserialized = true;
        }
    }

    [ProtoBuf.ProtoContract, Serializable]
    public class BrokenSerEntity : ISerializable
    {
        public BrokenSerEntity()
        { }

        [ProtoBuf.ProtoMember(1)]
        public int Value { get; set; }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            Serializer.Serialize<BrokenSerEntity>(info, null);
        }
        protected BrokenSerEntity(SerializationInfo info, StreamingContext context)
        {
            Serializer.Merge<BrokenSerEntity>(info, this);
        }
    }
    [ProtoBuf.ProtoContract, Serializable]
    public class BrokenDeserEntity : ISerializable
    {
        public BrokenDeserEntity()
        { }

        [ProtoBuf.ProtoMember(1)]
        public int Value { get; set; }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            Serializer.Serialize<BrokenDeserEntity>(info, this);
        }
        protected BrokenDeserEntity(SerializationInfo info, StreamingContext context)
        {
            Serializer.Merge<BrokenDeserEntity>(info, null);
        }
    }
    [TestFixture]
    public class RemotingTests
    {
        [Test]
        public void TestClone()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                RemotingEntity obj = new RemotingEntity {Value = 12345};
                Assert.IsFalse(obj.WasDeserialized);
                Assert.IsFalse(obj.WasSerialized);
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(ms, obj);
                Assert.IsTrue(obj.WasSerialized);
                ms.Position = 0;
                RemotingEntity clone = (RemotingEntity) bf.Deserialize(ms);
                Assert.IsFalse(clone.WasSerialized);
                Assert.IsTrue(clone.WasDeserialized);
                Assert.AreEqual(obj.Value, clone.Value);
                
            }
        }
        [Test]
        public void TestSerNullItem()
        {
            Assert.Throws<ArgumentNullException>(() => {
                BrokenSerEntity obj = new BrokenSerEntity { Value = 12345 };
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, obj);
                }
            });
        }
        [Test]
        public void TestSerNullContext()
        {
            Assert.Throws<ArgumentNullException>(() => {
                RemotingEntity obj = new RemotingEntity { Value = 12345 };
                Serializer.Serialize((SerializationInfo)null, obj);

            });
        }
        [Test]
        public void TestDeSerNullItem()
        {
            Assert.Throws<BinaryFormatterException>(() => {

                BrokenDeserEntity obj = new BrokenDeserEntity { Value = 12345 };
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, obj);
                    ms.Position = 0;
                    try
                    {
                        BrokenDeserEntity clone = (BrokenDeserEntity)bf.Deserialize(ms);
                    }
                    catch (TargetInvocationException ex)
                    {
                        if (ex.InnerException == null) throw;
                        throw ex.InnerException;
                    }
                }
            });
        }
        [Test]
        public void TestDeSerNullContext()
        {
            Assert.Throws<ArgumentNullException>(() => {
                RemotingEntity obj = new RemotingEntity { Value = 12345 };
                Serializer.Merge((SerializationInfo)null, obj);
            });
        }
    }
}
#endif