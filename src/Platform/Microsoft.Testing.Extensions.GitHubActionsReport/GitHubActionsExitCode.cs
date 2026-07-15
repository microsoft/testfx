// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Helpers for turning a Microsoft.Testing.Platform process exit code into a GitHub-friendly verdict.
/// Used by the step-summary and annotation reporters so that non-test-result failures observable once the
/// test session has finished — e.g. a <c>--minimum-expected-tests</c> violation, a run that discovered zero
/// tests, a <c>--maximum-failed-tests</c> stop or a test-adapter session failure — are surfaced instead of
/// silently looking like a passing run. Outcomes that occur outside the end-of-session path (a hard
/// abort/cancellation, or host failures raised before/after the session) are not reachable here.
/// See <see href="https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-troubleshooting#exit-codes"/>.
/// </summary>
internal static class GitHubActionsExitCode
{
    /// <summary>
    /// Returns <see langword="true"/> when the exit code is a normal test-result outcome (everything passed,
    /// or at least one test failed). Those outcomes are already reflected by the passed/failed totals and the
    /// per-test failure annotations, so callers do not surface a separate exit-code callout for them.
    /// </summary>
    public static bool IsTestResultOutcome(int exitCode)
        => exitCode is (int)ExitCode.Success or (int)ExitCode.AtLeastOneTestFailed;

    /// <summary>
    /// Returns <see langword="true"/> when the run did not succeed (any non-zero exit code).
    /// </summary>
    public static bool IndicatesFailure(int exitCode)
        => exitCode != (int)ExitCode.Success;

    /// <summary>
    /// Returns the enum name for a known exit code (e.g. <c>MinimumExpectedTestsPolicyViolation</c>) or
    /// <c>Unknown</c> for a value outside the documented set.
    /// </summary>
    public static string GetName(int exitCode)
        => Enum.IsDefined(typeof(ExitCode), exitCode)
            ? ((ExitCode)exitCode).ToString()
            : "Unknown";

    /// <summary>
    /// Returns a short, human-readable, localized explanation of the exit code.
    /// </summary>
    public static string GetReason(int exitCode)
        => exitCode switch
        {
            (int)ExitCode.Success => GitHubActionsResources.ExitCodeReasonSuccess,
            (int)ExitCode.GenericFailure => GitHubActionsResources.ExitCodeReasonGenericFailure,
            (int)ExitCode.AtLeastOneTestFailed => GitHubActionsResources.ExitCodeReasonAtLeastOneTestFailed,
            (int)ExitCode.TestSessionAborted => GitHubActionsResources.ExitCodeReasonTestSessionAborted,
            (int)ExitCode.InvalidPlatformSetup => GitHubActionsResources.ExitCodeReasonInvalidPlatformSetup,
            (int)ExitCode.InvalidCommandLine => GitHubActionsResources.ExitCodeReasonInvalidCommandLine,
            (int)ExitCode.TestHostProcessExitedNonGracefully => GitHubActionsResources.ExitCodeReasonTestHostProcessExitedNonGracefully,
            (int)ExitCode.ZeroTests => GitHubActionsResources.ExitCodeReasonZeroTests,
            (int)ExitCode.MinimumExpectedTestsPolicyViolation => GitHubActionsResources.ExitCodeReasonMinimumExpectedTestsPolicyViolation,
            (int)ExitCode.TestAdapterTestSessionFailure => GitHubActionsResources.ExitCodeReasonTestAdapterTestSessionFailure,
            (int)ExitCode.DependentProcessExited => GitHubActionsResources.ExitCodeReasonDependentProcessExited,
            (int)ExitCode.IncompatibleProtocolVersion => GitHubActionsResources.ExitCodeReasonIncompatibleProtocolVersion,
            (int)ExitCode.TestExecutionStoppedForMaxFailedTests => GitHubActionsResources.ExitCodeReasonTestExecutionStoppedForMaxFailedTests,
            _ => GitHubActionsResources.ExitCodeReasonUnknown,
        };
}
