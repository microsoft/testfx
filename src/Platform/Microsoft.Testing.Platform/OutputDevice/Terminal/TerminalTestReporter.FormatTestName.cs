// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
    internal void TestDiscovered(TestNode testNode)
        => TestDiscovered(FormatTestName(testNode));

    internal void TestInProgress(TestNode testNode)
        => TestInProgress(testNode.Uid.Value, FormatTestName(testNode));

    internal void TestCompleted(
        TestNode testNode,
        TestOutcome outcome,
        TimeSpan? duration,
        string? informativeMessage,
        string? errorMessage,
        Exception? exception,
        string? expected,
        string? actual,
        string? standardOutput,
        string? errorOutput)
        => TestCompleted(
            testNode.Uid.Value,
            FormatTestName(testNode),
            outcome,
            duration,
            informativeMessage,
            errorMessage,
            exception,
            expected,
            actual,
            standardOutput,
            errorOutput);

    private string FormatTestName(TestNode testNode)
        => _options.TestNameFormatter is { } formatter
            ? formatter.Format(testNode)
            : testNode.DisplayName;
}
