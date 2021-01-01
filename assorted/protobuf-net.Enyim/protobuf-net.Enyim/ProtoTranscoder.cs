﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using Enyim.Caching.Memcached;
using AqlaSerializer.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace AqlaSerializer.Caching.Enyim
{
    /// <summary>
    /// Acts as a transcoder compatible with the "enyim" client, swapping
    /// BinaryFormatter for protobuf-net's Serializer
    /// </summary>
    public sealed class NetTranscoder : ITranscoder
    {
        /// <summary>
        /// Create a new instance of NetTranscoder using the default model
        /// </summary>
        public NetTranscoder() : this(null) { }
        /// <summary>
        /// Create a new instance of NetTranscoder using the specified model (or the default model if null)
        /// </summary>
        public NetTranscoder(TypeModel model)
        {
            this.model = model ?? RuntimeTypeModel.Default;
        }
        private readonly TypeModel model;
        private readonly ITranscoder inner = new DefaultTranscoder();
        private readonly ReaderWriterLockSlim sync = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private readonly Dictionary<ArraySegment<byte>, Type> typeCache
            = new Dictionary<ArraySegment<byte>, Type>(new ByteSegmentComparer());
        const uint ProtoIdentifier = 0xfa57; // arbitrary, but avoiding "enyim"
        static readonly Encoding enc = new UTF8Encoding(false);

        private long serializedCount, deserializedCount;
        /// <summary>
        /// The number of protobuf serializations performed by the transcoder
        /// </summary>
        public long Serialized { get { return Interlocked.CompareExchange(ref serializedCount, 0, 0); } }
        /// <summary>
        /// The number of protobuf deserializations performed by the transcoder
        /// </summary>
        public long Deserialized { get { return Interlocked.CompareExchange(ref deserializedCount, 0, 0); } }

        Type ReadType(byte[] buffer, ref int offset, ref int count)
        {
            if (count < 4) throw new EndOfStreamException();
            int len = (int)buffer[offset++]
                    | (buffer[offset++] << 8)
                    | (buffer[offset++] << 16)
                    | (buffer[offset++] << 24);
            count -= 4;
            if (count < len) throw new EndOfStreamException();
            int keyOffset = offset;
            offset += len;
            count -= len;


            // lookup without any encoding overheads
            ArraySegment<byte> key = new ArraySegment<byte>(buffer, keyOffset, len);
            Type type;
            sync.EnterReadLock();
            try
            {
                if (typeCache.TryGetValue(key, out type))
                {
                    return type;
                }
            }
            finally
            {
                sync.ExitReadLock();
            }

            // resolve while we flip the lock, and create a new buffer to use
            // for the key (standalone)
            type = Type.GetType(enc.GetString(buffer, keyOffset, len), true);
            byte[] standaloneBuffer = new byte[len];
            Buffer.BlockCopy(buffer, keyOffset, standaloneBuffer, 0, len);
            key = new ArraySegment<byte>(standaloneBuffer, 0, len);

            sync.EnterWriteLock();
            try
            {
                // did somebody beat us to it?
                Type tmp;
                if (typeCache.TryGetValue(key, out tmp)) return tmp;
                typeCache.Add(key, type);
                return type;
            }
            finally
            {
                sync.ExitWriteLock();
            }
        }
        object ITranscoder.Deserialize(CacheItem item)
        {
            switch (item.Flags)
            {
                case ProtoIdentifier:
                    var segment = item.Data;
                    byte[] raw = segment.Array;
                    int count = segment.Count, offset = segment.Offset;
                    Type type = ReadType(raw, ref offset, ref count);
                    using (var ms = new MemoryStream(raw, offset, count))
                    {
                        Interlocked.Increment(ref deserializedCount);
                        return model.Deserialize(ms, null, type);
                    }
                default:
                    return inner.Deserialize(item);
            }
        }
        CacheItem ITranscoder.Serialize(object o)
        {
            if (o == null) return inner.Serialize(o);
            Type type = o.GetType();
            if (model.CanSerializeContractType(type))
            {
                using (var ms = new MemoryStream())
                {
                    WriteType(ms, type);
                    Interlocked.Increment(ref serializedCount);
                    model.Serialize(ms, o);
                    return new CacheItem(ProtoIdentifier, new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length));
                }
            }
            else
            {
                return inner.Serialize(o);
            }
        }

        private void WriteType(MemoryStream ms, Type type)
        {
            sync.EnterReadLock();
            try
            {
                foreach (var pair in typeCache)
                {
                    if (pair.Value == type)
                    {
                        var segment = pair.Key;
                        WriteSegment(ms, segment);
                        return;
                    }
                }
            }
            finally
            {
                sync.ExitReadLock();
            }

            string typeName = type.AssemblyQualifiedName;
            int i = typeName.IndexOf(','); // first split
            if (i >= 0) { i = typeName.IndexOf(',', i + 1); } // second split
            if (i >= 0) { typeName = typeName.Substring(0, i); } // extract type/assembly only
            byte[] buffer = enc.GetBytes(typeName);
            var key = new ArraySegment<byte>(buffer, 0, buffer.Length);

            sync.EnterWriteLock();
            try
            {
                typeCache[key] = type;
            }
            finally
            {
                sync.ExitWriteLock();
            }
            WriteSegment(ms, key);
        }

        private static void WriteSegment(MemoryStream dest, ArraySegment<byte> segment)
        {
            int len = segment.Count;
            dest.WriteByte((byte)len);
            dest.WriteByte((byte)(len >> 8));
            dest.WriteByte((byte)(len >> 16));
            dest.WriteByte((byte)(len >> 24));
            dest.Write(segment.Array, segment.Offset, segment.Count);
        }
    }
}