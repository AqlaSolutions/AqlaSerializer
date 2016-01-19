using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

[SetUpFixture]
public class LeakDetection
{
    private UnhandledExceptionEventHandler _unhandledExceptionHandler;
    readonly List<object> _exceptions = new List<object>();

    public Action<IList<object>> UnhandledExceptionCheck { get; set; }
    
    [SetUp]
    public void UnhandledExceptionRegistering()
    {
        _exceptions.Clear();
        UnhandledExceptionCheck = DefaultExceptionCheck;
        _unhandledExceptionHandler = (s, e) =>
        {
            _exceptions.Add(e.ExceptionObject);

            Debug.WriteLine(e.ExceptionObject);
        };

        AppDomain.CurrentDomain.UnhandledException += _unhandledExceptionHandler;
    }

    void DefaultExceptionCheck(IList<object> e)
    {
        Assert.IsTrue(e.Count == 0, string.Join("\r\n\r\n", e.Select(ex => ex.ToString()).ToArray()));
    }

    [TearDown]
    public void VerifyUnhandledExceptionOnFinalizers()
    {
        GC.GetTotalMemory(true);

        UnhandledExceptionCheck(_exceptions);

        AppDomain.CurrentDomain.UnhandledException -= _unhandledExceptionHandler;
    }
}