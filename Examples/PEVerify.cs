// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Diagnostics;
using NUnit.Framework;

namespace Examples
{
    public static class PEVerify
    {
        public static bool AssertValid(string path)
        {
            // note; PEVerify can be found %ProgramFiles%\Microsoft SDKs\Windows\v6.0A\bin
            const string exePath = "PEVerify.exe";
            var startInfo = new ProcessStartInfo(exePath, path);
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            using (Process proc = Process.Start(startInfo))
            {
                if (proc.WaitForExit(10000))
                {
                    Assert.AreEqual(0, proc.ExitCode, path);
                    return proc.ExitCode == 0;
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
