// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

/// <summary>
/// Well-known attribute and instrument names emitted by Microsoft.Testing.Platform.
/// </summary>
/// <remarks>
/// Names follow the OpenTelemetry semantic conventions for tests (<c>test.*</c>), code
/// (<c>code.*</c>), errors (<c>error.*</c>), CI/CD (<c>cicd.*</c>) and VCS (<c>vcs.*</c>).
/// Some of those conventions are still in "development" status upstream; we keep them in one
/// place so a rename upstream is a single-file change here.
/// </remarks>
internal static class TestingPlatformSemanticConventions
{
    /// <summary>
    /// Attribute names.
    /// </summary>
    internal static class Attributes
    {
        // ---- test.* (OTel semantic conventions for testing) ----
        internal const string TestCaseName = "test.case.name";
        internal const string TestCaseResultStatus = "test.case.result.status";
        internal const string TestSuiteName = "test.suite.name";
        internal const string TestRunId = "test.run.id";

        // ---- code.* ----
        internal const string CodeFunctionName = "code.function.name";
        internal const string CodeNamespace = "code.namespace";
        internal const string CodeFilePath = "code.file.path";
        internal const string CodeLineNumber = "code.line.number";
        internal const string CodeColumnNumber = "code.column.number";
        internal const string CodeStacktrace = "code.stacktrace";

        // ---- error.* ----
        internal const string ErrorType = "error.type";
        internal const string ErrorMessage = "error.message";

        // ---- Microsoft.Testing.Platform specific ----
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
        internal const string TestArtifactPath = "test.artifact.path";
        internal const string TestArtifactKind = "test.artifact.kind";
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
    internal static class TestResultStatus
    {
        internal const string Passed = "passed";
        internal const string Failed = "failed";
        internal const string Skipped = "skipped";
        internal const string Error = "error";
        internal const string Timeout = "timeout";
        internal const string Cancelled = "cancelled";
        internal const string Unknown = "unknown";
    }

    /// <summary>
    /// Instrument (metric) names.
    /// </summary>
    internal static class Metrics
    {
        // Semantic-convention aligned instruments.
        internal const string TestCaseDuration = "test.case.duration";
        internal const string TestCaseResultCount = "test.case.result.count";
        internal const string TestRunDuration = "test.run.duration";
        internal const string TestRunActiveCases = "test.case.active";
        internal const string TestDiscoveryDuration = "test.discovery.duration";
        internal const string TestArtifactCount = "test.artifact.count";
        internal const string TestExtensionDuration = "test.extension.duration";
        internal const string TestRetryCount = "test.case.retry.count";
        internal const string MessageBusMessageCount = "test.messagebus.message.count";

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
        internal const string Milliseconds = "ms";
        internal const string Count = "{test}";
        internal const string Artifacts = "{artifact}";
        internal const string Messages = "{message}";
    }

    /// <summary>
    /// Span/activity names used by the platform.
    /// </summary>
    internal static class Activities
    {
        internal const string TestHostBuilder = "TestHostBuilder";
        internal const string TestFramework = "TestFramework";
        internal const string Discovery = "Discovery";
        internal const string Run = "Run";
    }
}
