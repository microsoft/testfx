// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class TraceTextWriter : TextWriter
{
    private readonly TextWriter? _liveEchoTarget;

    public TraceTextWriter(TextWriter? liveEchoTarget)
        // Guard against re-entrant double capture: if the echo target is itself a ConsoleRouter (for
        // example the test host reused the process and a router was installed by a previous run), echoing
        // to it while TestContext.Current is set would re-enter its capture path and record the output an
        // extra time. Only echo when the target is a real console.
        => _liveEchoTarget = liveEchoTarget is ConsoleRouter ? null : liveEchoTarget;

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (TestContext.Current as TestContextImplementation is { } testContext)
        {
            testContext.WriteTrace(value);
        }

        _liveEchoTarget?.Write(value);
    }

    public override void Write(string? value)
    {
        if (TestContext.Current as TestContextImplementation is { } testContext)
        {
            testContext.WriteTrace(value);
        }

        _liveEchoTarget?.Write(value);
    }
}
