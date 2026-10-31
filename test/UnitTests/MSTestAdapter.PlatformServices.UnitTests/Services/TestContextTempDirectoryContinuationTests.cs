// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
#if NETFRAMEWORK
using System.Security;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Security.Principal;
#endif

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

public sealed class TestContextTempDirectoryContinuationTests : TestContainer
{
    private const string RetainVariable = "MSTEST_TEST_TEMP_DIRECTORY_RETAIN";
    private readonly string? _previousRetain = Environment.GetEnvironmentVariable(RetainVariable);
    private readonly IPlatformServiceProvider _previousPlatform = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _platform = new();

    public TestContextTempDirectoryContinuationTests()
    {
        Environment.SetEnvironmentVariable(RetainVariable, null);
        PlatformServiceProvider.Instance = _platform;
    }

    public void AllUnavailableBasesSurfaceIoFailureWithoutCachingIt()
    {
        using ContextDirectory directory = new();
        string resultsFile = Path.Combine(directory.Path, "results");
        string tempFile = Path.Combine(directory.Path, "temp");
        File.WriteAllText(resultsFile, "results path remains a file");
        File.WriteAllText(tempFile, "temp path remains a file");
        using TestContextImplementation context = CreateContext(resultsFile);
        string[] variables = ["TEMP", "TMP", "TMPDIR"];
        string?[] originals = variables.Select(Environment.GetEnvironmentVariable).ToArray();
        string originalTempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        try
        {
            foreach (string variable in variables)
            {
                Environment.SetEnvironmentVariable(variable, tempFile);
            }

            Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Should().Be(tempFile);
            Action create = () => _ = context.TestTempDirectory;

            IOException failure = create.Should().Throw<IOException>().Subject.Single();
            failure.Message.Should().Contain(tempFile);
#if NETCOREAPP
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                failure.InnerException.Should().BeOfType<System.ComponentModel.Win32Exception>()
                    .Which.NativeErrorCode.Should().Be(20);
                failure.Message.Should().StartWith($"Could not create test temporary directory '{tempFile}{Path.DirectorySeparatorChar}").And.EndWith("'.");
            }
#endif
            File.ReadAllText(resultsFile).Should().Be("results path remains a file");
            File.ReadAllText(tempFile).Should().Be("temp path remains a file");
        }
        finally
        {
            for (int index = 0; index < variables.Length; index++)
            {
                Environment.SetEnvironmentVariable(variables[index], originals[index]);
            }
        }

        string path = context.TestTempDirectory!;
        Path.GetDirectoryName(path).Should().Be(originalTempRoot);
        string artifact = Path.Combine(path, "recovered.txt");
        File.WriteAllText(artifact, "usable after the failed initialization");
        File.ReadAllText(artifact).Should().Be("usable after the failed initialization");
        context.Dispose();
        Directory.Exists(path).Should().BeFalse();
    }

    public void UnixTempDirectoryIsOwnerOnlyAndCanStoreArtifacts()
    {
        using ContextDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);

        string path = context.TestTempDirectory!;

        Path.GetDirectoryName(path).Should().Be(directory.Path);
        string artifact = Path.Combine(path, "artifact.txt");
        File.WriteAllText(artifact, "private scratch bytes");
        File.ReadAllText(artifact).Should().Be("private scratch bytes");
#if NETCOREAPP
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.GetUnixFileMode(path).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
#endif
        context.Dispose();
        Directory.Exists(path).Should().BeFalse();
    }

    public void RetentionOverrideHonorsOnlyDocumentedTruthyValues()
    {
        string? previous = Environment.GetEnvironmentVariable(RetainVariable);
        try
        {
            foreach ((string? value, bool retained) in new (string?, bool)[]
            {
                ("1", true),
                ("true", true),
                ("TrUe", true),
                ("0", false),
                ("false", false),
                (" true ", false),
                (string.Empty, false),
                (null, false),
            })
            {
                using ContextDirectory directory = new();
                using TestContextImplementation context = CreateContext(directory.Path);
                string path = context.TestTempDirectory!;
                string artifact = Path.Combine(path, "state.txt");
                File.WriteAllText(artifact, "retained bytes");
                Environment.SetEnvironmentVariable(RetainVariable, value);

                context.Dispose();

                Directory.Exists(path).Should().Be(retained);
                if (retained)
                {
                    File.ReadAllText(artifact).Should().Be("retained bytes");
                }
                else
                {
                    File.Exists(artifact).Should().BeFalse();
                }
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(RetainVariable, previous);
        }
    }

    public void SiblingPrefixAttachmentsDoNotRetainTheTempDirectory()
    {
        using ContextDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        string path = context.TestTempDirectory!;
        string sibling = Directory.CreateDirectory(path + "-sibling").FullName;
        string attachment = Path.Combine(sibling, "attachment.txt");
        File.WriteAllText(attachment, "sibling attachment bytes");
        context.AddResultFile(attachment);
        context.GetResultFiles().Should().Equal(attachment);

        context.Dispose();

        Directory.Exists(path).Should().BeFalse();
        File.ReadAllText(attachment).Should().Be("sibling attachment bytes");
    }

    public void ConsumingOnlyOutsideAttachmentsStillDeletesPerTestArtifacts()
    {
        using ContextDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        string path = context.TestTempDirectory!;
        string attachment = Path.Combine(directory.Path, "outside.txt");
        File.WriteAllText(attachment, "outside attachment bytes");
        context.AddResultFile(attachment);

        context.GetResultFiles().Should().Equal(attachment);
        context.GetResultFiles().Should().BeNull();
        context.Dispose();

        Directory.Exists(path).Should().BeFalse();
        File.ReadAllText(attachment).Should().Be("outside attachment bytes");
    }

#if NETFRAMEWORK
    public void DeniedChildCreationFallsBackWithoutChangingTheResultsDirectory()
    {
        using ContextDirectory directory = new();
        string results = Directory.CreateDirectory(Path.Combine(directory.Path, "results")).FullName;
        string existing = Path.Combine(results, "existing.txt");
        File.WriteAllText(existing, "pre-existing results bytes");
        using TestContextImplementation context = CreateContext(results);
        string fallback;
        using (new DirectoryCreationDenial(results))
        {
            Action createChild = () => Directory.CreateDirectory(Path.Combine(results, "denied-child"));
            createChild.Should().ThrowExactly<UnauthorizedAccessException>();

            fallback = context.TestTempDirectory!;

            Path.GetDirectoryName(fallback).Should().Be(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            Directory.GetDirectories(results).Should().BeEmpty();
            File.ReadAllText(existing).Should().Be("pre-existing results bytes");
            string scratch = Path.Combine(fallback, "usable.txt");
            File.WriteAllText(scratch, "fallback bytes");
            File.ReadAllText(scratch).Should().Be("fallback bytes");
        }

        context.Dispose();
        Directory.Exists(fallback).Should().BeFalse();
        File.ReadAllText(existing).Should().Be("pre-existing results bytes");
    }

    public void LockedCleanupAndThrowingLoggerCannotFailDisposal()
    {
        using ContextDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        string path = context.TestTempDirectory!;
        string locked = Path.Combine(path, "locked.txt");
        var warnings = new List<(string Format, object?[] Arguments)>();
        _platform.MockTraceLogger
            .Setup(logger => logger.Warning(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) => warnings.Add((format, [.. arguments])))
            .Throws(new InvalidOperationException("logger failure must not escape cleanup"));
        using (var stream = new FileStream(locked, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            Action dispose = context.Dispose;

            dispose.Should().NotThrow();

            Directory.Exists(path).Should().BeTrue();
            warnings.Should().ContainSingle();
            warnings[0].Format.Should().Be("Failed to delete per-test temporary directory '{0}': {1}");
            warnings[0].Arguments[0].Should().Be(path);
            warnings[0].Arguments[1].Should().BeOfType<IOException>();
        }

        context.Dispose();
        Directory.Exists(path).Should().BeFalse();
        warnings.Should().ContainSingle();
    }

    public void CleanupTreatsDeniedEnvironmentPermissionAsUnset()
    {
        using ContextDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        string path = context.TestTempDirectory!;
        File.WriteAllText(Path.Combine(path, "scratch.txt"), "scratch bytes");
        var permissions = new PermissionSet(PermissionState.None);
        permissions.AddPermission(new SecurityPermission(PermissionState.Unrestricted));
        permissions.AddPermission(new ReflectionPermission(PermissionState.Unrestricted));
        permissions.AddPermission(new FileIOPermission(PermissionState.Unrestricted));
        Exception? environmentFailure = null;
        Exception? cleanupFailure = null;
        try
        {
            permissions.PermitOnly();
            try
            {
                _ = Environment.GetEnvironmentVariable(RetainVariable);
            }
            catch (Exception exception)
            {
                environmentFailure = exception;
            }

            try
            {
                context.Dispose();
            }
            catch (Exception exception)
            {
                cleanupFailure = exception;
            }
        }
        finally
        {
            CodeAccessPermission.RevertPermitOnly();
        }

        environmentFailure.Should().BeOfType<SecurityException>();
        cleanupFailure.Should().BeNull();
        Directory.Exists(path).Should().BeFalse();
    }
#endif

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

#if NETFRAMEWORK
    private sealed class DirectoryCreationDenial : IDisposable
    {
        private readonly string _path;
        private readonly byte[] _original;

        public DirectoryCreationDenial(string path)
        {
            _path = path;
            DirectorySecurity access = Directory.GetAccessControl(path, AccessControlSections.Access);
            _original = access.GetSecurityDescriptorBinaryForm();
            using var identity = WindowsIdentity.GetCurrent();
            access.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.CreateDirectories, AccessControlType.Deny));
            Directory.SetAccessControl(path, access);
        }

        public void Dispose()
        {
            DirectorySecurity original = new();
            original.SetSecurityDescriptorBinaryForm(_original, AccessControlSections.Access);
            Directory.SetAccessControl(_path, original);
        }
    }
#endif

    private sealed class ContextDirectory : IDisposable
    {
        public ContextDirectory() => Path = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "t" + Guid.NewGuid().ToString("N").Substring(0, 12))).FullName;

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
#endif
