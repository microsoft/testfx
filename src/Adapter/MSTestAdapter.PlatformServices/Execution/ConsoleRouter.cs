// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal abstract class ConsoleRouter : TextWriter
{
    private readonly TextWriter _originalConsole;
    private readonly Func<TestOutputCaptureMode> _modeProvider;

    protected ConsoleRouter(TextWriter originalConsole, Func<TestOutputCaptureMode> modeProvider)
    {
        _originalConsole = originalConsole;
        _modeProvider = modeProvider;
    }

    public override Encoding Encoding => Encoding.UTF8;

    // The routers are installed once per process, but the capture mode is read on every write so a
    // reused host that changes OutputCaptureMode between runs is honored:
    //   None   -> pass straight through to the console (do not capture)
    //   Result -> capture into the current test result (do not echo)
    //   Live   -> capture into the current test result and echo live to the console
    // Writes made when no test is running always pass straight through.
    public override void Write(char value)
    {
        TestOutputCaptureMode mode = _modeProvider();
        if (mode != TestOutputCaptureMode.None && TestContext.Current is TestContextImplementation testContext)
        {
            WriteToTestContext(testContext, value);
            if (mode == TestOutputCaptureMode.Live)
            {
                _originalConsole.Write(value);
            }
        }
        else
        {
            _originalConsole.Write(value);
        }
    }

    public override void Write(string? value)
    {
        TestOutputCaptureMode mode = _modeProvider();
        if (mode != TestOutputCaptureMode.None && TestContext.Current is TestContextImplementation testContext)
        {
            WriteToTestContext(testContext, value);
            if (mode == TestOutputCaptureMode.Live)
            {
                _originalConsole.Write(value);
            }
        }
        else
        {
            _originalConsole.Write(value);
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        TestOutputCaptureMode mode = _modeProvider();
        if (mode != TestOutputCaptureMode.None && TestContext.Current is TestContextImplementation testContext)
        {
            WriteToTestContext(testContext, buffer, index, count);
            if (mode == TestOutputCaptureMode.Live)
            {
                _originalConsole.Write(buffer, index, count);
            }
        }
        else
        {
            _originalConsole.Write(buffer, index, count);
        }
    }

    protected abstract void WriteToTestContext(TestContextImplementation testContext, char value);

    protected abstract void WriteToTestContext(TestContextImplementation testContext, string? value);

    protected abstract void WriteToTestContext(TestContextImplementation testContext, char[] buffer, int index, int count);
}
