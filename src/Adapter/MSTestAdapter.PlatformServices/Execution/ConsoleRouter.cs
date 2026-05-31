// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal abstract class ConsoleRouter : TextWriter
{
    private readonly TextWriter _originalConsole;

    protected ConsoleRouter(TextWriter originalConsole)
        => _originalConsole = originalConsole;

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (TestContext.Current is TestContextImplementation testContext)
        {
            WriteToTestContext(testContext, value);
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
