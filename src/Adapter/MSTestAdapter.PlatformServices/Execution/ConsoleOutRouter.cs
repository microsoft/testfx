// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class ConsoleOutRouter : ConsoleRouter
{
    public ConsoleOutRouter(TextWriter originalConsoleOut, Func<TestOutputCaptureMode> modeProvider)
        : base(originalConsoleOut, modeProvider)
    {
    }

    protected override void WriteToTestContext(TestContextImplementation testContext, char value)
        => testContext.StandardOutputBuilder.Append(value);

    protected override void WriteToTestContext(TestContextImplementation testContext, string? value)
        => testContext.StandardOutputBuilder.Append(value);

    protected override void WriteToTestContext(TestContextImplementation testContext, char[] buffer, int index, int count)
        => testContext.StandardOutputBuilder.Append(buffer, index, count);
}
