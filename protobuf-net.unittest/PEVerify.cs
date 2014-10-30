﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NUnit.Framework;
using System.Diagnostics;

namespace AqlaSerializer.unittest
{
    static class PEVerify
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
            // note; PEVerify can be found %ProgramFiles%\Microsoft SDKs\Windows\v6.0A\bin
            const string exePath = "PEVerify.exe";
            ProcessStartInfo psi = new ProcessStartInfo(exePath, path);
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            using (Process proc = Process.Start(psi))
            {
                if (proc.WaitForExit(10000))
                {
                    Assert.AreEqual(exitCode, proc.ExitCode, path);
                    if(deleteOnSuccess) File.Delete(path);
                }
                else
                {
                    proc.Kill();
                    throw new TimeoutException();
                }
            }
        }
    }
}
