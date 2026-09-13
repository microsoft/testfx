// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class AssertionFailureDiagnosticsTests : TestContainer
{
    public void CaptureShouldAttachBoundedContextForFailedTest()
    {
        EnableCapture();
        using TempDirectoryScope resultsDirectory = new();
        using TestContextImplementation context = CreateContext("Tests.SampleTests", "Fails", "Fails (42)", resultsDirectory.Path);
        using IDisposable? activeScope = context.StartAssertionFailureDiagnosticsScope();
        using IDisposable currentContext = TestContextImplementation.SetCurrentTestContext(context);

        TestContextImplementation.CaptureAssertionFailureDiagnostics(
            "Assertion failed. Values differ.",
            expected: "42",
            actual: "41");
        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);

        string artifactPath = context.GetResultFiles().Should().ContainSingle().Which;
        string json = File.ReadAllText(artifactPath);

        json.Should().Contain("\"schemaVersion\":1");
        json.Should().Contain("\"fullyQualifiedName\":\"Tests.SampleTests.Fails\"");
        json.Should().Contain("\"displayName\":\"Fails (42)\"");
        json.Should().Contain("\"expected\":\"42\"");
        json.Should().Contain("\"actual\":\"41\"");
        json.Should().Contain("\"activeTests\"");
        json.Should().Contain("\"cpuPercentDuringTest\"");
        json.Should().Contain("\"workingSetBytes\"");
        json.Should().Contain("\"processIoAvailable\"");
        json.Should().Contain("\"outputVolumeAvailableFreeBytes\"");
        json.Should().Contain("\"stackFrames\"");
    }

    public void CaptureShouldListTestsRunningInParallel()
    {
        EnableCapture();
        using TempDirectoryScope resultsDirectory = new();
        using TestContextImplementation firstContext = CreateContext("Tests.FirstTests", "Runs", "first", resultsDirectory.Path);
        using TestContextImplementation secondContext = CreateContext("Tests.SecondTests", "Fails", "second", resultsDirectory.Path);
        using IDisposable? firstActiveScope = firstContext.StartAssertionFailureDiagnosticsScope();
        using IDisposable? secondActiveScope = secondContext.StartAssertionFailureDiagnosticsScope();
        using IDisposable currentContext = TestContextImplementation.SetCurrentTestContext(secondContext);

        TestContextImplementation.CaptureAssertionFailureDiagnostics("Assertion failed.", expected: null, actual: null);
        secondContext.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);

        string artifactPath = secondContext.GetResultFiles().Should().ContainSingle().Which;
        string json = File.ReadAllText(artifactPath);

        json.Should().Contain("\"fullyQualifiedName\":\"Tests.FirstTests.Runs\"");
        json.Should().Contain("\"fullyQualifiedName\":\"Tests.SecondTests.Fails\"");
        json.Should().Contain("\"isFailingTest\":true");
        json.Should().Contain("\"isFailingTest\":false");
    }

    public void CaptureShouldBeDiscardedWhenTestPasses()
    {
        EnableCapture();
        using TempDirectoryScope resultsDirectory = new();
        using TestContextImplementation context = CreateContext("Tests.SampleTests", "CatchesFailure", null, resultsDirectory.Path);
        using IDisposable? activeScope = context.StartAssertionFailureDiagnosticsScope();
        using IDisposable currentContext = TestContextImplementation.SetCurrentTestContext(context);

        TestContextImplementation.CaptureAssertionFailureDiagnostics("Assertion failed.", expected: null, actual: null);
        string artifactPath = Directory.GetFiles(context.TestTempDirectory!, "mstest-assertion-failure-state-attempt-1-invocation-*-capture-1.json").Should().ContainSingle().Which;
        File.Exists(artifactPath).Should().BeTrue();

        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Passed);

        context.GetResultFiles().Should().BeNull();
        File.Exists(artifactPath).Should().BeFalse();
    }

    public void CaptureShouldBeRetainedForNonPassingOutcome()
    {
        EnableCapture();
        using TempDirectoryScope resultsDirectory = new();
        using TestContextImplementation context = CreateContext("Tests.SampleTests", "TimesOut", null, resultsDirectory.Path);
        using IDisposable? activeScope = context.StartAssertionFailureDiagnosticsScope();
        using IDisposable currentContext = TestContextImplementation.SetCurrentTestContext(context);

        TestContextImplementation.CaptureAssertionFailureDiagnostics("Assertion failed.", expected: null, actual: null);
        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Timeout);

        string artifactPath = context.GetResultFiles().Should().ContainSingle().Which;
        File.Exists(artifactPath).Should().BeTrue();
    }

    public void CaptureShouldLimitAssertionFailuresPerAttempt()
    {
        EnableCapture();
        using TempDirectoryScope resultsDirectory = new();
        using TestContextImplementation context = CreateContext("Tests.SampleTests", "MultipleFailures", null, resultsDirectory.Path);
        using IDisposable? activeScope = context.StartAssertionFailureDiagnosticsScope();
        using IDisposable currentContext = TestContextImplementation.SetCurrentTestContext(context);

        TestContextImplementation.CaptureAssertionFailureDiagnostics("first failure", expected: "one", actual: "two");
        TestContextImplementation.CaptureAssertionFailureDiagnostics("second failure", expected: "three", actual: "four");
        TestContextImplementation.CaptureAssertionFailureDiagnostics("third failure", expected: "five", actual: "six");
        TestContextImplementation.CaptureAssertionFailureDiagnostics("fourth failure", expected: "seven", actual: "eight");
        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);

        IList<string>? artifactPaths = context.GetResultFiles();
        artifactPaths.Should().HaveCount(3);
        string combinedJson = string.Join(Environment.NewLine, artifactPaths!.Select(File.ReadAllText));

        combinedJson.Should().Contain("first failure");
        combinedJson.Should().Contain("second failure");
        combinedJson.Should().Contain("third failure");
        combinedJson.Should().NotContain("fourth failure");
    }

    public void CaptureShouldUseDistinctArtifactsForRepeatedInvocations()
    {
        EnableCapture();
        using TempDirectoryScope resultsDirectory = new();
        using TestContextImplementation context = CreateContext("Tests.SampleTests", "Retries", null, resultsDirectory.Path);

        using (context.StartAssertionFailureDiagnosticsScope())
        {
            TestContextImplementation.CaptureAssertionFailureDiagnostics("first attempt", expected: null, actual: null);
            context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);
        }

        string firstArtifactPath = context.GetResultFiles().Should().ContainSingle().Which;

        using (context.StartAssertionFailureDiagnosticsScope())
        {
            TestContextImplementation.CaptureAssertionFailureDiagnostics("second attempt", expected: null, actual: null);
            context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);
        }

        string secondArtifactPath = context.GetResultFiles().Should().ContainSingle().Which;

        firstArtifactPath.Should().NotBe(secondArtifactPath);
        File.ReadAllText(firstArtifactPath).Should().Contain("first attempt");
        File.ReadAllText(secondArtifactPath).Should().Contain("second attempt");
    }

    public void FailedRetryArtifactShouldSurvivePassingFinalAttemptCleanup()
    {
        EnableCapture();
        using TempDirectoryScope resultsDirectory = new();
        using TestContextImplementation context = CreateContext("Tests.SampleTests", "Flaky", null, resultsDirectory.Path);

        using (context.StartAssertionFailureDiagnosticsScope())
        {
            TestContextImplementation.CaptureAssertionFailureDiagnostics("failed attempt", expected: null, actual: null);
            context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);
        }

        string failedAttemptArtifact = context.GetResultFiles().Should().ContainSingle().Which;

        context.Context.TestRunCount = 2;
        context.SetOutcome(UnitTestOutcome.Passed);
        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Passed);
        context.Dispose();

        File.Exists(failedAttemptArtifact).Should().BeTrue();
    }

    public void EmptyIterationShouldTransferUnscopedDiagnosticsToOuterResult()
    {
        EnableCapture();
        using TempDirectoryScope resultsDirectory = new();
        using TestContextImplementation outerContext = CreateContext("Tests.SampleTests", "Rows", null, resultsDirectory.Path);
        using TestContextImplementation iterationContext = outerContext.CloneForDataDrivenIteration();
        using (TestContextImplementation.SetCurrentTestContext(iterationContext))
        {
            TestContextImplementation.CaptureAssertionFailureDiagnostics("row returned no result", expected: null, actual: null);
        }

        iterationContext.TransferAssertionFailureDiagnosticsTo(outerContext);
        TestResult[] results =
        [
            new()
            {
                Outcome = UnitTestOutcome.Unknown,
                TestFailureException = new InvalidOperationException("No result."),
            },
        ];

        outerContext.FinalizeAssertionFailureDiagnosticsExecution(results);

        string artifactPath = results[0].ResultFiles.Should().ContainSingle().Which;
        File.ReadAllText(artifactPath).Should().Contain("row returned no result");
    }

    public void CaptureShouldBoundUserControlledStringsAndPreserveSurrogatePairs()
    {
        EnableCapture();
        using TempDirectoryScope resultsDirectory = new();
        string longValue = string.Concat(new string('a', 20_000), char.ConvertFromUtf32(0x1F600), new string('b', 20_000));
        string malformedValue = string.Concat(longValue, "\uD800");
        using TestContextImplementation context = CreateContext(longValue, longValue, longValue, resultsDirectory.Path);
        using IDisposable? activeScope = context.StartAssertionFailureDiagnosticsScope();

        TestContextImplementation.CaptureAssertionFailureDiagnostics(longValue, malformedValue, longValue);
        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);

        string artifactPath = context.GetResultFiles().Should().ContainSingle().Which;
        new FileInfo(artifactPath).Length.Should().BeLessThan(8 * 1024 * 1024);
        using var artifact = JsonDocument.Parse(File.ReadAllText(artifactPath));
        artifact.RootElement.GetProperty("assertion").GetProperty("expected").GetString().Should().NotContain("\uD800");

        MethodInfo truncateMethod = typeof(TestContextImplementation).GetMethod("Truncate", BindingFlags.NonPublic | BindingFlags.Static)!;
        const string suffix = "... <truncated>";
        int maximumLength = suffix.Length + 2;
        string splitBoundary = string.Concat("a", char.ConvertFromUtf32(0x1F600), new string('b', 100));
        string truncated = (string)truncateMethod.Invoke(null, [splitBoundary, maximumLength])!;
        string malformed = (string)truncateMethod.Invoke(null, ["\uD800", maximumLength])!;

        truncated.Length.Should().BeLessThanOrEqualTo(maximumLength);
        char.IsHighSurrogate(truncated[truncated.Length - suffix.Length - 1]).Should().BeFalse();
        malformed.Should().Be("\uFFFD");
        Action encode = () => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetBytes(string.Concat(truncated, malformed));
        encode.Should().NotThrow();
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            MSTestSettings.Reset();
            base.Dispose(disposing);
        }
    }

    private static void EnableCapture()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <CaptureAssertionFailureDiagnostics>true</CaptureAssertionFailureDiagnostics>
              </MSTest>
            </RunSettings>
            """;

        MSTestSettings settings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, logger: null)!;
        MSTestSettings.PopulateSettings(settings);
    }

    private static TestContextImplementation CreateContext(string className, string methodName, string? displayName, string resultsDirectory)
    {
        var testMethod = new Mock<ITestMethod>();
        testMethod.SetupGet(method => method.FullClassName).Returns(className);
        testMethod.SetupGet(method => method.Name).Returns(methodName);

        var context = new TestContextImplementation(
            testMethod.Object,
            className,
            new Dictionary<string, object?>
            {
                ["TestResultsDirectory"] = resultsDirectory,
            },
            messageLogger: null,
            testRunCancellationToken: null);
        context.SetDisplayName(displayName);
        context.Context.TestRunCount = 1;
        return context;
    }

    private sealed class TempDirectoryScope : IDisposable
    {
        public TempDirectoryScope()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MSTestAssertionFailureDiagnostics", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
#endif
