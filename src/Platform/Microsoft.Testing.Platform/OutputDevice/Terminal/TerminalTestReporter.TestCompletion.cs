// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
    internal void TestCompleted(
        string testNodeUid,
        string displayName,
        TestOutcome outcome,
        TimeSpan? duration,
        string? informativeMessage,
        string? errorMessage,
        Exception? exception,
        string? expected,
        string? actual,
        string? standardOutput,
        string? errorOutput)
    {
        FlatException[] flatExceptions = ExceptionFlattener.Flatten(errorMessage, exception);
        TestCompleted(
            testNodeUid,
            displayName,
            outcome,
            duration,
            informativeMessage,
            flatExceptions,
            expected,
            actual,
            standardOutput,
            errorOutput);
    }

    private void TestCompleted(
        string testNodeUid,
        string displayName,
        TestOutcome outcome,
        TimeSpan? duration,
        string? informativeMessage,
        FlatException[] exceptions,
        string? expected,
        string? actual,
        string? standardOutput,
        string? errorOutput)
    {
        if (_testProgressState is null)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        TestProgressState asm = _testProgressState;

        if (_options.ShowActiveTests)
        {
            asm.TestNodeResultsState?.RemoveRunningTestNode(testNodeUid);
        }

        switch (outcome)
        {
            case TestOutcome.Error:
            case TestOutcome.Timeout:
            case TestOutcome.Canceled:
            case TestOutcome.Fail:
                asm.FailedTests++;
                asm.TotalTests++;
                break;
            case TestOutcome.Passed:
                asm.PassedTests++;
                asm.TotalTests++;
                break;
            case TestOutcome.Skipped:
                asm.SkippedTests++;
                asm.TotalTests++;
                break;
        }

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
        _terminalWithProgress.NotifyTestCompleted();
        if (outcome != TestOutcome.Passed || GetShowPassedTests())
        {
            _terminalWithProgress.WriteToTerminal(terminal => RenderTestCompleted(
                terminal,
                displayName,
                outcome,
                duration,
                informativeMessage,
                exceptions,
                expected,
                actual,
                standardOutput,
                errorOutput));
        }
    }

    private bool GetShowPassedTests()
    {
        _shouldShowPassedTests ??= _options.ShowPassedTests();
        return _shouldShowPassedTests.Value;
    }

    private void RenderTestCompleted(
        ITerminal terminal,
        string displayName,
        TestOutcome outcome,
        TimeSpan? duration,
        string? informativeMessage,
        FlatException[] flatExceptions,
        string? expected,
        string? actual,
        string? standardOutput,
        string? errorOutput)
    {
        if (outcome == TestOutcome.Passed && !GetShowPassedTests())
        {
            return;
        }

        TerminalColor color = outcome switch
        {
            TestOutcome.Error or TestOutcome.Fail or TestOutcome.Canceled or TestOutcome.Timeout => TerminalColor.DarkRed,
            TestOutcome.Skipped => TerminalColor.DarkYellow,
            TestOutcome.Passed => TerminalColor.DarkGreen,
            _ => throw new NotSupportedException(),
        };
        string outcomeText = outcome switch
        {
            TestOutcome.Fail or TestOutcome.Error => TerminalResources.FailedLowercase,
            TestOutcome.Skipped => TerminalResources.SkippedLowercase,
            TestOutcome.Canceled or TestOutcome.Timeout => $"{TerminalResources.FailedLowercase} ({TerminalResources.CancelledLowercase})",
            TestOutcome.Passed => TerminalResources.PassedLowercase,
            _ => throw new NotSupportedException(),
        };

        terminal.SetColor(color);
        terminal.Append(outcomeText);
        terminal.ResetColor();
        terminal.Append(' ');
        terminal.Append(MakeControlCharactersVisible(displayName, true));

        if (duration.HasValue)
        {
            terminal.Append(' ');
            AppendLongDuration(terminal, duration.Value);
        }

        terminal.AppendLine();

        AppendIndentedLine(terminal, informativeMessage, SingleIndentation);
        FormatErrorMessage(terminal, flatExceptions, outcome, 0);
        FormatExpectedAndActual(terminal, expected, actual);
        FormatStackTrace(terminal, flatExceptions, 0);
        FormatInnerExceptions(terminal, flatExceptions);

        bool isFailed = outcome is TestOutcome.Fail or TestOutcome.Error or TestOutcome.Timeout or TestOutcome.Canceled;
        string? stdoutToShow = _options.ShowStdout switch
        {
            OutputShowMode.All => standardOutput,
            OutputShowMode.Failed => isFailed ? standardOutput : null,
            OutputShowMode.None => null,
            _ => throw ApplicationStateGuard.Unreachable(),
        };
        string? stderrToShow = _options.ShowStderr switch
        {
            OutputShowMode.All => errorOutput,
            OutputShowMode.Failed => isFailed ? errorOutput : null,
            OutputShowMode.None => null,
            _ => throw ApplicationStateGuard.Unreachable(),
        };
        FormatStandardAndErrorOutput(terminal, stdoutToShow, stderrToShow);
    }
}
