// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class ConsoleOutCapturer : TextWriter
{
    private readonly TextWriter _originalConsoleOut;

    public ConsoleOutCapturer(TextWriter originalConsoleOut)
        => _originalConsoleOut = originalConsoleOut;

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (TestContextImplementation.CurrentTestContext is { } testContext)
        {
            testContext.WriteConsoleOut(value);
        }
        else
        {
            _originalConsoleOut.Write(value);
        }
    }

    public override void Write(string? value)
    {
        if (TestContextImplementation.CurrentTestContext is { } testContext)
        {
            testContext.WriteConsoleOut(value);
        }
        else
        {
            _originalConsoleOut.Write(value);
        }
    }
}
