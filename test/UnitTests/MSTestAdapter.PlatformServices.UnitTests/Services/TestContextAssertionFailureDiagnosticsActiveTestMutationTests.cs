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

public sealed class TestContextAssertionFailureDiagnosticsActiveTestMutationTests : TestContainer
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Type ActiveTestType = typeof(TestContextImplementation).GetNestedType("ActiveTest", BindingFlags.NonPublic | BindingFlags.Public)!;
    private static readonly IDictionary ActiveTests = (IDictionary)typeof(TestContextImplementation).GetField("ActiveTests", StaticMembers)!.GetValue(null)!;
    private static readonly FieldInfo CurrentActiveTestField = typeof(TestContextImplementation).GetField("CurrentActiveTest", StaticMembers)!;
    private static readonly PropertyInfo CurrentActiveTestValue = CurrentActiveTestField.FieldType.GetProperty("Value")!;

    private readonly IPlatformServiceProvider _previousPlatform = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _platform = new();
    private readonly List<(string Format, object?[] Arguments)> _warnings = [];

    public TestContextAssertionFailureDiagnosticsActiveTestMutationTests()
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

    public void DisposingAScopeASecondTimeNeitherUnregistersNorUnbindsTheActiveTestAgain()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
        scope.Should().NotBeNull();
        object? activeTest = GetCurrentActiveTest();
        activeTest.Should().NotBeNull();
        long id = (long)ActiveTestType.GetProperty("Id", InstanceMembers)!.GetValue(activeTest)!;
        ActiveTests.Contains(id).Should().BeTrue("a started scope registers its active test");

        scope!.Dispose();

        ActiveTests.Contains(id).Should().BeFalse();
        GetCurrentActiveTest().Should().BeNull();

        // Publish the closed active test again, as if a concurrent flow still observed it, to see what a repeated Dispose touches.
        ActiveTests.Add(id, activeTest);
        SetCurrentActiveTest(activeTest);
        try
        {
            scope.Dispose();

            ActiveTests.Contains(id).Should().BeTrue("disposing an already disposed scope must not unregister the active test again");
            GetCurrentActiveTest().Should().BeSameAs(activeTest, "disposing an already disposed scope must not restore the previous active test again");
        }
        finally
        {
            SetCurrentActiveTest(null);
            ActiveTests.Remove(id);
        }

        _warnings.Should().BeEmpty();
    }

    public void ClosingAnActiveTestHandsOverEachPendingPathOnlyOnceAndReleasesThem()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        using IDisposable? scope = context.StartAssertionFailureDiagnosticsScope();
        TestContextImplementation.CaptureAssertionFailureDiagnostics("pending artifact", null, null);
        object activeTest = GetCurrentActiveTest()!;
        var pendingPaths = (ICollection)ActiveTestType.GetField("_pendingPaths", InstanceMembers)!.GetValue(activeTest)!;
        pendingPaths.Count.Should().Be(1);

        string[] drained = CloseAndDrain(activeTest);

        string artifact = drained.Should().ContainSingle().Which;
        File.Exists(artifact).Should().BeTrue("closing hands the artifacts over without deleting them");
        pendingPaths.Count.Should().Be(0, "the drained paths must not stay queued once the active test is closed");
        CloseAndDrain(activeTest).Should().BeEmpty();
        _warnings.Should().BeEmpty();
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

    private static TestContextImplementation CreateContext(string resultsDirectory)
    {
        var method = new Mock<ITestMethod>();
        method.SetupGet(value => value.FullClassName).Returns("Tests.ActiveTest");
        method.SetupGet(value => value.Name).Returns("Runs");
        var context = new TestContextImplementation(
            method.Object, "Tests.ActiveTest", new Dictionary<string, object?> { ["TestResultsDirectory"] = resultsDirectory }, null, null);
        context.SetOutcome(UnitTestOutcome.Passed);
        return context;
    }

    private static object? GetCurrentActiveTest()
        => CurrentActiveTestValue.GetValue(CurrentActiveTestField.GetValue(null));

    private static void SetCurrentActiveTest(object? activeTest)
        => CurrentActiveTestValue.SetValue(CurrentActiveTestField.GetValue(null), activeTest);

    private static string[] CloseAndDrain(object activeTest)
        => (string[])ActiveTestType.GetMethod("CloseAndDrain", InstanceMembers)!.Invoke(activeTest, null)!;

    private sealed class DiagnosticsDirectory : IDisposable
    {
        public DiagnosticsDirectory() => Path = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "d" + Guid.NewGuid().ToString("N").Substring(0, 12))).FullName;

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
#endif
