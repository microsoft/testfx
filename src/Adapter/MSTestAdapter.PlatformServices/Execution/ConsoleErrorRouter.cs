// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class ConsoleErrorRouter : ConsoleRouter
{
    public ConsoleErrorRouter(TextWriter originalConsoleErr, bool echoLive)
        : base(originalConsoleErr, echoLive)
    {
    }

    protected override void WriteToTestContext(TestContextImplementation testContext, char value)
        => testContext.WriteConsoleErr(value);

    protected override void WriteToTestContext(TestContextImplementation testContext, string? value)
        => testContext.WriteConsoleErr(value);

    protected override void WriteToTestContext(TestContextImplementation testContext, char[] buffer, int index, int count)
        => testContext.WriteConsoleErr(buffer, index, count);
}
