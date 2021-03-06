﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Examples
{
    public static class PEVerify
    {
        public static void AssertValid(string path)
        {
#if FAKE_COMPILE
            return;
#endif
#if NET5_0
            var references = Assembly.LoadFile(Path.GetFullPath(path)).GetReferencedAssemblies().Select(x => x.CodeBase).Where(x => x != null).ToArray();
            var errors = new ILVerify.ILVerify(path, references).Run().ToList();
            Assert.IsEmpty(errors, "Checking "+ path);
            return;
#endif
            // note; PEVerify can be found %ProgramFiles%\Microsoft SDKs\Windows\v6.0A\bin
            const string exePath = "PEVerify.exe";
            var startInfo = new ProcessStartInfo(exePath, path);
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            startInfo.StandardOutputEncoding = Encoding.GetEncoding(866);
            using (Process proc = Process.Start(startInfo))
            {
                bool ok = proc.WaitForExit(20000);
                string output = proc.StandardOutput.ReadToEnd();
                if (ok)
                {
                    Assert.AreEqual(0, proc.ExitCode, path + "\r\n" + output);
                    return;
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
                    Assert.Fail("PEVerify timeout: "+ path + "\r\n" + output);
                    return;
                }
            }
        }
    }
}
