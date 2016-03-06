// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.IO;

namespace AqlaSerializer
{
    /// <summary>
    /// Provides a simple buffer-based implementation of an <see cref="IExtension">extension</see> object.
    /// </summary>
    public sealed class BufferExtension : IExtension
    {
        private byte[] _buffer;

        int IExtension.GetLength()
        {
            return _buffer == null ? 0 : _buffer.Length;
        }

        Stream IExtension.BeginAppend()
        {
            return new MemoryStream();
        }

        void IExtension.EndAppend(Stream stream, bool commit)
        {
            using (stream)
            {
                int len;
                if (commit && (len = (int)stream.Length) > 0)
                {
                    MemoryStream ms = (MemoryStream)stream;

                    if (_buffer == null)
                    {   // allocate new buffer
                        _buffer = ms.ToArray();
                    }
                    else
                    {   // resize and copy the data
                        // note: Array.Resize not available on CF
                        int offset = _buffer.Length;
                        byte[] tmp = new byte[offset + len];
                        Helpers.BlockCopy(_buffer, 0, tmp, 0, offset);

#if PORTABLE || WINRT // no GetBuffer() - fine, we'll use Read instead
                        int bytesRead;
                        long oldPos = ms.Position;
                        ms.Position = 0;
                        while (len > 0 && (bytesRead = ms.Read(tmp, offset, len)) > 0)
                        {
                            len -= bytesRead;
                            offset += bytesRead;
                        }
                        if(len != 0) throw new EndOfStreamException();
                        ms.Position = oldPos;
#else
                        Helpers.BlockCopy(ms.GetBuffer(), 0, tmp, offset, len);
#endif
                        _buffer = tmp;
                    }
                }
            }
        }

        Stream IExtension.BeginQuery()
        {
            return _buffer == null ? Stream.Null : new MemoryStream(_buffer);
        }

        void IExtension.EndQuery(Stream stream)
        {
            using (stream) { } // just clean up
        }
    }
}
