// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Diagnostics;
using System.Text;
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
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            startInfo.StandardOutputEncoding = Encoding.GetEncoding(866);
            using (Process proc = Process.Start(startInfo))
            {
                bool ok = proc.WaitForExit(10000);
                string output = proc.StandardOutput.ReadToEnd();
                if (ok)
                {
                    Assert.AreEqual(0, proc.ExitCode, path + "\r\n" + output);
                    return proc.ExitCode == 0;
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
                    return false;
                }
            }
        }
    }
}
