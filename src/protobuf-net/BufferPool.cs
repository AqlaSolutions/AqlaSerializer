// Modified by Vladyslav Taranov for AqlaSerializer, 2016

using System;
using System.Threading;
namespace AqlaSerializer
{
    internal sealed class BufferPool
    {
        internal static void Flush()
        {
#if PLAT_NO_INTERLOCKED
            lock(Pool)
            {
                for (int i = 0; i < Pool.Length; i++) Pool[i] = null;
            }
#else
            for (int i = 0; i < Pool.Length; i++)
            {
                Interlocked.Exchange(ref Pool[i], null); // and drop the old value on the floor
            }
#endif
        }
        private BufferPool() { }
        const int PoolSize = 20;
        internal const int BufferLength = 1024;
        private static readonly object[] Pool = new object[PoolSize];

        internal static byte[] GetBuffer()
        {
            object tmp;
            #if PLAT_NO_INTERLOCKED
            lock(Pool)
            {
                for (int i = 0; i < Pool.Length; i++)
                {
                    if((tmp = Pool[i]) != null)
                    {
                        Pool[i] = null;
                        return (byte[])tmp;
                    }
                }
            }
#else
            for (int i = 0; i < Pool.Length; i++)
            {
                if ((tmp = Interlocked.Exchange(ref Pool[i], null)) != null) return (byte[])tmp;
            }
#endif
            return new byte[BufferLength];
        }

        static readonly bool Is32Bit = IntPtr.Size <= 4;

        internal static void ResizeAndFlushLeft(ref byte[] buffer, int toFitAtLeastBytes, int copyFromIndex, int copyBytes)
        {
            Helpers.DebugAssert(buffer != null);
            Helpers.DebugAssert(toFitAtLeastBytes > buffer.Length);
            Helpers.DebugAssert(copyFromIndex >= 0);
            Helpers.DebugAssert(copyBytes >= 0);

            // try growing, else match
            int newLength = (Is32Bit && toFitAtLeastBytes > 1024 * 1024 * 50)
                // in 32bit when > 50mb grow only linearly
                ? (toFitAtLeastBytes + 1024 * 1024 * 10)
                // in 64bit grow twice and add 10 kb extra if not enough
                : Math.Max(buffer.Length * 2, toFitAtLeastBytes + 1024 * 10);
            
            byte[] newBuffer = new byte[newLength];
            if (copyBytes > 0)
            {
                Helpers.BlockCopy(buffer, copyFromIndex, newBuffer, 0, copyBytes);
            }
            if (buffer.Length == BufferPool.BufferLength)
            {
                BufferPool.ReleaseBufferToPool(ref buffer);
            }
            buffer = newBuffer;
        }
        internal static void ReleaseBufferToPool(ref byte[] buffer)
        {
            if (buffer == null) return;
            if (buffer.Length == BufferLength)
            {
#if PLAT_NO_INTERLOCKED
                lock (Pool)
                {
                    for (int i = 0; i < Pool.Length; i++)
                    {
                        if(Pool[i] == null)
                        {
                            Pool[i] = buffer;
                            break;
                        }
                    }
                }
#else
                for (int i = 0; i < Pool.Length; i++)
                {
                    if (Interlocked.CompareExchange(ref Pool[i], buffer, null) == null)
                    {
                        break; // found a null; swapped it in
                    }
                }
#endif
            }
            // if no space, just drop it on the floor
            buffer = null;
        }

    }
}