// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class ConsoleOutRouter : ConsoleRouter
{
    public ConsoleOutRouter(TextWriter originalConsoleOut, bool echoLive)
        : base(originalConsoleOut, echoLive)
    {
    }

    protected override void WriteToTestContext(TestContextImplementation testContext, char value)
        => testContext.WriteConsoleOut(value);

    protected override void WriteToTestContext(TestContextImplementation testContext, string? value)
        => testContext.WriteConsoleOut(value);

    protected override void WriteToTestContext(TestContextImplementation testContext, char[] buffer, int index, int count)
        => testContext.WriteConsoleOut(buffer, index, count);
}
