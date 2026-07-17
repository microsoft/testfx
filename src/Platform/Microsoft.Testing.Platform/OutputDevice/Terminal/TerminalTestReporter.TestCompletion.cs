// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
    internal void TestCompleted(
        string executionId,
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
            executionId,
            // In-process host: a single attempt, so the instance id is the (fixed) execution id.
            instanceId: executionId,
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

    /// <summary>
    /// Orchestrator overload (<c>dotnet test</c>): carries the assembly/target-framework/architecture and the
    /// per-attempt instance id that the multi-process orchestrator knows. The instance id drives retry attribution
    /// in <see cref="TestProgressState"/>; assembly/tfm/arch are accepted for signature parity and the future
    /// per-test assembly link.
    /// </summary>
    internal void TestCompleted(
        string assembly,
        string? targetFramework,
        string? architecture,
        string executionId,
        string instanceId,
        string testNodeUid,
        string displayName,
        string? informativeMessage,
        TestOutcome outcome,
        TimeSpan? duration,
        FlatException[]? exceptions,
        string? expected,
        string? actual,
        string? standardOutput,
        string? errorOutput)
        // assembly / targetFramework / architecture are intentionally not forwarded yet: they are reserved for the
        // per-test assembly link in a follow-up. The instance id IS forwarded — it drives retry attribution.
        => TestCompleted(
            executionId,
            instanceId,
            testNodeUid,
            displayName,
            outcome,
            duration,
            informativeMessage,
            exceptions ?? [],
            expected,
            actual,
            standardOutput,
            errorOutput);

    private void TestCompleted(
        string executionId,
        string instanceId,
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
        if (!_assemblies.TryGetValue(executionId, out TestProgressState? asm))
        {
            throw ApplicationStateGuard.Unreachable();
        }

        if (_options.ShowActiveTests)
        {
            asm.TestNodeResultsState?.RemoveRunningTestNode(testNodeUid);
        }

        // Record the reported duration for the "slowest tests" summary section. All outcomes are included (a slow
        // test that then fails is still slow). Called on every completion so a retry that reports no timing clears
        // the earlier attempt's stale duration rather than leaving it ranked. Gated on the feature so a run without
        // --show-slowest-tests pays no bookkeeping cost.
        if (_options.SlowestTestsCount > 0)
        {
            asm.RecordTestDuration(testNodeUid, displayName, duration);
        }

        switch (outcome)
        {
            case TestOutcome.Error:
            case TestOutcome.Timeout:
            case TestOutcome.Canceled:
            case TestOutcome.Fail:
                asm.ReportFailedTest(testNodeUid, instanceId);
                break;
            case TestOutcome.Passed:
                asm.ReportPassingTest(testNodeUid, instanceId);
                break;
            case TestOutcome.Skipped:
                asm.ReportSkippedTest(testNodeUid, instanceId);
                break;
        }

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
        _terminalWithProgress.NotifyTestCompleted();
        if (outcome != TestOutcome.Passed || GetShowPassedTests())
        {
            // Resolve the attempt from the result's instance so multiple instances can participate in one attempt.
            int attempt = asm.GetAttemptNumber(instanceId);
            _terminalWithProgress.WriteToTerminal(terminal => RenderTestCompleted(
                terminal,
                attempt,
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
        int attempt,
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

        // Orchestrator-only: annotate which retry attempt this result belongs to (e.g. "failed (try 2)") so retried
        // results are not mistaken for duplicates. _isRetry is only ever set for the dotnet test orchestrator; the
        // in-process host leaves it false, so its per-test lines are unchanged.
        if (_isRetry)
        {
            terminal.SetColor(TerminalColor.DarkGray);
            terminal.Append($" ({string.Format(CultureInfo.CurrentCulture, TerminalResources.Try, attempt)})");
        }

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
