// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class TraceTextWriter : TextWriter
{
    private readonly TextWriter _console;
    private readonly Func<TestOutputCaptureMode> _modeProvider;

    public TraceTextWriter(TextWriter console, Func<TestOutputCaptureMode> modeProvider)
    {
        _console = console;
        _modeProvider = modeProvider;
    }

    public override Encoding Encoding => Encoding.UTF8;

    // Installed once per process; the capture mode is read on every write so a reused host that changes
    // OutputCaptureMode between runs is honored. Trace is only captured/echoed while a test is running:
    // framework/adapter trace emitted during host setup or between tests is left to the default listeners.
    public override void Write(char value)
    {
        TestOutputCaptureMode mode = _modeProvider();
        if (mode != TestOutputCaptureMode.None && TestContext.Current as TestContextImplementation is { } testContext)
        {
            testContext.TraceBuilder.Append(value);
            if (mode == TestOutputCaptureMode.Live)
            {
                _console.Write(value);
            }
        }
    }

    public override void Write(string? value)
    {
        TestOutputCaptureMode mode = _modeProvider();
        if (mode != TestOutputCaptureMode.None && TestContext.Current as TestContextImplementation is { } testContext)
        {
            testContext.TraceBuilder.Append(value);
            if (mode == TestOutputCaptureMode.Live)
            {
                _console.Write(value);
            }
        }
    }
}
