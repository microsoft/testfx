// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// We use positive exit codes for failure because POSIX/BASH exit codes are unsigned 8-bit integers.
/// On POSIX systems the standard exit code is 0 for success and any number from 1 to 255 for anything else.
/// See the <see href="https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-troubleshooting#exit-codes">troubleshooting documentation</see> for general exit-code guidance.
/// </summary>
[Embedded]
internal enum ExitCode
{
    /// <summary>
    /// The test run succeeded.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The test run failed for a reason not represented by a more specific exit code.
    /// </summary>
    GenericFailure = 1,

    /// <summary>
    /// At least one test failed.
    /// </summary>
    AtLeastOneTestFailed = 2,

    /// <summary>
    /// The test session was aborted.
    /// </summary>
    TestSessionAborted = 3,

    /// <summary>
    /// The test platform setup or extension configuration is invalid.
    /// </summary>
    InvalidPlatformSetup = 4,

    /// <summary>
    /// The command-line arguments are invalid.
    /// </summary>
    InvalidCommandLine = 5,

    // Value 6 is reserved for the retired FeatureNotImplemented code and must not be reused.

    /// <summary>
    /// The test host process exited without completing its shutdown protocol, or its IPC-reported exit code differed from its OS exit code.
    /// </summary>
    TestHostProcessExitedNonGracefully = 7,

    /// <summary>
    /// No tests counted as executed under the active zero-tests policy.
    /// </summary>
    ZeroTests = 8,

    /// <summary>
    /// Fewer tests ran than required by the minimum-expected-tests policy.
    /// </summary>
    MinimumExpectedTestsPolicyViolation = 9,

    /// <summary>
    /// The test adapter reported a test-session failure.
    /// </summary>
    TestAdapterTestSessionFailure = 10,

    /// <summary>
    /// A process monitored by the dependent-process listener exited.
    /// </summary>
    DependentProcessExited = 11,

    /// <summary>
    /// The communicating test-platform processes use incompatible protocol versions.
    /// </summary>
    IncompatibleProtocolVersion = 12,

    /// <summary>
    /// Test execution stopped after reaching the maximum-failed-tests limit.
    /// </summary>
    TestExecutionStoppedForMaxFailedTests = 13,

    /// <summary>
    /// One or more code-coverage thresholds were not met.
    /// </summary>
    CoverageThresholdFailed = 14,

    /// <summary>
    /// Test execution stopped early because the configured deadline was approaching, so not every test ran.
    /// </summary>
    TestExecutionStoppedAtDeadline = 15,
}
