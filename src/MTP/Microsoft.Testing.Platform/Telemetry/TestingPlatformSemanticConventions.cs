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
