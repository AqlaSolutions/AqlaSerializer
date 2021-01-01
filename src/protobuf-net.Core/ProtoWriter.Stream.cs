using ProtoBuf.Internal;
using AqlaSerializer.Meta;
using System;
using System.Diagnostics;
using System.IO;
using System.Buffers;
using System.Runtime.InteropServices;

namespace AqlaSerializer
{
    public partial class ProtoWriter
    {
        /// <summary>
        /// Creates a new writer against a stream
        /// </summary>
        /// <param name="dest">The destination stream</param>
        /// <param name="model">The model to use for serialization; this can be null, but this will impair the ability to serialize sub-objects</param>
        /// <param name="context">Additional context about this serialization operation</param>
        [Obsolete(ProtoReader.PreferStateAPI, false)]
        public static ProtoWriter Create(Stream dest, TypeModel model, SerializationContext context = null)
            => StreamProtoWriter.CreateStreamProtoWriter(dest, model, context);

        partial struct State
        {
            /// <summary>
            /// Creates a new writer against a stream
            /// </summary>
            /// <param name="dest">The destination stream</param>
            /// <param name="model">The model to use for serialization; this can be null, but this will impair the ability to serialize sub-objects</param>
            /// <param name="userState">Additional context about this serialization operation</param>
            public static State Create(Stream dest, TypeModel model, object userState = null)
            {
                var writer = StreamProtoWriter.CreateStreamProtoWriter(dest, model, userState);
                return new State(writer);
            }
        }
        private class StreamProtoWriter : ProtoWriter
        {
            protected internal override State DefaultState() => new State(this);

            private Stream _dest;
            private int flushLock;

            private protected override bool ImplDemandFlushOnDispose => true;
            internal long InitialUnderlyingStreamPosition { get; }

            public bool AllowStreamRewriting { get; }

            internal StreamProtoWriter(Stream dest, TypeModel model, SerializationContext context)
                : base(model, context)
            {
                if (dest == null) throw new ArgumentNullException(nameof(dest));
                if (!dest.CanWrite) throw new ArgumentException("Cannot write to stream", nameof(dest));
                //if (model == null) throw new ArgumentNullException("model");
                this._dest = dest;
                _ioBuffer = BufferPool.GetBuffer();
                if ((model != null && model.AllowStreamRewriting) && dest.CanSeek && dest.CanRead)
                    AllowStreamRewriting = true;
                InitialUnderlyingStreamPosition = dest.Position;
            }
            internal static StreamProtoWriter CreateStreamProtoWriter(Stream dest, TypeModel model, object userState)
            {
                var obj = Pool<StreamProtoWriter>.TryGet() ?? new StreamProtoWriter();
                obj.Init(model, userState, true);
                if (dest is null) ThrowHelper.ThrowArgumentNullException(nameof(dest));
                if (!dest.CanWrite) ThrowHelper.ThrowArgumentException("Cannot write to stream", nameof(dest));
                //if (model is null) ThrowHelper.ThrowArgumentNullException("model");
                obj.dest = dest;
                obj.ioBuffer = BufferPool.GetBuffer();
                return obj;
            }
            private protected override void Cleanup()
            {
                base.Dispose();
                // importantly, this does **not** own the stream, and does not dispose it
                _dest = null;
                BufferPool.ReleaseBufferToPool(ref _ioBuffer);
                if (_tempBuffer != null)
                    BufferPool.ReleaseBufferToPool(ref _tempBuffer);
            }

            internal override void Init(TypeModel model, object userState, bool impactCount)
            {
                base.Init(model, userState, impactCount);
                ioIndex = 0;
                flushLock = 0;
            }

            internal override void Dispose()
            {
                base.Dispose();
                Pool<StreamProtoWriter>.Put(this);
            }

            private static void IncrementedAndReset(int length, StreamProtoWriter writer)
            {
                Debug.Assert(length >= 0);
                writer._ioIndex += length;
                writer.Advance(length);
                writer.WireType = WireType.None;
            }

            private protected override bool TryFlush(ref State state)
            {
                if ((this.AllowStreamRewriting || this._flushLock == 0) && this._ioIndex != 0)
                {
                    this._dest.Write(this._ioBuffer, 0, this._ioIndex);
                    this._ioIndex = 0;
                    return true;
                }

                return false;
            }

            bool IsFlushAdvised(int whenSize)
            {
                const int ADVISORY_FLUSH_SIZE = 1024;
                const int STREAM_AS_BUFFER_ADVISORY_FLUSH_SIZE = 1024 * 1024 * 10; // do not backread stream unless necessary
                bool streamRead = AllowStreamRewriting;

                return !streamRead
                    ? _flushLock == 0 && whenSize >= ADVISORY_FLUSH_SIZE
                    : whenSize >= STREAM_AS_BUFFER_ADVISORY_FLUSH_SIZE;
            }

            bool IsFlushAdvised()
            {
                return IsFlushAdvised(_ioIndex);
            }

            /// <summary>
            /// Works only if position is in buffer and not flushed yet
            /// </summary>
            /// <param name="position"></param>
            /// <param name="state"></param>
            /// <returns></returns>
            protected internal override bool TrySeek(long position, ref ProtoWriter.State state)
            {
                var flushed = _position64 - _ioIndex;
                if (flushed > 0) position -= flushed;
                if (position < 0 || position > _ioBuffer.Length)
                    return false;
                var diff = position - _ioIndex;
                _ioIndex = (int)position;
                _position64 += diff;
                return true;
            }

            
            private static void DemandSpace(int required, StreamProtoWriter writer, ref State state)
            {
                // check for enough space
                if ((writer._ioBuffer.Length - writer._ioIndex) < required)
                {
                    TryFlushOrResize(required, writer, ref state);
                }
            }

            private static void TryFlushOrResize(int required, StreamProtoWriter writer, ref State state)
            {
                if (writer.IsFlushAdvised(writer._ioIndex + required))
                {
                    writer.Flush(ref state); // try emptying the buffer
                    if ((writer._ioBuffer.Length - writer._ioIndex) >= required) return;
                }
                // either can't empty the buffer, or that didn't help; need more space
                BufferPool.ResizeAndFlushLeft(ref writer._ioBuffer, required + writer._ioIndex, 0, writer._ioIndex);
            }

            private byte[] _ioBuffer;
            private int _ioIndex;

            private protected override void ImplWriteBytes(ref State state, byte[] data, int offset, int length)
            {
                if (flushLock != 0 || length <= _ioBuffer.Length) // write to the buffer
                {
                    DemandSpace(length, this, ref state);
                    Buffer.BlockCopy(data, offset, _ioBuffer, _ioIndex, length);
                    _ioIndex += length;
                }
                else
                {
                    // writing data that is bigger than the buffer (and the buffer
                    // isn't currently locked due to a sub-object needing the size backfilled)
                    Flush(ref state); // commit any existing data from the buffer
                                      // now just write directly to the underlying stream
                    _dest.Write(data, offset, length);
                    // since we've flushed offset etc is 0, and remains
                    // zero since we're writing directly to the stream
                }
            }

#if !PLAT_SPAN_OVERLOADS
            static void WriteFallback(ReadOnlySpan<byte> bytes, Stream stream)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(2048);
                try
                {
                    var target = new Span<byte>(buffer);
                    var capacity = target.Length;
                    // add all the chunks of (buffer size)
                    while (bytes.Length >= capacity)
                    {
                        bytes.Slice(0, capacity).CopyTo(target);
                        stream.Write(buffer, 0, capacity);
                        bytes = bytes.Slice(start: capacity);
                    }
                    // and anything that is left
                    bytes.CopyTo(target);
                    stream.Write(buffer, 0, bytes.Length);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
#endif
            private protected override void ImplWriteBytes(ref State state, System.Buffers.ReadOnlySequence<byte> data)
            {
                int length = checked((int)data.Length);
                if (length == 0) return;
                if (flushLock != 0 || length <= _ioBuffer.Length) // write to the buffer
                {
                    DemandSpace(length, this, ref state);
                    System.Buffers.BuffersExtensions.CopyTo(data, new Span<byte>(_ioBuffer, _ioIndex, length));
                    _ioIndex += length;
                }
                else
                {
                    // writing data that is bigger than the buffer (and the buffer
                    // isn't currently locked due to a sub-object needing the size backfilled)
                    state.Flush(); // commit any existing data from the buffer
                                      // now just write directly to the underlying stream
                    foreach(var chunk in data)
                    {
#if PLAT_SPAN_OVERLOADS
                        dest.Write(chunk.Span);
#else
                        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(chunk, out var segment))
                        {
                            _dest.Write(segment.Array, segment.Offset, segment.Count);
                        }
                        else
                        {
                            var arr = System.Buffers.ArrayPool<byte>.Shared.Rent(chunk.Length);
                            try
                            {
                                chunk.CopyTo(arr);
                                _dest.Write(arr, 0, chunk.Length);
                            }
                            finally
                            {
                                System.Buffers.ArrayPool<byte>.Shared.Return(arr);
                            }
                        }
#endif
                    }

                    // since we've flushed offset etc is 0, and remains
                    // zero since we're writing directly to the stream
                }
            }

            private protected override void ImplWriteString(ref State state, string value, int expectedBytes)
            {
                DemandSpace(expectedBytes, this, ref state);
                int actualBytes = UTF8.GetBytes(value, 0, value.Length, _ioBuffer, _ioIndex);
                _ioIndex += actualBytes;
                Debug.Assert(expectedBytes == actualBytes);
            }

            private static void WriteUInt32ToBuffer(uint value, byte[] buffer, int index)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(index, 4), value);
            }

            private protected override void ImplWriteFixed32(ref State state, uint value)
            {
                DemandSpace(4, this, ref state);
                WriteUInt32ToBuffer(value, _ioBuffer, _ioIndex);
                _ioIndex += 4;
            }
            private protected override void ImplWriteFixed64(ref State state, ulong value)
            {
                DemandSpace(8, this, ref state);
                var buffer = _ioBuffer;
                var index = _ioIndex;

                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(index, 8), value);
                _ioIndex += 8;
            }

            internal override int ImplWriteVarint64(ref State state, ulong value)
            {
                DemandSpace(10, this, ref state);
                int count = 0;
                do
                {
                    _ioBuffer[_ioIndex++] = (byte)((value & 0x7F) | 0x80);
                    count++;
                } while ((value >>= 7) != 0);
                _ioBuffer[_ioIndex - 1] &= 0x7F;
                return count;
            }

            private protected override int ImplWriteVarint32(ref State state, uint value)
            {
                DemandSpace(5, this, ref state);
                int count = 0;
                do
                {
                    _ioBuffer[_ioIndex++] = (byte)((value & 0x7F) | 0x80);
                    count++;
                } while ((value >>= 7) != 0);
                _ioBuffer[_ioIndex - 1] &= 0x7F;
                return count;
            }

            private protected override void ImplCopyRawFromStream(ref State state, Stream source)
            {
                byte[] buffer = _ioBuffer;
                int space = buffer.Length - _ioIndex, bytesRead = 1; // 1 here to spoof case where already full

                // try filling the buffer first   
                while (space > 0 && (bytesRead = source.Read(buffer, _ioIndex, space)) > 0)
                {
                    _ioIndex += bytesRead;
                    Advance(bytesRead);
                    space -= bytesRead;
                }
                if (bytesRead <= 0) return; // all done using just the buffer; stream exhausted

                // at this point the stream still has data, but buffer is full; 
                if (flushLock == 0)
                {
                    // flush the buffer and write to the underlying stream instead
                    state.Flush();
                    while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        _dest.Write(buffer, 0, bytesRead);
                        Advance(bytesRead);
                    }
                }
                else
                {
                    while (true)
                    {
                        // need more space; resize (double) as necessary,
                        // requesting a reasonable minimum chunk each time
                        // (128 is the minimum; there may actually be much
                        // more space than this in the buffer)
                        DemandSpace(128, this, ref state);
                        if ((bytesRead = source.Read(_ioBuffer, _ioIndex,
                            _ioBuffer.Length - _ioIndex)) <= 0)
                        {
                            break;
                        }
                        Advance(bytesRead);
                        _ioIndex += bytesRead;
                    }
                }
            }
            private protected override SubItemToken ImplStartLengthPrefixedSubItem(ref State state, object instance, PrefixStyle style)
            {
                switch (WireType)
                {
                    case WireType.String:
                        WireType = WireType.None;
                        DemandSpace(32, this, ref state); // make some space in anticipation...
                        flushLock++;
                        Advance(1);
                        return new SubItemToken((long)(_ioIndex++)); // leave 1 space (optimistic) for length
                    case WireType.Fixed32:
                        DemandSpace(32, this, ref state); // make some space in anticipation...
                        flushLock++;
                        SubItemToken token = new SubItemToken((long)_ioIndex);
                        IncrementedAndReset(4, this); // leave 4 space (rigid) for length
                        return token;
                    default:
                        state.ThrowInvalidSerializationOperation();
                        return default;
                }
            }

            private protected override void ImplEndLengthPrefixedSubItem(ref State state, SubItemToken token, PrefixStyle style)
            {
                byte[] buffer;
                var value = token.Value64;

                long flushed = this._position64 - this._ioIndex;
                Debug.Assert(flushed >= 0, "Position should be always bigger or equal to ioIndex (it's total written length including flushed)");

                long inBufferPos = value - flushed;

                // so we're backfilling the length into an existing sequence
                // should operate on buffer?
                if (inBufferPos >= 0)
                {
                    int positionDiff = checked((int)(this._position64 - value));
                    int len;
                    switch (style)
                    {
                        case PrefixStyle.Fixed32:
                            len = (int)(positionDiff - 4);
                            WriteUInt32ToBuffer((uint)len, _ioBuffer, (int)inBufferPos);
                            break;
                        case PrefixStyle.Fixed32BigEndian:
                            len = (int)(positionDiff - 4);
                            buffer = _ioBuffer;
                            WriteUInt32ToBuffer((uint)len, buffer, (int)value);
                            // and swap the byte order
                            byte b = buffer[value];
                            buffer[value] = buffer[value + 3];
                            buffer[value + 3] = b;
                            b = buffer[value + 1];
                            buffer[value + 1] = buffer[value + 2];
                            buffer[value + 2] = b;
                            break;
                        case PrefixStyle.Base128:
                            // string - complicated because we only reserved one byte;
                            // if the prefix turns out to need more than this then
                            // we need to shuffle the existing data
                            len = (int)(positionDiff - 1);
                            int offset = 0;
                            uint tmp = (uint)len;
                            while ((tmp >>= 7) != 0) offset++;
                            if (offset == 0)
                            {
                                _ioBuffer[value] = (byte)(len & 0x7F);
                            }
                            else
                            {
                                DemandSpace(offset, this, ref state);
                                byte[] blob = _ioBuffer;
                                Buffer.BlockCopy(blob, (int)(inBufferPos + 1), blob, (int)(inBufferPos + 1 + offset), len);
                                tmp = (uint)len;
                                do
                                {
                                    blob[value++] = (byte)((tmp & 0x7F) | 0x80);
                                } while ((tmp >>= 7) != 0);
                                blob[value - 1] = (byte)(blob[value - 1] & ~0x80);
                                Advance(offset);
                                _ioIndex += offset;
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(style));
                    }
                }
                else // do the same thing but for stream
                {
                    long positionDiff = this._position64 - value;

                    byte[] temp = null;
                    int writeFromTemp = 0;
                    var prevPos = _dest.Position;

                    switch (style)
                    {
                        case PrefixStyle.Fixed32:
                            {
                                writeFromTemp = 4;
                                int len = checked((int)(positionDiff - writeFromTemp));
                                temp = this.GetTempBuffer(writeFromTemp);
                                WriteUInt32ToBuffer((uint)len, temp, 0);
                            }
                            break;
                        case PrefixStyle.Fixed32BigEndian:
                            {
                                writeFromTemp = 4;
                                int len = checked((int)((int)(positionDiff - writeFromTemp)));
                                temp = this.GetTempBuffer(writeFromTemp);
                                WriteUInt32ToBuffer((uint)len, temp, 0);
                                // and swap the byte order
                                byte b = temp[0];
                                temp[0] = temp[0 + 3];
                                temp[0 + 3] = b;
                                b = temp[0 + 1];
                                temp[0 + 1] = temp[0 + 2];
                                temp[0 + 2] = b;
                            }
                            break;
                        case PrefixStyle.Base128:
                            {
                                // string - complicated because we only reserved one byte;
                                // if the prefix turns out to need more than this then
                                // we need to shuffle the existing data

                                // hack: len up to long
                                long len = positionDiff - 1;
                                int offset = 0;
                                ulong tmp = (ulong)len;
                                while ((tmp >>= 7) != 0) offset++;
                                if (offset == 0)
                                {
                                    _dest.Position += inBufferPos; // negative
                                    _dest.WriteByte((byte)(len & 0x7F));
                                }
                                else
                                {
                                    DemandSpace(offset, this, ref state);
                                    buffer = this._ioBuffer;
                                    if (inBufferPos == -1)
                                        Buffer.BlockCopy(buffer, 0, buffer, offset, checked((int)len)); // must be ok if pos is just -1
                                    else
                                    {
                                        // move data from inBufferPos + 1 to inBufferPos + 1 + offset
                                        long left = len;
                                        long blockInBufferPos = inBufferPos + 1; // negative
                                                                                 // first - in memory
                                        long inMemoryWas = len + blockInBufferPos;
                                        if (inMemoryWas > 0)
                                        {
                                            Buffer.BlockCopy(buffer, 0, buffer, offset, (int)inMemoryWas); // in memory should not be more than int.MaxValue
                                            left -= inMemoryWas;
                                        }

                                        int inMemoryDataStart = offset;

                                        // second - from stream to stream (and memory)
                                        int maxBlockSize = (int)Math.Min(left, 1024 * 1024);
                                        temp = this.GetTempBuffer(maxBlockSize);
                                        long streamPosition = _dest.Position;
                                        long streamEndPosition = streamPosition;
                                        while (left > 0)
                                        {
                                            int thisBlockSize = (int)Math.Min(left, maxBlockSize);
                                            streamPosition -= thisBlockSize;
                                            _dest.Position = streamPosition;
                                            int actual = _dest.Read(temp, 0, thisBlockSize);
                                            Debug.Assert(thisBlockSize == actual);
                                            int overhead = Math.Min(inMemoryDataStart, thisBlockSize);
                                            if (overhead > 0)
                                            {
                                                // we have more bytes than we can put to the stream
                                                // so they go to memory

                                                Buffer.BlockCopy(temp, thisBlockSize - overhead, buffer, inMemoryDataStart - overhead, overhead);

                                                inMemoryDataStart -= overhead;
                                                thisBlockSize -= overhead;
                                                left -= overhead;
                                            }

                                            if (thisBlockSize > 0)
                                            {
                                                _dest.Position = streamPosition + offset;
                                                Debug.Assert(_dest.Position + thisBlockSize <= streamEndPosition);
                                                _dest.Write(temp, 0, thisBlockSize);
                                            }

                                            left -= thisBlockSize;
                                        }
                                        _dest.Position = streamEndPosition;
                                    }

                                    // from inBufferPos
                                    temp = this.GetTempBuffer(10);
                                    int count = 0;
                                    tmp = (ulong)len;
                                    do
                                    {
                                        temp[count++] = (byte)((tmp & 0x7F) | 0x80);
                                    } while ((tmp >>= 7) != 0);
                                    temp[count - 1] = (byte)(temp[count - 1] & ~0x80);

                                    writeFromTemp = count;

                                    this._position64 += offset;
                                    this._ioIndex += offset;

                                }
                            }
                            break;
                        default:
                            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(style));
                            break;
                    }

                    if (writeFromTemp != 0)
                    {
                        Debug.Assert(temp != null, "temp != null");

                        int lengthInStream = (int)Math.Min(-inBufferPos, writeFromTemp);
                        _dest.Position += inBufferPos;
                        _dest.Write(temp, 0, lengthInStream);

                        if (lengthInStream != writeFromTemp)
                            Buffer.BlockCopy(temp, lengthInStream, _ioBuffer, 0, writeFromTemp - lengthInStream);
                    }

                    _dest.Position = prevPos;
                }

                // and this object is no longer a blockage - also flush if sensible
                this._flushLock--;
                if (this.IsFlushAdvised())
                {
                    state.Flush();
                }
            }

            byte[] _tempBuffer = BufferPool.GetBuffer();

            byte[] GetTempBuffer(int requiredMinSize)
            {
                if (_tempBuffer.Length < requiredMinSize)
                    BufferPool.ResizeAndFlushLeft(ref _tempBuffer, requiredMinSize, 0, 0);
                return _tempBuffer;
            }

        }
    }
}