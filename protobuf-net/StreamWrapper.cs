//#define DEBUG_WRITING

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace AqlaSerializer
{
    internal class StreamWrapper
    {
        readonly Stream _stream;
        readonly MemoryStream _streamAsMs;
        long _lastFlushPosition;
        readonly Stream _nonSeekingStream;
        long _startOffset;
        readonly bool _autoSize;

        public long CurPosition
        {
            get { return (int)(_stream.Position - _startOffset); }
            set
            {

                long position = value + _startOffset;
                if (_stream.Length < position && _autoSize)
                {
                    _stream.SetLength(position);
                    SetBytesUsed(position);
                }
                _stream.Position = position;
            }
        }

        public long BytesUsed { get; private set; }

        void SetBytesUsed(long position)
        {
            if (BytesUsed < position)
                BytesUsed = position;
        }


        public StreamWrapper(Stream stream, bool isForWriting)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanSeek || !stream.CanRead)
            {
                if (!isForWriting)
                    throw new InvalidOperationException("Deserializing streams should support both Read and Seek operations");
                _nonSeekingStream = stream;
                stream = _streamAsMs = new MemoryStream();
            }
            _stream = stream;
            _autoSize = isForWriting;
            _startOffset = stream.Position;
        }

        [Conditional("DEBUG_WRITING")]
        void DebugWriting(long position)
        {
            if (position >= 4 && position <= 7)
            {

            }
        }

        public byte this[long position]
        {
            get
            {
                var p = CurPosition;
                try
                {
                    CurPosition = position;
                    return (byte)_stream.ReadByte();
                }
                finally
                {
                    CurPosition = p;
                }
            }
            set
            {
                var p = CurPosition;
                try
                {
                    CurPosition = position;
                    DebugWriting(position);
                    _stream.WriteByte(value);
                    SetBytesUsed(position + 1);
                }
                finally
                {
                    CurPosition = p;
                }
            }
        }

        public byte PreviousByte { get { return this[CurPosition - 1]; } set { this[CurPosition - 1] = value; } }

        public void GetBuffer(long streamPosition, byte[] dest, int destOffset, int count)
        {
            var p = CurPosition;
            try
            {
                CurPosition = streamPosition;
                _stream.Read(dest, destOffset, count);
            }
            finally
            {
                CurPosition = p;
            }
        }

        public void PutBuffer(long streamPosition, byte[] source, int sourceOffset, int count)
        {
            var p = CurPosition;
            try
            {
                CurPosition = streamPosition;
                DebugWriting(streamPosition);
                _stream.Write(source, sourceOffset, count);
                SetBytesUsed(streamPosition + count);
            }
            finally
            {
                CurPosition = p;
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var r = _stream.Read(buffer, offset, count);
            SetBytesUsed(CurPosition);
            return r;
        }

        public byte ReadByte()
        {
            int b = _stream.ReadByte();
            if (b == -1) throw new EndOfStreamException();
            return (byte)b;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            DebugWriting(CurPosition);
            _stream.Write(buffer, offset, count);
            SetBytesUsed(CurPosition);
        }

        public void WriteByte(byte value)
        {
            DebugWriting(CurPosition);
            _stream.WriteByte(value);
            SetBytesUsed(CurPosition);
        }

        public void Flush(bool onlyWhenSize)
        {
            long count = BytesUsed - _lastFlushPosition;
            if (onlyWhenSize && count < BufferPool.BufferLength / 2) return;
            _stream.Flush();
            if (_nonSeekingStream != null)
            {
                var p = CurPosition;
                CurPosition = _lastFlushPosition;
                bool pooling = count <= BufferPool.BufferLength;
                byte[] buffer = pooling ? BufferPool.GetBuffer() : new byte[1024 * 250];
                int read;
                while ((read = _stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    count -= read;

                    if (count <= 0)
                    {
                        _nonSeekingStream.Write(buffer, 0, read + (int)count);
                        break;
                    }

                    _nonSeekingStream.Write(buffer, 0, read);
                }
                if (pooling)
                    BufferPool.ReleaseBufferToPool(ref buffer);
                _nonSeekingStream.Flush();
                CurPosition = p;

                // CurPosition should not change while we truncate the MemoryStream
                _startOffset -= _streamAsMs.Length;
                _streamAsMs.SetLength(0);
            }
            _lastFlushPosition = CurPosition;
        }
    }
}