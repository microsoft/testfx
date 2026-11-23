// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
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

public sealed class TestContextAssertionFailureDiagnosticsMutationTests : TestContainer
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly FieldInfo PropertiesField = typeof(TestContextImplementation).GetField("_properties", InstanceMembers)!;
    private static readonly FieldInfo TestTempDirectoryField = typeof(TestContextImplementation).GetField("_testTempDirectory", InstanceMembers)!;
    private static readonly MethodInfo RecordCompletedPathMethod = typeof(TestContextImplementation).GetMethod("RecordCompletedAssertionFailureDiagnosticsPath", InstanceMembers)!;

    private readonly IPlatformServiceProvider _previousPlatform = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _platform = new();
    private readonly List<(string Format, object?[] Arguments)> _warnings = [];

    public TestContextAssertionFailureDiagnosticsMutationTests()
    {
        PlatformServiceProvider.Instance = _platform;
        _platform.MockTraceLogger
            .Setup(logger => logger.Warning(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) => _warnings.Add((format, [.. arguments])));
        MSTestSettings.Reset();
        MSTestSettings.PopulateSettings(
            "<RunSettings><MSTest><CaptureAssertionFailureDiagnostics>true</CaptureAssertionFailureDiagnostics></MSTest></RunSettings>",
            logger: null, configuration: null);
    }

    public void ScopesThatCannotBeInitializedAreNotStartedAndAreReportedAsWarnings()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.Init", "Starts", directory.Path);
        var original = (Dictionary<string, object?>)PropertiesField.GetValue(context)!;
        PropertiesField.SetValue(context, new FailOnceDictionary(original));
        try
        {
            using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();

            scope.Should().BeNull();
        }
        finally
        {
            PropertiesField.SetValue(context, original);
        }

        (string format, object?[] arguments) = _warnings.Should().ContainSingle().Which;
        format.Should().Be("Failed to initialize assertion failure diagnostics for test '{0}.{1}': {2}");
        arguments.Should().HaveCount(3);
        arguments[0].Should().Be("Tests.Init");
        arguments[1].Should().Be("Starts");
        arguments[2].Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("boom");
    }

    public void FailedAttemptsKeepTheTestTempDirectoryEvenOnceTheirResultFilesWereCollected()
    {
        using DiagnosticsDirectory directory = new();
        TestContextImplementation context = CreateContext("Tests.Retain", "Retries", directory.Path);
        string tempDirectory;
        try
        {
            using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
            TestContextImplementation.CaptureAssertionFailureDiagnostics("kept for the retry", null, null);

            context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);

            tempDirectory = context.TestTempDirectory!;
            context.GetResultFiles().Should().ContainSingle();
            context.GetResultFiles().Should().BeNull("the host already collected the attachments of this attempt");
        }
        finally
        {
            context.Dispose();
        }

        Directory.Exists(tempDirectory).Should().BeTrue("the retained artifact must outlive the disposed context");
        Directory.GetFiles(tempDirectory, "*.json").Should().ContainSingle();
        _warnings.Should().BeEmpty();
    }

    public void FailedAttemptsRecordTheirArtifactsSoThatAPassingExecutionRemovesThem()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.Record", "Retries", directory.Path);
        using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
        TestContextImplementation.CaptureAssertionFailureDiagnostics("superseded by a passing retry", null, null);
        context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);
        IList<string> attachments = context.GetResultFiles()!;
        string artifact = attachments.Should().ContainSingle().Which;
        TestResult[] results = [new() { Outcome = UnitTestOutcome.Passed, ResultFiles = attachments }];

        context.FinalizeAssertionFailureDiagnosticsExecution(results, resetCaptureBudget: true);

        results[0].ResultFiles.Should().BeNull();
        File.Exists(artifact).Should().BeFalse("the diagnostics of a failed attempt are dropped when the test passes in the end");
        _warnings.Should().BeEmpty();
    }

    public void ArtifactsThatCannotBeAttachedAreReportedAsWarnings()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.Attach", "Fails", directory.Path);
        using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
        TestContextImplementation.CaptureAssertionFailureDiagnostics("cannot be attached", null, null);
        string tempDirectory = context.TestTempDirectory!;
        string artifact = Directory.GetFiles(tempDirectory, "*.json").Should().ContainSingle().Which;

        // A per-test temp directory that is not a valid path makes AddResultFile throw once the artifact exists.
        TestTempDirectoryField.SetValue(context, "\0");
        try
        {
            context.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);
        }
        finally
        {
            TestTempDirectoryField.SetValue(context, tempDirectory);
        }

        (string format, object?[] arguments) = _warnings.Should().ContainSingle().Which;
        format.Should().Be("Failed to attach assertion failure diagnostics artifact '{0}': {1}");
        arguments.Should().HaveCount(2);
        arguments[0].Should().Be(artifact);
        arguments[1].Should().BeAssignableTo<ArgumentException>();
    }

    public void TransferredDiagnosticsOfAFailedResultKeepTheTestTempDirectoryOfTheDestination()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation iteration = CreateContext("Tests.Iteration", "Row", directory.Path);
        TestContextImplementation outer = CreateContext("Tests.Outer", "Rows", directory.Path);
        TestResult[] results = [new() { Outcome = UnitTestOutcome.Failed }];
        string outerDirectory;
        try
        {
            using IDisposable? scope = iteration.StartAssertionFailureDiagnosticsScope();
            TestContextImplementation.CaptureAssertionFailureDiagnostics("row failed", null, null);
            iteration.FinalizeAssertionFailureDiagnostics(UnitTestOutcome.Failed);
            iteration.TransferAssertionFailureDiagnosticsTo(outer);
            outerDirectory = outer.TestTempDirectory!;

            outer.FinalizeAssertionFailureDiagnosticsExecution(results, resetCaptureBudget: false);
        }
        finally
        {
            outer.Dispose();
        }

        results[0].ResultFiles.Should().ContainSingle();
        Directory.Exists(outerDirectory).Should().BeTrue("the destination must keep its directory for the attached diagnostics");
        _warnings.Should().BeEmpty();
    }

    public void CompletedArtifactsAreMatchedWithTheCaseSensitivityOfTheOperatingSystem()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext("Tests.Casing", "Fails", directory.Path);
        RecordCompletedPathMethod.Invoke(context, [Path.Combine(directory.Path, "diagnostics.json")]);
        RecordCompletedPathMethod.Invoke(context, [Path.Combine(directory.Path, "DIAGNOSTICS.JSON")]);
        TestResult[] results = [new() { Outcome = UnitTestOutcome.Failed }];

        context.FinalizeAssertionFailureDiagnosticsExecution(results, resetCaptureBudget: false);

        results[0].ResultFiles.Should().HaveCount(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 1 : 2,
            "paths only differing by case are the same file on Windows but different files elsewhere");
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = _previousPlatform;
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private static TestContextImplementation CreateContext(string className, string methodName, string resultsDirectory)
    {
        var method = new Mock<ITestMethod>();
        method.SetupGet(value => value.FullClassName).Returns(className);
        method.SetupGet(value => value.Name).Returns(methodName);
        var context = new TestContextImplementation(
            method.Object, className, new Dictionary<string, object?> { ["TestResultsDirectory"] = resultsDirectory }, null, null);
        context.SetOutcome(UnitTestOutcome.Passed);
        return context;
    }

    private sealed class FailOnceDictionary : Dictionary<string, object?>, IDictionary<string, object?>
    {
        private bool _hasFailed;

        public FailOnceDictionary(Dictionary<string, object?> source)
            : base(source)
        {
        }

        bool IDictionary<string, object?>.TryGetValue(string key, out object? value)
        {
            if (!_hasFailed)
            {
                _hasFailed = true;
                throw new InvalidOperationException("boom");
            }

            return TryGetValue(key, out value);
        }
    }

    private sealed class DiagnosticsDirectory : IDisposable
    {
        public DiagnosticsDirectory() => Path = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "d" + Guid.NewGuid().ToString("N").Substring(0, 12))).FullName;

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
#endif
