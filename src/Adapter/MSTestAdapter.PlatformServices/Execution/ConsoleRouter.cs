// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal abstract class ConsoleRouter : TextWriter
{
    private readonly TextWriter _originalConsole;
    private readonly bool _echoLive;

    protected ConsoleRouter(TextWriter originalConsole, bool echoLive)
    {
        _originalConsole = originalConsole;

        // Avoid re-entrant double capture: if we are wrapping another ConsoleRouter (for example the
        // test host reused the process and a router was installed by a previous run), echoing to it while
        // TestContext.Current is set would re-enter that router's capture path and record the output an
        // extra time. Only echo when the underlying writer is a real console.
        _echoLive = echoLive && originalConsole is not ConsoleRouter;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (TestContext.Current is TestContextImplementation testContext)
        {
            WriteToTestContext(testContext, value);
            if (_echoLive)
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
        if (TestContext.Current is TestContextImplementation testContext)
        {
            WriteToTestContext(testContext, value);
            if (_echoLive)
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
        if (TestContext.Current is TestContextImplementation testContext)
        {
            WriteToTestContext(testContext, buffer, index, count);
            if (_echoLive)
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
