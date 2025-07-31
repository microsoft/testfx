// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.PlatformServices.Execution;

internal sealed class ConsoleOutRouter : TextWriter
{
    private readonly TextWriter _originalConsoleOut;

    public ConsoleOutRouter(TextWriter originalConsoleOut)
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

    public override void Write(char[] buffer, int index, int count)
    {
        if (TestContextImplementation.CurrentTestContext is { } testContext)
        {
            testContext.WriteConsoleOut(buffer, index, count);
        }
        else
        {
            _originalConsoleOut.Write(buffer, index, count);
        }
    }
}
