// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
#if NETFRAMEWORK
using System.Security;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Security.Principal;
#endif

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class AssertionFailureDiagnosticsContinuationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousPlatform = PlatformServiceProvider.Instance;
    private readonly TestContext? _previousContext = TestContext.Current;
    private readonly TestablePlatformServiceProvider _platform = new();
    private readonly List<(string Format, object?[] Arguments)> _warnings = [];

    public AssertionFailureDiagnosticsContinuationTests()
    {
        PlatformServiceProvider.Instance = _platform;
        _platform.MockTraceLogger
            .Setup(logger => logger.Warning(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) => _warnings.Add((format, [.. arguments])));
        EnableCapture();
    }

    public void DisabledAndUnboundCapturesDoNotCreateArtifacts()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.Disabled", "Runs", null, directory.Path);
        MSTestSettings.Reset();
        using (TestContextImplementation.SetCurrentTestContext(context))
        {
            context.StartAssertionFailureDiagnosticsScope().Should().BeNull();
            TestContextImplementation.CaptureAssertionFailureDiagnostics("disabled", "expected", "actual");
        }

        EnableCapture();
        using (TestContextImplementation.SetCurrentTestContext(null))
        {
            TestContextImplementation.CaptureAssertionFailureDiagnostics("unbound", null, null);
            context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);
        }

        Directory.GetFileSystemEntries(directory.Path).Should().BeEmpty();
        context.GetResultFiles().Should().BeNull();
        _warnings.Should().BeEmpty();
    }

    public void FixtureCaptureFailureIsReportedWithoutLeakingAnActiveTest()
    {
        using DiagnosticsDirectory directory = new();
        using var fixture = new TestContextImplementation(
            null, "Tests.Fixtures",
            new Dictionary<string, object?> { ["TestName"] = "Initialize", ["TestResultsDirectory"] = directory.Path },
            null, null);
        using (TestContextImplementation.SetCurrentTestContext(fixture))
        {
            TestContextImplementation.CaptureAssertionFailureDiagnostics("fixture failure", "blue", "red");
        }

        fixture.GetResultFiles().Should().BeNull();
        Directory.GetFileSystemEntries(directory.Path).Should().BeEmpty();
        _warnings.Should().ContainSingle();
        _warnings[0].Format.Should().Be("Failed to capture assertion failure diagnostics outside a test method invocation for test '{0}.{1}': {2}");
        _warnings[0].Arguments.Take(2).Should().Equal("Tests.Fixtures", "Initialize");
        _warnings[0].Arguments[2].Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("A per-test temporary directory is required to capture assertion failure diagnostics.");

        using TestContextImplementation test = CreateContext("Tests.AfterFixture", "Runs", null, directory.Path);
        using IDisposable? scope = test.StartAssertionFailureDiagnosticsScope();
        using JsonDocument artifact = CaptureArtifact(test, "real test", null, null);
        artifact.RootElement.GetProperty("activeTests").EnumerateArray()
            .Select(value => value.GetProperty("fullyQualifiedName").GetString()).Should().Equal("Tests.AfterFixture.Runs");
    }

    public void WrongContextFinalizationDoesNotConsumeAnotherTestsArtifacts()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation owner = CreateContext("Tests.Owner", "Runs", null, directory.Path);
        using TestContextImplementation other = CreateContext("Tests.Other", "Runs", null, directory.Path);
        using IDisposable? scope = owner.StartAssertionFailureDiagnosticsScope();
        TestContextImplementation.CaptureAssertionFailureDiagnostics("owned capture", "one", "two");

        other.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Passed);
        other.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);

        other.GetResultFiles().Should().BeNull();
        owner.GetResultFiles().Should().BeNull();
        string path = Directory.GetFiles(owner.TestTempDirectory!, "*.json").Should().ContainSingle().Which;
        owner.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);
        owner.GetResultFiles().Should().Equal(path);
        using var artifact = JsonDocument.Parse(File.ReadAllText(path));
        artifact.RootElement.GetProperty("assertion").GetProperty("message").GetString().Should().Be("owned capture");
        _warnings.Should().BeEmpty();
    }

    public void FailedArtifactWritesAreLoggedAndConsumeOnlyTheirCaptureSlot()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.WriteFailure", "Runs", null, directory.Path);
        using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
        TestContextImplementation.CaptureAssertionFailureDiagnostics("first capture", "one", "two");
        string first = Directory.GetFiles(context.TestTempDirectory!, "*.json").Should().ContainSingle().Which;
        first.Should().EndWith("-capture-1.json");
        string blocked = first.Replace("-capture-1.json", "-capture-2.json");
        Directory.CreateDirectory(blocked);

        TestContextImplementation.CaptureAssertionFailureDiagnostics("blocked second", null, null);
        TestContextImplementation.CaptureAssertionFailureDiagnostics("third capture", "three", "four");
        TestContextImplementation.CaptureAssertionFailureDiagnostics("over budget fourth", null, null);
        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);

        string third = first.Replace("-capture-1.json", "-capture-3.json");
        context.GetResultFiles().Should().Equal(first, third);
        Directory.GetFiles(context.TestTempDirectory!, "*.json").Should().BeEquivalentTo(new[] { first, third });
        Directory.Exists(blocked).Should().BeTrue();
        using var firstArtifact = JsonDocument.Parse(File.ReadAllText(first));
        using var thirdArtifact = JsonDocument.Parse(File.ReadAllText(third));
        firstArtifact.RootElement.GetProperty("captureIndex").GetInt32().Should().Be(1);
        thirdArtifact.RootElement.GetProperty("captureIndex").GetInt32().Should().Be(3);
        thirdArtifact.RootElement.GetProperty("assertion").GetProperty("message").GetString().Should().Be("third capture");
        _warnings.Should().HaveCount(2);
        _warnings[0].Format.Should().Be("Failed to delete incomplete assertion failure diagnostics artifact '{0}': {1}");
        _warnings[0].Arguments[0].Should().Be(blocked);
        _warnings[0].Arguments[1].Should().Match<Exception>(exception => exception is IOException || exception is UnauthorizedAccessException);
        _warnings[1].Format.Should().Be("Failed to capture assertion failure diagnostics for test '{0}': {1}");
        _warnings[1].Arguments[0].Should().Be("Tests.WriteFailure.Runs");
        _warnings[1].Arguments[1].Should().BeOfType<UnauthorizedAccessException>();
        _warnings[1].Arguments[1].Should().NotBeSameAs(_warnings[0].Arguments[1]);
    }

    public void PassingFinalizationReportsDeleteFailuresWithoutAttachingArtifacts()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.Passing", "Runs", null, directory.Path);
        using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
        TestContextImplementation.CaptureAssertionFailureDiagnostics("caught failure", null, null);
        string path = ReplaceArtifactWithDirectory(context);

        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Passed);
        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);

        context.GetResultFiles().Should().BeNull();
        Directory.Exists(path).Should().BeTrue();
        _warnings.Should().ContainSingle();
        _warnings[0].Format.Should().Be("Failed to delete assertion failure diagnostics artifact '{0}' for a non-failing test: {1}");
        _warnings[0].Arguments[0].Should().Be(path);
        _warnings[0].Arguments[1].Should().Match<Exception>(exception => exception is IOException || exception is UnauthorizedAccessException);
    }

    public void PassingExecutionCleanupFailureStillRemovesOnlyDiagnosticsAttachments()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.PassingExecution", "Runs", null, directory.Path);
        using (TestContextImplementation.SetCurrentTestContext(context))
        {
            TestContextImplementation.CaptureAssertionFailureDiagnostics("caught outside method", null, null);
        }

        string path = ReplaceArtifactWithDirectory(context);
        string ordinary = Path.Combine(directory.Path, "ordinary.txt");
        File.WriteAllText(ordinary, "ordinary attachment bytes");
        TestResult[] results = [new() { Outcome = UnitTestOutcome.Passed, ResultFiles = new[] { path, ordinary } }];

        context.FinalizeAssertionFailureDiagnosticsExecution(results, resetCaptureBudget: true);
        context.FinalizeAssertionFailureDiagnosticsExecution(results, resetCaptureBudget: true);

        results[0].ResultFiles.Should().Equal(ordinary);
        File.ReadAllText(ordinary).Should().Be("ordinary attachment bytes");
        Directory.Exists(path).Should().BeTrue();
        _warnings.Should().ContainSingle();
        _warnings[0].Format.Should().Be("Failed to delete assertion failure diagnostics artifact '{0}' ({1}): {2}");
        _warnings[0].Arguments.Take(2).Should().Equal(path, "completed diagnostics for a passing test");
        _warnings[0].Arguments[2].Should().Match<Exception>(exception => exception is IOException || exception is UnauthorizedAccessException);
    }

    public void ThrowingDiagnosticLoggerCannotEscapeCaptureOrCleanup()
    {
        var loggerFailure = new InvalidOperationException("logger failure must not escape diagnostics");
        _platform.MockTraceLogger
            .Setup(logger => logger.Warning(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) => _warnings.Add((format, [.. arguments])))
            .Throws(loggerFailure);
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.LoggerFailure", "Runs", null, directory.Path);
        using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
        TestContextImplementation.CaptureAssertionFailureDiagnostics("first capture", null, null);
        string first = Directory.GetFiles(context.TestTempDirectory!, "*.json").Should().ContainSingle().Which;
        Directory.CreateDirectory(first.Replace("-capture-1.json", "-capture-2.json"));
        Action capture = () => TestContextImplementation.CaptureAssertionFailureDiagnostics("unwritable capture", null, null);

        capture.Should().NotThrow();
        _warnings.Should().HaveCount(2);
        _warnings[1].Arguments[1].Should().BeOfType<UnauthorizedAccessException>().And.NotBeSameAs(loggerFailure);
        File.Delete(first);
        Directory.CreateDirectory(first);
        Action dispose = () => scope!.Dispose();

        dispose.Should().NotThrow();
        _warnings.Should().HaveCount(3);
        _warnings[2].Format.Should().Be("Failed to delete unfinalized assertion failure diagnostics artifact '{0}': {1}");
        _warnings[2].Arguments[0].Should().Be(first);
        context.GetResultFiles().Should().BeNull();
    }

    public void NestedScopeDisposalRestoresPreviousCaptureAndIsIdempotent()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation outer = CreateContext("Tests.Outer", "Runs", null, directory.Path);
        using TestContextImplementation inner = CreateContext("Tests.Inner", "Runs", null, directory.Path);
        using IDisposable? outerScope = outer.StartAssertionFailureDiagnosticsScope();
        using IDisposable? innerScope = inner.StartAssertionFailureDiagnosticsScope();
        TestContextImplementation.CaptureAssertionFailureDiagnostics("unfinalized inner", null, null);
        string innerArtifact = Directory.GetFiles(inner.TestTempDirectory!, "*.json").Should().ContainSingle().Which;

        innerScope!.Dispose();
        innerScope.Dispose();

        File.Exists(innerArtifact).Should().BeFalse();
        inner.GetResultFiles().Should().BeNull();
        using JsonDocument artifact = CaptureArtifact(outer, "restored outer", "blue", "red");
        artifact.RootElement.GetProperty("test").GetProperty("fullyQualifiedName").GetString().Should().Be("Tests.Outer.Runs");
        artifact.RootElement.GetProperty("assertion").GetProperty("message").GetString().Should().Be("restored outer");
        artifact.RootElement.GetProperty("activeTests").EnumerateArray()
            .Select(value => value.GetProperty("fullyQualifiedName").GetString()).Should().Equal("Tests.Outer.Runs");
        _warnings.Should().BeEmpty();
    }

    public void UnfinalizedArtifactCleanupFailureDoesNotLeakActiveRegistration()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation outer = CreateContext("Tests.AfterCleanup", "Runs", null, directory.Path);
        using TestContextImplementation inner = CreateContext("Tests.Unfinalized", "Runs", null, directory.Path);
        using IDisposable? outerScope = outer.StartAssertionFailureDiagnosticsScope();
        using IDisposable? innerScope = inner.StartAssertionFailureDiagnosticsScope();
        TestContextImplementation.CaptureAssertionFailureDiagnostics("unfinalized", null, null);
        string path = ReplaceArtifactWithDirectory(inner);

        innerScope!.Dispose();
        innerScope.Dispose();

        Directory.Exists(path).Should().BeTrue();
        _warnings.Should().ContainSingle();
        _warnings[0].Format.Should().Be("Failed to delete unfinalized assertion failure diagnostics artifact '{0}': {1}");
        _warnings[0].Arguments[0].Should().Be(path);
        _warnings[0].Arguments[1].Should().Match<Exception>(exception => exception is IOException || exception is UnauthorizedAccessException);
        using JsonDocument artifact = CaptureArtifact(outer, "after cleanup", null, null);
        artifact.RootElement.GetProperty("activeTests").EnumerateArray()
            .Select(value => value.GetProperty("fullyQualifiedName").GetString()).Should().Equal("Tests.AfterCleanup.Runs");
    }

    public void ClosedScopeIgnoresFurtherCapturesUntilANewInvocationStarts()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.Closed", "Runs", null, directory.Path);
        using (context.StartAssertionFailureDiagnosticsScope())
        {
            using JsonDocument first = CaptureArtifact(context, "first invocation", null, null);
            first.RootElement.GetProperty("captureIndex").GetInt32().Should().Be(1);
            TestContextImplementation.CaptureAssertionFailureDiagnostics("ignored after close", null, null);
            context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);
            context.GetResultFiles().Should().BeNull();
            Directory.GetFiles(context.TestTempDirectory!, "*.json").Should().ContainSingle();
        }

        using (context.StartAssertionFailureDiagnosticsScope())
        {
            using JsonDocument second = CaptureArtifact(context, "next invocation", null, null);
            second.RootElement.GetProperty("captureIndex").GetInt32().Should().Be(2);
            second.RootElement.GetProperty("assertion").GetProperty("message").GetString().Should().Be("next invocation");
        }
    }

    public void SerializedArtifactPreservesIdentityAssertionAndProcessContracts()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");
            using DiagnosticsDirectory directory = new();
            using TestContextImplementation context = CreateContext("Tests.Serialization", "Report", "Report (blue)", directory.Path, attempt: 4);
            using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
            using JsonDocument artifact = CaptureArtifact(context, "Expected a blue status.", null, "line 1\nline 2\0");
            JsonElement root = artifact.RootElement;

            root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
            root.GetProperty("captureIndex").GetInt32().Should().Be(1);
            root.GetProperty("assertion").GetProperty("message").GetString().Should().Be("Expected a blue status.");
            root.GetProperty("assertion").TryGetProperty("expected", out _).Should().BeFalse();
            root.GetProperty("assertion").GetProperty("actual").GetString().Should().Be("line 1\nline 2\0");
            root.GetProperty("test").GetProperty("fullyQualifiedName").GetString().Should().Be("Tests.Serialization.Report");
            root.GetProperty("test").GetProperty("displayName").GetString().Should().Be("Report (blue)");
            root.GetProperty("test").GetProperty("attempt").GetInt32().Should().Be(4);
            root.GetProperty("thread").GetProperty("managedThreadId").GetInt32().Should().Be(Environment.CurrentManagedThreadId);
            JsonElement[] active = root.GetProperty("activeTests").EnumerateArray().ToArray();
            active.Should().ContainSingle();
            active[0].GetProperty("fullyQualifiedName").GetString().Should().Be("Tests.Serialization.Report");
            active[0].GetProperty("displayName").GetString().Should().Be("Report (blue)");
            active[0].GetProperty("attempt").GetInt32().Should().Be(4);
            active[0].GetProperty("isFailingTest").GetBoolean().Should().BeTrue();
            root.GetProperty("activeTestsTruncated").GetBoolean().Should().BeFalse();

            JsonElement process = root.GetProperty("process");
            using var currentProcess = Process.GetCurrentProcess();
            process.GetProperty("processId").GetInt32().Should().Be(currentProcess.Id);
            process.GetProperty("processName").GetString().Should().Be(currentProcess.ProcessName);
            process.GetProperty("currentCulture").GetString().Should().Be("fr-FR");
            process.GetProperty("currentUICulture").GetString().Should().Be("ja-JP");
            process.GetProperty("processorCount").GetInt32().Should().Be(Environment.ProcessorCount);
            process.GetProperty("managedHeapBytes").GetInt64().Should().BeGreaterThan(0);
            process.GetProperty("workingSetBytes").GetInt64().Should().BeGreaterThan(0);
#if NETCOREAPP
            process.GetProperty("frameworkDescription").GetString().Should().Be(RuntimeInformation.FrameworkDescription);
            process.GetProperty("operatingSystemDescription").GetString().Should().Be(RuntimeInformation.OSDescription);
            process.GetProperty("processArchitecture").GetString().Should().Be(RuntimeInformation.ProcessArchitecture.ToString());
            process.GetProperty("gcMemoryLoadBytes").GetInt64().Should().BeGreaterThanOrEqualTo(0);
            process.GetProperty("gcTotalAvailableMemoryBytes").GetInt64().Should().BeGreaterThan(0);
#else
            process.GetProperty("frameworkDescription").GetString().Should().Be(RuntimeEnvironment.GetSystemVersion());
            process.GetProperty("operatingSystemDescription").GetString().Should().Be(Environment.OSVersion.VersionString);
            process.GetProperty("processArchitecture").GetString().Should().Be(IntPtr.Size == 8 ? "X64" : "X86");
            process.TryGetProperty("gcMemoryLoadBytes", out _).Should().BeFalse();
            process.TryGetProperty("gcTotalAvailableMemoryBytes", out _).Should().BeFalse();
#endif
            bool ioAvailable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || File.Exists("/proc/self/io");
            process.GetProperty("processIoAvailable").GetBoolean().Should().Be(ioAvailable);
            if (ioAvailable)
            {
                process.GetProperty("processIoReadBytesDuringTest").GetInt64().Should().BeGreaterThanOrEqualTo(0);
                process.GetProperty("processIoWriteBytesDuringTest").GetInt64().Should().BeGreaterThanOrEqualTo(0);
            }
            else
            {
                process.TryGetProperty("processIoReadBytesDuringTest", out _).Should().BeFalse();
                process.TryGetProperty("processIoWriteBytesDuringTest", out _).Should().BeFalse();
            }

            process.TryGetProperty("outputVolumeError", out _).Should().BeFalse();
            process.GetProperty("outputVolumePath").GetString().Should().Be(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Path.GetPathRoot(context.TestTempDirectory)
                    : new DriveInfo(context.TestTempDirectory!).Name);
            long totalBytes = process.GetProperty("outputVolumeTotalBytes").GetInt64();
            totalBytes.Should().BeGreaterThan(0);
            process.GetProperty("outputVolumeAvailableFreeBytes").GetInt64().Should().BeInRange(0, totalBytes);
            JsonElement[] frames = root.GetProperty("stackFrames").EnumerateArray().ToArray();
            frames.Should().Contain(frame => frame.GetProperty("method").GetString()!.EndsWith(".CaptureAssertionFailureDiagnostics", StringComparison.Ordinal)
                && frame.GetProperty("isFrameworkFrame").GetBoolean());
            frames.Should().Contain(frame => frame.GetProperty("method").GetString() == typeof(AssertionFailureDiagnosticsContinuationTests).FullName + "." + nameof(SerializedArtifactPreservesIdentityAssertionAndProcessContracts)
                && !frame.GetProperty("isFrameworkFrame").GetBoolean());
            _warnings.Should().BeEmpty();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    public void ActiveSnapshotOrdersFailingThenOrdinalIdentitiesAndInvocationTieBreak()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation beta = CreateContext("Tests.Beta", "Runs", "zeta", directory.Path, 5);
        using TestContextImplementation omega = CreateContext("Tests.Alpha", "Runs", "omega", directory.Path, 2);
        using TestContextImplementation alphaFirst = CreateContext("Tests.Alpha", "Runs", "alpha", directory.Path, 3);
        using TestContextImplementation alphaSecond = CreateContext("Tests.Alpha", "Runs", "alpha", directory.Path, 4);
        using TestContextImplementation failing = CreateContext("Tests.Zulu", "Runs", "failing", directory.Path, 7);
        using IDisposable? betaScope = beta.StartAssertionFailureDiagnosticsScope();
        using IDisposable? omegaScope = omega.StartAssertionFailureDiagnosticsScope();
        using IDisposable? alphaFirstScope = alphaFirst.StartAssertionFailureDiagnosticsScope();
        using IDisposable? alphaSecondScope = alphaSecond.StartAssertionFailureDiagnosticsScope();
        using IDisposable? failingScope = failing.StartAssertionFailureDiagnosticsScope();

        using JsonDocument artifact = CaptureArtifact(failing, "ordering", null, null);

        JsonElement[] active = artifact.RootElement.GetProperty("activeTests").EnumerateArray().ToArray();
        active.Select(value => value.GetProperty("fullyQualifiedName").GetString())
            .Should().Equal("Tests.Zulu.Runs", "Tests.Alpha.Runs", "Tests.Alpha.Runs", "Tests.Alpha.Runs", "Tests.Beta.Runs");
        active.Select(value => value.GetProperty("displayName").GetString())
            .Should().Equal("failing", "alpha", "alpha", "omega", "zeta");
        active.Select(value => value.GetProperty("attempt").GetInt32()).Should().Equal(7, 3, 4, 2, 5);
        active.Select(value => value.GetProperty("isFailingTest").GetBoolean()).Should().Equal(true, false, false, false, false);
        artifact.RootElement.GetProperty("activeTestsTruncated").GetBoolean().Should().BeFalse();
    }

    public void ActiveSnapshotCapsAt128AndKeepsTheFailingTestFirst()
    {
        using DiagnosticsDirectory directory = new();
        var contexts = new List<TestContextImplementation>();
        var scopes = new List<IDisposable>();
        try
        {
            for (int index = 128; index >= 0; index--)
            {
                TestContextImplementation context = CreateContext("Tests.Background", "Runs", $"worker-{index:D3}", directory.Path);
                contexts.Add(context);
                scopes.Add(context.StartAssertionFailureDiagnosticsScope()!);
            }

            using TestContextImplementation failing = CreateContext("Tests.ZFailing", "Runs", "failing", directory.Path);
            using IDisposable? failingScope = failing.StartAssertionFailureDiagnosticsScope();
            using JsonDocument artifact = CaptureArtifact(failing, "bounded snapshot", null, null);

            JsonElement[] active = artifact.RootElement.GetProperty("activeTests").EnumerateArray().ToArray();
            active.Should().HaveCount(128);
            active[0].GetProperty("fullyQualifiedName").GetString().Should().Be("Tests.ZFailing.Runs");
            active[0].GetProperty("isFailingTest").GetBoolean().Should().BeTrue();
            active.Skip(1).Select(value => value.GetProperty("displayName").GetString())
                .Should().Equal(Enumerable.Range(0, 127).Select(index => $"worker-{index:D3}"));
            active.Skip(1).Select(value => value.GetProperty("isFailingTest").GetBoolean()).Should().OnlyContain(value => !value);
            artifact.RootElement.GetProperty("activeTestsTruncated").GetBoolean().Should().BeTrue();
        }
        finally
        {
            for (int index = scopes.Count - 1; index >= 0; index--)
            {
                scopes[index].Dispose();
            }

            foreach (TestContextImplementation context in contexts)
            {
                context.Dispose();
            }
        }
    }

    public void DeepCaptureBoundsStackFramesWithoutLosingFrameworkClassification()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.Stack", "Runs", null, directory.Path);
        using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();

        CaptureAtDepth(80);
        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);

        string path = context.GetResultFiles().Should().ContainSingle().Which;
        using var artifact = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement[] frames = artifact.RootElement.GetProperty("stackFrames").EnumerateArray().ToArray();
        frames.Should().HaveCount(64);
        frames.Should().Contain(frame => frame.GetProperty("isFrameworkFrame").GetBoolean());
        JsonElement[] recursiveFrames = frames.Where(frame => frame.GetProperty("method").GetString() == typeof(AssertionFailureDiagnosticsContinuationTests).FullName + ".CaptureAtDepth").ToArray();
        recursiveFrames.Length.Should().BeGreaterThan(50);
        recursiveFrames.Should().OnlyContain(frame => !frame.GetProperty("isFrameworkFrame").GetBoolean());
        artifact.RootElement.GetProperty("assertion").GetProperty("message").GetString().Should().Be("deep stack");
    }

    public void MissingArtifactDirectoryReportsWriteFailureAndAllowsLaterCapture()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.MissingDirectory", "Runs", null, directory.Path);
        string path = context.TestTempDirectory!;
        using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
        Directory.Delete(path);

        TestContextImplementation.CaptureAssertionFailureDiagnostics("missing output directory", null, null);

        bool onWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        int warningCount = onWindows ? 2 : 1;
        int captureWarningIndex = onWindows ? 1 : 0;
        _warnings.Should().HaveCount(warningCount);
        if (onWindows)
        {
            _warnings[0].Format.Should().Be("Failed to delete incomplete assertion failure diagnostics artifact '{0}': {1}");
            _warnings[0].Arguments[0].Should().BeOfType<string>().Which.Should().StartWith(path + Path.DirectorySeparatorChar);
            _warnings[0].Arguments[1].Should().BeOfType<DirectoryNotFoundException>();
            _warnings[0].Arguments[1].Should().NotBeSameAs(_warnings[1].Arguments[1]);
        }

        _warnings[captureWarningIndex].Format.Should().Be("Failed to capture assertion failure diagnostics for test '{0}': {1}");
        _warnings[captureWarningIndex].Arguments[0].Should().Be("Tests.MissingDirectory.Runs");
        _warnings[captureWarningIndex].Arguments[1].Should().BeOfType<DirectoryNotFoundException>();
        context.GetResultFiles().Should().BeNull();
        Directory.Exists(path).Should().BeFalse();
        Directory.CreateDirectory(path);
        using JsonDocument artifact = CaptureArtifact(context, "after directory recreation", "ready", "restored");
        artifact.RootElement.GetProperty("captureIndex").GetInt32().Should().Be(2);
        artifact.RootElement.GetProperty("assertion").GetProperty("message").GetString().Should().Be("after directory recreation");
        artifact.RootElement.GetProperty("assertion").GetProperty("actual").GetString().Should().Be("restored");
        _warnings.Should().HaveCount(warningCount);
    }

#if NETFRAMEWORK
    public void ProcessPermissionFailureIsReportedAndLaterCaptureRecovers()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.ProcessPermission", "Runs", null, directory.Path);
        var permissions = new PermissionSet(PermissionState.None);
        permissions.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution));
        permissions.AddPermission(new ReflectionPermission(PermissionState.Unrestricted));
        permissions.AddPermission(new FileIOPermission(PermissionState.Unrestricted));
        permissions.AddPermission(new EnvironmentPermission(PermissionState.Unrestricted));
        _ = _platform.AdapterTraceLogger;
        IDisposable? failedScope = null;
        try
        {
            permissions.PermitOnly();
            failedScope = context.StartAssertionFailureDiagnosticsScope();
            using (TestContextImplementation.SetCurrentTestContext(context))
            {
                TestContextImplementation.CaptureAssertionFailureDiagnostics("restricted process access", null, null);
            }
        }
        finally
        {
            CodeAccessPermission.RevertPermitOnly();
            failedScope?.Dispose();
        }

        failedScope.Should().BeNull();
        _warnings.Should().HaveCount(2);
        _warnings[0].Format.Should().Be("Failed to initialize assertion failure diagnostics for test '{0}.{1}': {2}");
        _warnings[0].Arguments.Take(2).Should().Equal("Tests.ProcessPermission", "Runs");
        _warnings[0].Arguments[2].Should().BeOfType<SecurityException>();
        _warnings[1].Format.Should().Be("Failed to capture assertion failure diagnostics outside a test method invocation for test '{0}.{1}': {2}");
        _warnings[1].Arguments[2].Should().BeOfType<SecurityException>();
        Directory.GetFileSystemEntries(directory.Path).Should().BeEmpty();
        using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
        using JsonDocument artifact = CaptureArtifact(context, "after restoring process access", null, null);
        artifact.RootElement.GetProperty("assertion").GetProperty("message").GetString().Should().Be("after restoring process access");
        artifact.RootElement.GetProperty("activeTests").EnumerateArray()
            .Select(value => value.GetProperty("fullyQualifiedName").GetString()).Should().Equal("Tests.ProcessPermission.Runs");
    }

    public void DirectoryListingDenialDoesNotPreventDiagnosticsCapture()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.VolumePermission", "Runs", null, directory.Path);
        string path = context.TestTempDirectory!;
        using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
        DirectorySecurity access = Directory.GetAccessControl(path, AccessControlSections.Access);
        byte[] original = access.GetSecurityDescriptorBinaryForm();
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            access.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.ListDirectory, AccessControlType.Deny));
            Directory.SetAccessControl(path, access);
            Action enumerate = () => Directory.GetFiles(path);
            enumerate.Should().ThrowExactly<UnauthorizedAccessException>();

            TestContextImplementation.CaptureAssertionFailureDiagnostics("volume probe unavailable", "blue", "red");
        }
        finally
        {
            DirectorySecurity restored = new();
            restored.SetSecurityDescriptorBinaryForm(original, AccessControlSections.Access);
            Directory.SetAccessControl(path, restored);
        }

        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);
        string artifactPath = context.GetResultFiles().Should().ContainSingle().Which;
        using var artifact = JsonDocument.Parse(File.ReadAllText(artifactPath));
        JsonElement process = artifact.RootElement.GetProperty("process");
        process.TryGetProperty("outputVolumeError", out _).Should().BeFalse();
        process.GetProperty("outputVolumeTotalBytes").GetInt64().Should().BeGreaterThan(0);
        process.GetProperty("outputVolumeAvailableFreeBytes").GetInt64().Should().BeGreaterThanOrEqualTo(0);
        process.GetProperty("outputVolumePath").GetString().Should().Be(Path.GetPathRoot(path));
        artifact.RootElement.GetProperty("assertion").GetProperty("message").GetString().Should().Be("volume probe unavailable");
        artifact.RootElement.GetProperty("assertion").GetProperty("expected").GetString().Should().Be("blue");
        artifact.RootElement.GetProperty("test").GetProperty("fullyQualifiedName").GetString().Should().Be("Tests.VolumePermission.Runs");
        _warnings.Should().BeEmpty();
    }

    public void AttachmentPermissionFailureIsReportedWithoutLosingTheOriginalArtifact()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.AttachPermission", "Runs", null, directory.Path);
        using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
        TestContextImplementation.CaptureAssertionFailureDiagnostics("original assertion artifact", "blue", "red");
        string path = Directory.GetFiles(context.TestTempDirectory!, "*.json").Should().ContainSingle().Which;
        var permissions = new PermissionSet(PermissionState.None);
        permissions.AddPermission(new SecurityPermission(PermissionState.Unrestricted));
        permissions.AddPermission(new ReflectionPermission(PermissionState.Unrestricted));
        permissions.AddPermission(new EnvironmentPermission(PermissionState.Unrestricted));
        permissions.AddPermission(new FileIOPermission(FileIOPermissionAccess.Read, path));
        _ = _platform.AdapterTraceLogger;
        bool exists = false;
        Exception? pathFailure = null;
        try
        {
            permissions.PermitOnly();
            exists = File.Exists(path);
            try
            {
                Path.GetFullPath(path);
            }
            catch (Exception exception)
            {
                pathFailure = exception;
            }

            context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);
        }
        finally
        {
            CodeAccessPermission.RevertPermitOnly();
        }

        exists.Should().BeTrue();
        pathFailure.Should().BeOfType<SecurityException>();
        context.GetResultFiles().Should().BeNull();
        _warnings.Should().ContainSingle();
        _warnings[0].Format.Should().Be("Failed to attach assertion failure diagnostics artifact '{0}': {1}");
        _warnings[0].Arguments[0].Should().Be(path);
        _warnings[0].Arguments[1].Should().BeOfType<SecurityException>();
        using var artifact = JsonDocument.Parse(File.ReadAllText(path));
        artifact.RootElement.GetProperty("assertion").GetProperty("message").GetString().Should().Be("original assertion artifact");
        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);
        _warnings.Should().ContainSingle();
        context.Dispose();
        File.Exists(path).Should().BeFalse();
    }

    public void DeniedArtifactCreationDoesNotReportAnIncompleteCleanupFailure()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.WritePermission", "Runs", null, directory.Path);
        string path = context.TestTempDirectory!;
        using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
        DirectorySecurity access = Directory.GetAccessControl(path, AccessControlSections.Access);
        byte[] original = access.GetSecurityDescriptorBinaryForm();
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            access.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.CreateFiles, AccessControlType.Deny));
            Directory.SetAccessControl(path, access);
            Action create = () =>
            {
                using var stream = new FileStream(Path.Combine(path, "denied-probe.txt"), FileMode.CreateNew);
            };
            create.Should().ThrowExactly<UnauthorizedAccessException>();

            TestContextImplementation.CaptureAssertionFailureDiagnostics("file creation denied", "blue", "red");
        }
        finally
        {
            DirectorySecurity restored = new();
            restored.SetSecurityDescriptorBinaryForm(original, AccessControlSections.Access);
            Directory.SetAccessControl(path, restored);
        }

        _warnings.Should().ContainSingle();
        _warnings[0].Format.Should().Be("Failed to capture assertion failure diagnostics for test '{0}': {1}");
        _warnings[0].Arguments[0].Should().Be("Tests.WritePermission.Runs");
        _warnings[0].Arguments[1].Should().BeOfType<UnauthorizedAccessException>();
        Directory.GetFileSystemEntries(path).Should().BeEmpty();
        context.GetResultFiles().Should().BeNull();
        using JsonDocument artifact = CaptureArtifact(context, "after restoring file creation", "ready", "restored");
        artifact.RootElement.GetProperty("captureIndex").GetInt32().Should().Be(2);
        artifact.RootElement.GetProperty("assertion").GetProperty("message").GetString().Should().Be("after restoring file creation");
        _warnings.Should().ContainSingle();
    }
#endif

    protected override void Dispose(bool disposing)
    {
        try
        {
            MSTestSettings.Reset();
            TestContext.Current = _previousContext;
            PlatformServiceProvider.Instance = _previousPlatform;
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private static void EnableCapture()
    {
        MSTestSettings.Reset();
        MSTestSettings.PopulateSettings(
            "<RunSettings><MSTest><CaptureAssertionFailureDiagnostics>true</CaptureAssertionFailureDiagnostics></MSTest></RunSettings>",
            logger: null, configuration: null);
    }

    private static TestContextImplementation CreateContext(string className, string methodName, string? displayName, string directory, int attempt = 1)
    {
        var method = new Mock<ITestMethod>();
        method.SetupGet(value => value.FullClassName).Returns(className);
        method.SetupGet(value => value.Name).Returns(methodName);
        var context = new TestContextImplementation(
            method.Object, className, new Dictionary<string, object?> { ["TestResultsDirectory"] = directory }, null, null);
        context.SetDisplayName(displayName);
        context.Context.TestRunCount = attempt;
        context.SetOutcome(UnitTestOutcome.Passed);
        return context;
    }

    private static JsonDocument CaptureArtifact(TestContextImplementation context, string message, string? expected, string? actual)
    {
        TestContextImplementation.CaptureAssertionFailureDiagnostics(message, expected, actual);
        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);
        string path = context.GetResultFiles().Should().ContainSingle().Which;
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string ReplaceArtifactWithDirectory(TestContextImplementation context)
    {
        string path = Directory.GetFiles(context.TestTempDirectory!, "*.json").Should().ContainSingle().Which;
        File.Delete(path);
        Directory.CreateDirectory(path);
        return path;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void CaptureAtDepth(int depth)
    {
        if (depth == 0)
        {
            TestContextImplementation.CaptureAssertionFailureDiagnostics("deep stack", null, null);
        }
        else
        {
            CaptureAtDepth(depth - 1);
        }

        GC.KeepAlive(depth);
    }

    private sealed class DiagnosticsDirectory : IDisposable
    {
        public DiagnosticsDirectory() => Path = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "d" + Guid.NewGuid().ToString("N").Substring(0, 12))).FullName;

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
#endif
