﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using DAL;
using Examples.SimpleStream;
using AqlaSerializer;
using AqlaSerializer.Meta;
using NUnit.Framework;

namespace Examples
{
    public class Program
    {
        static void Main() {
            Console.WriteLine("CLR: " + Environment.Version);
            new NWindTests().PerfTestDb();
        }
        static void Main2() {
            SimpleStreamDemo demo = new SimpleStreamDemo();
            //const int COUNT = 1000000;
            const bool RUN_LEGACY = true;
            //demo.PerfTestSimple(COUNT, RUN_LEGACY);
            //demo.PerfTestString(COUNT, RUN_LEGACY);
            //demo.PerfTestEmbedded(COUNT, RUN_LEGACY);
            //demo.PerfTestEnum(COUNT, true);
            //demo.PerfTestArray(COUNT, true);

            const int NWIND_COUNT = 1000;
            DAL.Database db = DAL.NWindTests.LoadDatabaseFromFile<DAL.Database>(RuntimeTypeModel.Default);
            Console.WriteLine("Sub-object format: {0}", DAL.Database.SubObjectFormat);
            SimpleStreamDemo.LoadTestItem(db, NWIND_COUNT, NWIND_COUNT, false, false, false, true, false, false, null);

            DatabaseCompat compat = DAL.NWindTests.LoadDatabaseFromFile<DatabaseCompat>(RuntimeTypeModel.Default);
            SimpleStreamDemo.LoadTestItem(compat, NWIND_COUNT, NWIND_COUNT, RUN_LEGACY, false, RUN_LEGACY, true, false, true, null);

            DatabaseCompatRem compatRem = DAL.NWindTests.LoadDatabaseFromFile<DatabaseCompatRem>(RuntimeTypeModel.Default);
            SimpleStreamDemo.LoadTestItem(compatRem, NWIND_COUNT, NWIND_COUNT, true, false, true, false, false, false, null);
            
        }

        public static string GetByteString(byte[] buffer)
        {
            if (buffer == null) return "[null]";
            if (buffer.Length == 0) return "[empty]";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < buffer.Length; i++)
            {
                sb.Append(buffer[i].ToString("X2")).Append(' ');
            }
            sb.Length -= 1;
            return sb.ToString();
        }
        public static string GetByteString<T>(T item) where T : class,new()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Serializer.Serialize(ms, item);
                byte[] actual = ms.ToArray();
                return GetByteString(actual);
            }
        }
        public static bool CheckBytes<T>(T item, TypeModel model, params byte[] expected)
        {
            var m = TypeModel.Create(false, ProtoCompatibilitySettingsValue.Incompatible);
            m.DeepClone(item);

            if (model == null) model = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
            var rtm = model as RuntimeTypeModel;
            if (rtm != null)
                rtm.AddNotAsReferenceDefault = true;

            using (MemoryStream ms = new MemoryStream())
            {
                model.Serialize(ms, item);
                ms.Position = 0;
                byte[] actual = ms.ToArray();
                bool equal = Program.ArraysEqual(actual, expected);
                if (!equal)
                {
                    string exp = GetByteString(expected), act = GetByteString(actual);
                    //Console.WriteLine("Expected: {0}", exp);
                    //Console.WriteLine("Actual: {0}", act);
                    ms.Position = 0;
                    var d = model.Deserialize<T>(ms);
                    Assert.That(act, Is.EqualTo(exp));
                }
                return equal;
            }
        }
        public static bool CheckBytes<T>(T item, params byte[] expected)
        {
            return CheckBytes<T>(item, null, expected);
        }
        public static T Build<T>(params byte[] raw) where T : class, new()
        {
            using (MemoryStream ms = new MemoryStream(raw))
            {
                var tm = TypeModel.Create(false, ProtoCompatibilitySettingsValue.FullCompatibility);
                return tm.Deserialize<T>(ms);
            }
        }
        public static bool ArraysEqual(byte[] actual, byte[] expected)
        {
            if (ReferenceEquals(actual, expected)) return true;
            if (actual == null || expected == null) return false;
            if (actual.Length != expected.Length) return false;
            for (int i = 0; i < actual.Length; i++)
            {
                if (actual[i] != expected[i]) return false;
            }
            return true;
        }

    }
}
