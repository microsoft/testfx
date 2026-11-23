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

public sealed class TestContextTempDirectoryCleanupMutationTests : TestContainer
{
    private const string RetainVariable = "MSTEST_TEST_TEMP_DIRECTORY_RETAIN";
    private readonly string? _previousRetain = Environment.GetEnvironmentVariable(RetainVariable);
    private readonly IPlatformServiceProvider _previousPlatform = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _platform = new();

    public TestContextTempDirectoryCleanupMutationTests()
    {
        Environment.SetEnvironmentVariable(RetainVariable, null);
        PlatformServiceProvider.Instance = _platform;
    }

    public void TempDirectoryHoldingARegisteredResultFileIsRetainedWhenAPassingTestIsDisposed()
    {
        using ContextDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        string created = context.TestTempDirectory!;
        string attachment = Path.Combine(created, "attachment.txt");
        File.WriteAllText(attachment, "attachment");
        context.AddResultFile(attachment);

        context.GetResultFiles().Should().Equal(Path.GetFullPath(attachment));
        context.Dispose();

        Directory.Exists(created).Should().BeTrue();
        File.ReadAllText(attachment).Should().Be("attachment");
    }

    public void ResultFileLocationIsComparedWithTheCaseSensitivityOfTheOperatingSystem()
    {
        using ContextDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        string created = context.TestTempDirectory!;
        context.AddResultFile(Path.Combine(created.ToUpperInvariant(), "attachment.txt"));

        context.GetResultFiles().Should().HaveCount(1);
        context.Dispose();

        // Windows paths are case-insensitive, so the upper-cased file still lives under the directory and keeps it alive.
        Directory.Exists(created).Should().Be(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            Environment.SetEnvironmentVariable(RetainVariable, _previousRetain);
            PlatformServiceProvider.Instance = _previousPlatform;
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private static TestContextImplementation CreateContext(string results)
    {
        var method = new Mock<ITestMethod>();
        method.SetupGet(value => value.Name).Returns("Scratch");
        var context = new TestContextImplementation(
            method.Object, "Tests.Scratch", new Dictionary<string, object?> { ["TestResultsDirectory"] = results }, null, null);
        context.SetOutcome(UnitTestOutcome.Passed);
        return context;
    }

    private sealed class ContextDirectory : IDisposable
    {
        public ContextDirectory() => Path = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "t" + Guid.NewGuid().ToString("N").Substring(0, 12))).FullName;

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
#endif
