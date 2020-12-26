// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using AqlaSerializer.Serializers;
using System.IO;
using NUnit.Framework;
using AqlaSerializer.Meta;
using AqlaSerializer.Compiler;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AqlaSerializer.unittest.Serializers
{
    static class Util
    {
        public static void Test(object value, Type innerType, Func<IProtoSerializer, IProtoSerializer> ctor,
            string expectedHex)
        {
            byte[] expected = new byte[expectedHex.Length / 2];
            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = (byte)Convert.ToInt32(expectedHex.Substring(i*2,2),16);
            }
            NilSerializer nil = new NilSerializer(innerType);
            var ser = ctor(nil);

            var model = RuntimeTypeModel.Create();
            var decorator = model.GetSerializer(ser, false);
            Test(value, decorator, "decorator", expected);

            var compiled = model.GetSerializer(ser, true);
            Test(value, compiled, "compiled", expected);
        }
        public static void Test(object obj, ProtoSerializer serializer, string message, byte[] expected)
        {
            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                int reported;
                using (ProtoWriter writer = new ProtoWriter(ms, RuntimeTypeModel.Default, null))
                {
                    serializer(obj, writer);
                    reported = ProtoWriter.GetPosition(writer);
                }
                data = ms.ToArray();
                Assert.AreEqual(reported, data.Length, message + ":reported/actual");
            }
            Assert.AreEqual(expected.Length, data.Length, message + ":Length");
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(expected[i], data[i], message + ":" + i);
            }
        }

        static int _testCounter;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TestModel(RuntimeTypeModel model, object value, string hex, [Values(false, true)] bool comp)
        {
            byte[] raw;
            using (MemoryStream ms = new MemoryStream())
            {
                model.Serialize(ms, value);
                raw = ms.ToArray();
            }

            if (comp)
                Assert.AreEqual(hex, GetHex(raw));

            model.CompileInPlace();
            using (MemoryStream ms = new MemoryStream())
            {
                model.Serialize(ms, value);
                raw = ms.ToArray();
            }

            if (comp)
                Assert.AreEqual(hex, GetHex(raw));

            var name = new StackFrame(1).GetMethod().Name + Interlocked.Increment(ref _testCounter);

            TypeModel compiled = model.Compile("compiled", $"compiled{name}.dll");
            PEVerify.Verify($"compiled{name}.dll");
            using (MemoryStream ms = new MemoryStream())
            {
                compiled.Serialize(ms, value);
                raw = ms.ToArray();
            }
            if (comp)
                Assert.AreEqual(hex, GetHex(raw));

        }
        
        public static void Test<T>(T value, Func<IProtoSerializer, IProtoSerializer> ctor, string expectedHex)
        {
            Test(value, typeof(T), ctor, expectedHex);
        }
        internal static string GetHex(byte[] bytes)
        {
            int len = bytes.Length;
            StringBuilder sb = new StringBuilder(len * 2);
            for (int i = 0; i < len; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }
        public static void Test(Action<ProtoWriter> action, string expectedHex)
        {
            using (var ms = new MemoryStream())
            {
                using (var pw = new ProtoWriter(ms, RuntimeTypeModel.Default, null))
                {
                    action(pw);
                }
                string s = GetHex(ms.ToArray());               
                Assert.AreEqual(expectedHex, s);
            }
        }
    }
}
