// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class ConsoleErrorCapturer : TextWriter
{
    private readonly TextWriter _originalConsoleErr;

    public ConsoleErrorCapturer(TextWriter originalConsoleErr)
        => _originalConsoleErr = originalConsoleErr;

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (TestContextImplementation.CurrentTestContext is { } testContext)
        {
            testContext.WriteConsoleErr(value);
        }
        else
        {
            _originalConsoleErr.Write(value);
        }
    }

    public override void Write(string? value)
    {
        if (TestContextImplementation.CurrentTestContext is { } testContext)
        {
            testContext.WriteConsoleErr(value);
        }
        else
        {
            _originalConsoleErr.Write(value);
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        if (TestContextImplementation.CurrentTestContext is { } testContext)
        {
            testContext.WriteConsoleErr(buffer, index, count);
        }
        else
        {
            _originalConsoleErr.Write(buffer, index, count);
        }
    }
}
