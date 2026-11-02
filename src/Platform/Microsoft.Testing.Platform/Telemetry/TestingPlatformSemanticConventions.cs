// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

/// <summary>
/// Well-known attribute and instrument names emitted by Microsoft.Testing.Platform.
/// </summary>
/// <remarks>
/// <para>
/// Where an OpenTelemetry semantic convention exists, its exact name is used: <c>test.*</c>, <c>code.*</c>,
/// <c>error.*</c>, <c>cicd.*</c>, <c>vcs.*</c>, <c>host.*</c>, <c>os.*</c> and <c>process.*</c>. Several of those
/// are still at "development" or "release candidate" stability upstream, so keeping every name in one file means a
/// rename upstream is a single-file change here.
/// </para>
/// <para>
/// Names marked "platform extension" below have <b>no</b> upstream definition as of semantic conventions 1.43.0 -
/// notably there are no upstream <c>test.*</c> metrics at all, and no upstream span conventions for test cases.
/// They deliberately sit in the <c>test.*</c> namespace because that is where an upstream definition would land,
/// and they are documented as ours rather than presented as standard.
/// </para>
/// </remarks>
internal static class TestingPlatformSemanticConventions
{
    /// <summary>
    /// Attribute names.
    /// </summary>
    internal static class Attributes
    {
        // ---- test.* (OpenTelemetry semantic conventions for testing, development stability) ----
        internal const string TestCaseName = "test.case.name";
        internal const string TestCaseResultStatus = "test.case.result.status";
        internal const string TestSuiteName = "test.suite.name";

        // ---- code.* (stable) ----
        // The convention asks for the *fully qualified* name here; there is no separate namespace attribute
        // (code.namespace is deprecated upstream).
        internal const string CodeFunctionName = "code.function.name";
        internal const string CodeFilePath = "code.file.path";
        internal const string CodeLineNumber = "code.line.number";
        internal const string CodeStacktrace = "code.stacktrace";

        // ---- error.* (stable) ----
        // Only error.type: error.message is deprecated upstream and is NOT RECOMMENDED on spans because of its
        // unbounded cardinality. The message travels on the exception event instead.
        internal const string ErrorType = "error.type";

        // ---- Platform extensions: no upstream definition ----
        internal const string TestCaseId = "test.case.id";
        internal const string TestCaseParentId = "test.case.parent.id";
        internal const string TestCaseResultExplanation = "test.case.result.explanation";
        internal const string TestCaseTimeoutMilliseconds = "test.case.timeout";

        // Deliberately not "test.case.duration": that name belongs to the seconds-valued histogram, and reusing it
        // for a milliseconds-valued span attribute would make any uniform query wrong by a factor of 1000.
        internal const string TestCaseDurationMilliseconds = "test.case.duration_ms";
        internal const string TestCaseRetryAttempt = "test.case.retry.attempt";
        internal const string TestAssemblyName = "test.assembly.name";
        internal const string TestFrameworkName = "test.framework.name";
        internal const string TestFrameworkVersion = "test.framework.version";
        internal const string TestSessionId = "test.session.id";
        internal const string TestHostType = "test.host.type";
        internal const string TestRunExitCode = "test.run.exit_code";
        internal const string TestRunResultStatus = "test.run.result.status";
        internal const string TestRunTotalCount = "test.run.total";
        internal const string TestRunFailedCount = "test.run.failed";
        internal const string TestRunSkippedCount = "test.run.skipped";
        internal const string TestRunRequestType = "test.run.request_type";
        internal const string TestExtensionUid = "test.extension.uid";
        internal const string TestExtensionVersion = "test.extension.version";
        internal const string TestExtensionDisplayName = "test.extension.display_name";
        internal const string TestOutputStdout = "test.output.stdout";
        internal const string TestOutputStderr = "test.output.stderr";
        internal const string TestStepPrefix = "test.step.";
        internal const string TestMetadataPrefix = "test.metadata.";

        // ---- Legacy attribute names kept for backward compatibility ----
        // Emitted alongside the semantic-convention names so existing dashboards keep working.
        internal const string LegacyTestName = "test.name";
        internal const string LegacyTestId = "test.id";
        internal const string LegacyTestParentId = "test.parent.id";
        internal const string LegacyTestMethod = "test.method";
        internal const string LegacyTestClass = "test.class";
        internal const string LegacyTestNamespace = "test.namespace";
        internal const string LegacyTestAssembly = "test.assembly";
        internal const string LegacyTestFilePath = "test.file.path";
        internal const string LegacyTestLineStart = "test.line.start";
        internal const string LegacyTestLineEnd = "test.line.end";
        internal const string LegacyTestResult = "test.result";
        internal const string LegacyTestResultExplanation = "test.result.explanation";
        internal const string LegacyTestResultExceptionType = "test.result.exception.type";
        internal const string LegacyTestResultExceptionMessage = "test.result.exception.message";
        internal const string LegacyTestResultExceptionStackTrace = "test.result.exception.stacktrace";
        internal const string LegacyTestResultTimeout = "test.result.timeout.ms";
        internal const string LegacyTestDuration = "test.duration.ms";
        internal const string LegacyTestStdout = "test.stdout";
        internal const string LegacyTestStderr = "test.stderr";
        internal const string LegacyTestMetadataPrefix = "test.metadataProperty.";
    }

    /// <summary>
    /// Values for <see cref="Attributes.TestCaseResultStatus"/>.
    /// </summary>
    /// <remarks>
    /// Upstream defines exactly two well-known values, <c>pass</c> and <c>fail</c>. The remaining values are
    /// custom (the specification explicitly allows that) because collapsing "skipped", "timed out" and "errored"
    /// into "fail" would lose the distinction a test report is built on.
    /// </remarks>
    internal static class TestResultStatus
    {
        internal const string Pass = "pass";
        internal const string Fail = "fail";
        internal const string Skipped = "skipped";
        internal const string Error = "error";
        internal const string Timeout = "timeout";
        internal const string Cancelled = "cancelled";
        internal const string Unknown = "unknown";

        /// <summary>
        /// Maps a status onto the value the pre-4.x <c>test.result</c> attribute used, so legacy dashboards keep
        /// seeing the spellings they were built against.
        /// </summary>
        internal static string ToLegacy(string status)
            => status switch
            {
                Pass => "passed",
                Fail => "failed",
                _ => status,
            };
    }

    /// <summary>
    /// Instrument (metric) names. None of these are defined upstream: semantic conventions 1.43.0 has no
    /// <c>test.*</c> metrics.
    /// </summary>
    internal static class Metrics
    {
        internal const string TestCaseDuration = "test.case.duration";
        internal const string TestCaseResultCount = "test.case.result.count";
        internal const string TestRunDuration = "test.run.duration";
        internal const string TestRunActiveCases = "test.case.active";
        internal const string TestRetryCount = "test.case.retry.count";

        // Legacy instruments kept for backward compatibility.
        internal const string LegacyTestsDiscovered = "tests.discovered";
        internal const string LegacyTestsStarted = "tests.started";
        internal const string LegacyTestsCompleted = "tests.completed";
        internal const string LegacyTestsPassed = "tests.passed";
        internal const string LegacyTestsFailed = "tests.failed";
        internal const string LegacyTestsSkipped = "tests.skipped";
        internal const string LegacyTestsUnknown = "tests.unknown";
        internal const string LegacyTestsDuration = "tests.duration";
    }

    /// <summary>
    /// Instrument units, following UCUM as required by OpenTelemetry.
    /// </summary>
    internal static class Units
    {
        internal const string Seconds = "s";
        internal const string Count = "{test}";
    }

    /// <summary>
    /// Span/activity names used by the platform.
    /// </summary>
    internal static class Activities
    {
        internal const string TestHostBuilder = "TestHostBuilder";
        internal const string TestFramework = "TestFramework";
    }
}
