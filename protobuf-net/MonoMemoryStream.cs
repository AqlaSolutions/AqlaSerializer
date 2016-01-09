﻿//
// System.IO.MemoryStream.cs
//
// Authors:	Marcin Szczepanski (marcins@zipworld.com.au)
//		Patrik Torstensson
//		Gonzalo Paniagua Javier (gonzalo@ximian.com)
//
// (c) 2001,2002 Marcin Szczepanski, Patrik Torstensson
// (c) 2003 Ximian, Inc. (http://www.ximian.com)
// Copyright (C) 2004 Novell (http://www.novell.com)
//

//
// Copyright (C) 2004 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace AqlaSerializer
{
    /// <summary>
    /// This MemoryStream contains optimized for 32 bit growing strategy for minimal memory overhead
    /// </summary>
    class MonoMemoryStream : Stream
    {
        bool canWrite;
        bool allowGetBuffer;
        int capacity;
        int length;
        byte[] internalBuffer;
        int initialIndex;
        bool expandable;
        bool streamClosed;
        int position;
        int dirty_bytes;

        public MonoMemoryStream() : this(0)
        {
        }

        public MonoMemoryStream(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");

            canWrite = true;

            this.capacity = capacity;
            internalBuffer = new byte[capacity];

            expandable = true;
            allowGetBuffer = true;
        }

        public MonoMemoryStream(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            InternalConstructor(buffer, 0, buffer.Length, true, false);
        }

        public MonoMemoryStream(byte[] buffer, bool writable)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            InternalConstructor(buffer, 0, buffer.Length, writable, false);
        }

        public MonoMemoryStream(byte[] buffer, int index, int count)
        {
            InternalConstructor(buffer, index, count, true, false);
        }

        public MonoMemoryStream(byte[] buffer, int index, int count, bool writable)
        {
            InternalConstructor(buffer, index, count, writable, false);
        }

        public MonoMemoryStream(byte[] buffer, int index, int count, bool writable, bool publiclyVisible)
        {
            InternalConstructor(buffer, index, count, writable, publiclyVisible);
        }

        void InternalConstructor(byte[] buffer, int index, int count, bool writable, bool publicallyVisible)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (index < 0 || count < 0)
                throw new ArgumentOutOfRangeException("index or count is less than 0.");

            if (buffer.Length - index < count)
                throw new ArgumentException("index+count",
                                 "The size of the buffer is less than index + count.");

            canWrite = writable;

            internalBuffer = buffer;
            capacity = count + index;
            length = capacity;
            position = index;
            initialIndex = index;

            allowGetBuffer = publicallyVisible;
            expandable = false;
        }

        void CheckIfClosedThrowDisposed()
        {
            if (streamClosed)
                throw new ObjectDisposedException("MemoryStream");
        }

        public override bool CanRead
        {
            get { return !streamClosed; }
        }

        public override bool CanSeek
        {
            get { return !streamClosed; }
        }

        public override bool CanWrite
        {
            get { return (!streamClosed && canWrite); }
        }

        public virtual int Capacity
        {
            get
            {
                CheckIfClosedThrowDisposed();
                return capacity - initialIndex;
            }

            set
            {
                CheckIfClosedThrowDisposed();
                if (value == capacity)
                    return; // LAMENESS: see MemoryStreamTest.ConstructorFive

                if (!expandable)
                    throw new NotSupportedException("Cannot expand this MemoryStream");

                if (value < 0 || value < length)
                    throw new ArgumentOutOfRangeException("value",
                    "New capacity cannot be negative or less than the current capacity " + value + " " + capacity);

                if (value == internalBuffer.Length)
                    return;

                byte[] newBuffer = null;
                if (value != 0)
                {
                    newBuffer = new byte[value];
                    Buffer.BlockCopy(internalBuffer, 0, newBuffer, 0, length);
                }

                dirty_bytes = 0; // discard any dirty area beyond previous length
                internalBuffer = newBuffer; // It's null when capacity is set to 0
                capacity = value;
            }
        }

        public override long Length
        {
            get
            {
                // LAMESPEC: The spec says to throw an IOException if the
                // stream is closed and an ObjectDisposedException if
                // "methods were called after the stream was closed".  What
                // is the difference?

                CheckIfClosedThrowDisposed();

                // This is ok for MemoryStreamTest.ConstructorFive
                return length - initialIndex;
            }
        }

        public override long Position
        {
            get
            {
                CheckIfClosedThrowDisposed();
                return position - initialIndex;
            }

            set
            {
                CheckIfClosedThrowDisposed();
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value",
                                "Position cannot be negative");

                if (value > Int32.MaxValue)
                    throw new ArgumentOutOfRangeException("value",
                    "Position must be non-negative and less than 2^31 - 1 - origin");

                position = initialIndex + (int)value;
            }
        }

        protected override void Dispose(bool disposing)
        {
            streamClosed = true;
            expandable = false;
        }

        public override void Flush()
        {
            // Do nothing
        }

        public virtual byte[] GetBuffer()
        {
            if (!allowGetBuffer)
                throw new UnauthorizedAccessException();

            return internalBuffer;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckIfClosedThrowDisposed();

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException("offset or count less than zero.");

            if (buffer.Length - offset < count)
                throw new ArgumentException("offset+count",
                                  "The size of the buffer is less than offset + count.");

            if (position >= length || count == 0)
                return 0;

            if (position > length - count)
                count = length - position;

            Buffer.BlockCopy(internalBuffer, position, buffer, offset, count);
            position += count;
            return count;
        }

        public override int ReadByte()
        {
            CheckIfClosedThrowDisposed();
            if (position >= length)
                return -1;

            return internalBuffer[position++];
        }

        public override long Seek(long offset, SeekOrigin loc)
        {
            CheckIfClosedThrowDisposed();

            // It's funny that they don't throw this exception for < Int32.MinValue
            if (offset > (long)Int32.MaxValue)
                throw new ArgumentOutOfRangeException("Offset out of range. " + offset);

            int refPoint;
            switch (loc)
            {
                case SeekOrigin.Begin:
                    if (offset < 0)
                        throw new IOException("Attempted to seek before start of MemoryStream.");
                    refPoint = initialIndex;
                    break;
                case SeekOrigin.Current:
                    refPoint = position;
                    break;
                case SeekOrigin.End:
                    refPoint = length;
                    break;
                default:
                    throw new ArgumentException("loc", "Invalid SeekOrigin");
            }

            // LAMESPEC: My goodness, how may LAMESPECs are there in this
            // class! :)  In the spec for the Position property it's stated
            // "The position must not be more than one byte beyond the end of the stream."
            // In the spec for seek it says "Seeking to any location beyond the length of the 
            // stream is supported."  That's a contradiction i'd say.
            // I guess seek can go anywhere but if you use position it may get moved back.

            refPoint += (int)offset;
            if (refPoint < initialIndex)
                throw new IOException("Attempted to seek before start of MemoryStream.");

            position = refPoint;
            return position;
        }

        static readonly bool Is32Bit = IntPtr.Size <= 4;
        
        int CalculateNewCapacity(int minimum)
        {
            if (minimum < 256)
                minimum = 256; // See GetBufferTwo test


            // FIX for 32 bit
            int newSize = (Is32Bit && minimum > 1024 * 1024 * 50) ? (minimum + 1024 * 1024 * 5) : capacity * 2;

            if (minimum < newSize)
                minimum = newSize;

            return minimum;
        }

        void Expand(int newSize)
        {
            // We don't need to take into account the dirty bytes when incrementing the
            // Capacity, as changing it will only preserve the valid clear region.
            if (newSize > capacity)
                Capacity = CalculateNewCapacity(newSize);
            else if (dirty_bytes > 0)
            {
                Array.Clear(internalBuffer, length, dirty_bytes);
                dirty_bytes = 0;
            }
        }

        public override void SetLength(long value)
        {
            if (!expandable && value > capacity)
                throw new NotSupportedException("Expanding this MemoryStream is not supported");

            CheckIfClosedThrowDisposed();

            if (!canWrite)
            {
                throw new NotSupportedException("Cannot write to this MemoryStream");
            }

            // LAMESPEC: AGAIN! It says to throw this exception if value is
            // greater than "the maximum length of the MemoryStream".  I haven't
            // seen anywhere mention what the maximum length of a MemoryStream is and
            // since we're this far this memory stream is expandable.
            if (value < 0 || (value + initialIndex) > (long)Int32.MaxValue)
                throw new ArgumentOutOfRangeException();

            int newSize = (int)value + initialIndex;

            if (newSize > length)
                Expand(newSize);
            else if (newSize < length) // Postpone the call to Array.Clear till expand time
                dirty_bytes += length - newSize;

            length = newSize;
            if (position > length)
                position = length;
        }

        public virtual byte[] ToArray()
        {
            int l = length - initialIndex;
            byte[] outBuffer = new byte[l];

            if (internalBuffer != null)
                Buffer.BlockCopy(internalBuffer, initialIndex, outBuffer, 0, l);
            return outBuffer;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckIfClosedThrowDisposed();

            if (!canWrite)
                throw new NotSupportedException("Cannot write to this stream.");

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException();

            if (buffer.Length - offset < count)
                throw new ArgumentException("offset+count",
                                 "The size of the buffer is less than offset + count.");

            // reordered to avoid possible integer overflow
            if (position > length - count)
                Expand(position + count);

            Buffer.BlockCopy(buffer, offset, internalBuffer, position, count);
            position += count;
            if (position >= length)
                length = position;
        }

        public override void WriteByte(byte value)
        {
            CheckIfClosedThrowDisposed();
            if (!canWrite)
                throw new NotSupportedException("Cannot write to this stream.");

            if (position >= length)
            {
                Expand(position + 1);
                length = position + 1;
            }

            internalBuffer[position++] = value;
        }

        public virtual void WriteTo(Stream stream)
        {
            CheckIfClosedThrowDisposed();

            if (stream == null)
                throw new ArgumentNullException("stream");

            stream.Write(internalBuffer, initialIndex, length - initialIndex);
        }
    }
}