// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
    internal void TestCompletedWithoutResult(string executionId, string testNodeUid)
    {
        if (!_assemblies.TryGetValue(executionId, out TestProgressState? asm))
        {
            throw ApplicationStateGuard.Unreachable();
        }

        asm.TestNodeResultsState?.RemoveRunningTestNode(testNodeUid);
        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
        _terminalWithProgress.NotifyTestCompleted();
    }

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
        => TestCompleted(
            executionId,
            testNodeUid,
            displayName,
            outcome,
            duration,
            informativeMessage,
            errorMessage,
            exception,
            expected,
            actual,
            standardOutput,
            errorOutput,
            retryAttempt: null);

    /// <summary>
    /// In-process host overload carrying the in-process retry attribution of the result. Passing the
    /// <see cref="Extensions.Messages.RetryAttemptProperty"/> itself rather than its parts keeps the three facts it
    /// carries - the attempt number, that the result belongs to a retry sequence at all, and whether a later
    /// attempt supersedes it - together, so a caller cannot forward some and drop the rest. A framework that does
    /// not retry passes <see langword="null"/>.
    /// </summary>
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
        string? errorOutput,
        RetryAttemptProperty? retryAttempt)
    {
        FlatException[] flatExceptions = ExceptionFlattener.Flatten(errorMessage, exception);
        TestCompleted(
            executionId,
            // In-process host: a single host attempt, so the instance id is the (fixed) execution id. In-process
            // retries are attributed by retryAttempt instead.
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
            errorOutput,
            retryAttempt);
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
            errorOutput,
            // The orchestrator attributes retries per host instance; it does not surface a test framework's
            // in-process retry attempt, so results arriving through this path have no in-process attribution.
            retryAttempt: null);

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
        string? errorOutput,
        RetryAttemptProperty? retryAttempt)
    {
        if (!_assemblies.TryGetValue(executionId, out TestProgressState? asm))
        {
            throw ApplicationStateGuard.Unreachable();
        }

        int retryAttemptNumber = retryAttempt?.AttemptNumber ?? 1;
        bool isRetryAttempt = retryAttempt is not null;

        // A retry sequence has one in-progress update for the whole test but a terminal update per attempt, so the
        // running node is only removed once the final attempt arrives - otherwise --show-active-tests would drop
        // the test while its next attempt is still executing.
        if (_options.ShowActiveTests && retryAttempt is not { IsSuperseded: true })
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
                asm.ReportFailedTest(testNodeUid, displayName, instanceId, retryAttemptNumber);
                break;
            case TestOutcome.Passed:
                asm.ReportPassingTest(testNodeUid, displayName, instanceId, retryAttemptNumber);
                break;
            case TestOutcome.Skipped:
                asm.ReportSkippedTest(testNodeUid, displayName, instanceId, retryAttemptNumber);
                break;
        }

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
        _terminalWithProgress.NotifyTestCompleted();
        if (IsTestResultVisible(outcome))
        {
            // Resolve the attempt from the result's instance so multiple instances can participate in one attempt.
            int hostAttempt = asm.GetAttemptNumber(instanceId);

            // An in-process retry attempt is annotated even when the run is not an orchestrator retry, otherwise a
            // [Retry]-decorated test's attempts would look like duplicate results. When both mechanisms are active
            // the in-process attempt wins the annotation, since that is the one that distinguishes the repeated
            // lines within this host.
            bool showAttempt = _isRetry || isRetryAttempt;
            int attempt = isRetryAttempt ? retryAttemptNumber : hostAttempt;
            _terminalWithProgress.WriteToTerminal(terminal => RenderTestCompleted(
                terminal,
                showAttempt,
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

    /// <summary>
    /// Determines whether a completed test's per-test terminal block (result line, informative details, stack
    /// trace, expected/actual, and captured stdout/stderr) should be rendered, based on
    /// <see cref="TerminalTestReporterOptions.ShowTestResults"/>. <see cref="TestOutcome.Fail"/>,
    /// <see cref="TestOutcome.Error"/>, <see cref="TestOutcome.Timeout"/>, and <see cref="TestOutcome.Canceled"/> are
    /// all gated by <see cref="TestResultVisibility.Failed"/>: they render identically (see
    /// <see cref="RenderTestCompleted"/>) and are indistinguishable to a user deciding what to see.
    /// </summary>
    private bool IsTestResultVisible(TestOutcome outcome) => outcome switch
    {
        TestOutcome.Passed => (_options.ShowTestResults & TestResultVisibility.Passed) != 0,
        TestOutcome.Skipped => (_options.ShowTestResults & TestResultVisibility.Skipped) != 0,
        TestOutcome.Fail or TestOutcome.Error or TestOutcome.Timeout or TestOutcome.Canceled => (_options.ShowTestResults & TestResultVisibility.Failed) != 0,
        _ => throw new NotSupportedException(),
    };

    private void RenderTestCompleted(
        ITerminal terminal,
        bool showAttempt,
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
        // The caller (TestCompleted) only queues this render when IsTestResultVisible(outcome) is true, so there
        // is no need to re-check visibility here.
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

        // Annotate which attempt this result belongs to (e.g. "failed (try 2)") so retried results are not mistaken
        // for duplicates. This is set for the dotnet test orchestrator's per-host retries and for a test framework's
        // in-process retries; a run with neither leaves showAttempt false, so its per-test lines are unchanged.
        if (showAttempt)
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
