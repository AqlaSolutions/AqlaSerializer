﻿
// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NUnit.Framework;
using System.Diagnostics;
using System.Reflection;

namespace AqlaSerializer.unittest
{
    static public class PEVerify
    {
        public static void Verify(string path)
        {
            Verify(path, 0, true);
        }
        public static void Verify(string path, int exitCode)
        {
            Verify(path, 0, true);
        }
        public static void Verify(string path, int exitCode, bool deleteOnSuccess)
        {
#if FAKE_COMPILE
            return;
#endif
#if NET5_0
            var references = Assembly.LoadFile(Path.GetFullPath(path)).GetReferencedAssemblies().Select(x => x.CodeBase).Where(x => x != null).ToArray();
            var errors = new ILVerify.ILVerify(path, references).Run().ToList();
            Assert.IsEmpty(errors);
            return;
#endif
            // note; PEVerify can be found %ProgramFiles%\Microsoft SDKs\Windows\v6.0A\bin
            const string exePath = "PEVerify.exe";
            ProcessStartInfo startInfo = new ProcessStartInfo(exePath, path);
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            startInfo.StandardOutputEncoding = Encoding.GetEncoding(866);
            using (Process proc = Process.Start(startInfo))
            {
                bool ok = proc.WaitForExit(10000);
                string output = proc.StandardOutput.ReadToEnd();
                if (ok)
                {
                    Assert.AreEqual(exitCode, proc.ExitCode, path + "\r\n" + output);
#if !DEBUG_COMPILE && !DEBUG_COMPILE_2
                    if (deleteOnSuccess) File.Delete(path);
#endif
                }
                else
                {
                    try
                    {
                        proc.Kill();
                    }
                    catch
                    {
                    }
                    Assert.Fail("PEVerify timeout: " + path + "\r\n" + output);
                }
            }
        }
    }
}
