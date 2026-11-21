// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using System.Security.AccessControl;
using System.Security.Principal;

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

public sealed class TestContextTempDirectoryMutationTests : TestContainer
{
    private const string RetainVariable = "MSTEST_TEST_TEMP_DIRECTORY_RETAIN";
    private readonly string? _previousRetain = Environment.GetEnvironmentVariable(RetainVariable);
    private readonly IPlatformServiceProvider _previousPlatform = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _platform = new();

    public TestContextTempDirectoryMutationTests()
    {
        Environment.SetEnvironmentVariable(RetainVariable, null);
        PlatformServiceProvider.Instance = _platform;
    }

    public void FallsBackToTheSystemTemporaryDirectoryWhenTheResultsDirectoryIsAFile()
    {
        using ContextDirectory directory = new();
        string resultsFile = Path.Combine(directory.Path, "results");
        File.WriteAllText(resultsFile, "results path is a file");
        using TestContextImplementation context = CreateContext(resultsFile);

        string created = context.TestTempDirectory!;

        Path.GetDirectoryName(created).Should().Be(TemporaryRoot());
        Path.GetFileName(created).Should().MatchRegex("^Scratch_[0-9a-f]{32}$");
        Directory.Exists(created).Should().BeTrue();
        File.ReadAllText(resultsFile).Should().Be("results path is a file");
        context.Dispose();
        Directory.Exists(created).Should().BeFalse();
    }

    public void ReadableNameIsSizedFromTheSystemTemporaryDirectoryWhenTheResultsDirectoryFails()
    {
        using ContextDirectory directory = new();
        if (directory.Path.Length > 123)
        {
            return;
        }

        string longRoot = Directory.CreateDirectory(Path.Combine(directory.Path, new string('d', 124 - directory.Path.Length))).FullName;
        string resultsFile = Path.Combine(directory.Path, "results");
        File.WriteAllText(resultsFile, "results path is a file");
        using TestContextImplementation context = CreateContext(resultsFile);
        context.SetDisplayName(new string('n', 60));
        string[] variables = ["TEMP", "TMP", "TMPDIR"];
        string?[] originals = variables.Select(Environment.GetEnvironmentVariable).ToArray();
        string created;
        try
        {
            foreach (string variable in variables)
            {
                Environment.SetEnvironmentVariable(variable, longRoot);
            }

            TemporaryRoot().Should().Be(longRoot);

            created = context.TestTempDirectory!;
        }
        finally
        {
            for (int index = 0; index < variables.Length; index++)
            {
                Environment.SetEnvironmentVariable(variables[index], originals[index]);
            }
        }

        // On Windows: 260 (MAX_PATH) - 80 (headroom) - 126 (root and separator) - 2 (separators) - 32 (unique suffix).
        int expectedNameLength = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 20 : 50;
        Path.GetDirectoryName(created).Should().Be(longRoot);
        Path.GetFileName(created).Should().MatchRegex($"^n{{{expectedNameLength}}}_[0-9a-f]{{32}}$");
    }

    public void ResultsDirectoryIsKeptWhenItLeavesExactlyTheMinimumReadableNameBudget()
    {
        using ContextDirectory directory = new();
        if (directory.Path.Length > 136)
        {
            return;
        }

        // On Windows: 260 (MAX_PATH) - 80 (headroom) - 138 (path) - 2 (separators) - 32 (unique suffix) = 8, the minimum.
        string results = Directory.CreateDirectory(Path.Combine(directory.Path, new string('r', 137 - directory.Path.Length))).FullName;
        results.Length.Should().Be(138);
        using TestContextImplementation context = CreateContext(results);
        context.SetDisplayName("ScratchNameLong");

        string created = context.TestTempDirectory!;

        Path.GetDirectoryName(created).Should().Be(results);
        Path.GetFileName(created).Should().MatchRegex(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "^ScratchN_[0-9a-f]{32}$"
            : "^ScratchNameLong_[0-9a-f]{32}$");
    }

    public void FallsBackToTheSystemTemporaryDirectoryWhenTheResultsDirectoryRejectsNewSubdirectories()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AssertFallsBackWhenNewSubdirectoriesAreDenied();
        }
    }

    public void MissingResultsDirectoryIsCreatedAndUsedAsTheBase()
    {
        using ContextDirectory directory = new();
        string results = Path.Combine(directory.Path, "missing", "results");
        using TestContextImplementation context = CreateContext(results);

        string created = context.TestTempDirectory!;

        Path.GetDirectoryName(created).Should().Be(results);
        Directory.Exists(created).Should().BeTrue();
    }

    public void CreatedDirectoryIsReturnedWithoutWaitingForTheCreationLock()
    {
        using ContextDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        string created = context.TestTempDirectory!;
        object creationLock = typeof(TestContextImplementation)
            .GetField("_testTempDirectoryLock", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(context)!;
        string? observed = null;
        Thread reader = new(() => observed = context.TestTempDirectory)
        {
            IsBackground = true,
        };

        bool returnedWhileLocked;
        EnterLock(creationLock);
        try
        {
            reader.Start();
            returnedWhileLocked = reader.Join(TimeSpan.FromSeconds(5));
        }
        finally
        {
            ExitLock(creationLock);
        }

        reader.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();
        returnedWhileLocked.Should().BeTrue("an already created directory must be returned without taking the creation lock");
        observed.Should().Be(created);
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

    [SupportedOSPlatform("windows")]
    private static void AssertFallsBackWhenNewSubdirectoriesAreDenied()
    {
        using ContextDirectory directory = new();
        string results = Directory.CreateDirectory(Path.Combine(directory.Path, "results")).FullName;
        using TestContextImplementation context = CreateContext(results);

        string fallback;
        using (new DirectoryCreationDenial(results))
        {
            Action createChild = () => Directory.CreateDirectory(Path.Combine(results, "denied-child"));
            createChild.Should().ThrowExactly<UnauthorizedAccessException>();

            fallback = context.TestTempDirectory!;

            Directory.GetDirectories(results).Should().BeEmpty();
        }

        Path.GetDirectoryName(fallback).Should().Be(TemporaryRoot());
        Path.GetFileName(fallback).Should().MatchRegex("^Scratch_[0-9a-f]{32}$");
        Directory.Exists(fallback).Should().BeTrue();
    }

    private static string TemporaryRoot()
        => Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static void EnterLock(object gate)
#if NET9_0_OR_GREATER
        => ((Lock)gate).Enter();
#else
        => Monitor.Enter(gate);
#endif

    private static void ExitLock(object gate)
#if NET9_0_OR_GREATER
        => ((Lock)gate).Exit();
#else
        => Monitor.Exit(gate);
#endif

    private static TestContextImplementation CreateContext(string results)
    {
        var method = new Mock<ITestMethod>();
        method.SetupGet(value => value.Name).Returns("Scratch");
        var context = new TestContextImplementation(
            method.Object, "Tests.Scratch", new Dictionary<string, object?> { ["TestResultsDirectory"] = results }, null, null);
        context.SetOutcome(UnitTestOutcome.Passed);
        return context;
    }

    [SupportedOSPlatform("windows")]
    private sealed class DirectoryCreationDenial : IDisposable
    {
        private readonly DirectoryInfo _directory;
        private readonly byte[] _original;

        public DirectoryCreationDenial(string path)
        {
            _directory = new DirectoryInfo(path);
            DirectorySecurity access = _directory.GetAccessControl(AccessControlSections.Access);
            _original = access.GetSecurityDescriptorBinaryForm();
            using var identity = WindowsIdentity.GetCurrent();
            access.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.CreateDirectories, AccessControlType.Deny));
            _directory.SetAccessControl(access);
        }

        public void Dispose()
        {
            DirectorySecurity original = new();
            original.SetSecurityDescriptorBinaryForm(_original, AccessControlSections.Access);
            _directory.SetAccessControl(original);
        }
    }

    private sealed class ContextDirectory : IDisposable
    {
        public ContextDirectory() => Path = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "t" + Guid.NewGuid().ToString("N").Substring(0, 12))).FullName;

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
#endif
