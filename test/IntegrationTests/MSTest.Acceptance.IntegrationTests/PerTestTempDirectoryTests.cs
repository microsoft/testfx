// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class PerTestTempDirectoryTests : AcceptanceTestBase<PerTestTempDirectoryTests.TestAssetFixture>
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task TestTempDirectory_IsUnique_CleansUpOnPass_And_RetainsOnFailure()
    {
        string recordDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(recordDirectory);
        try
        {
            var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
            TestHostResult testHostResult = await testHost.ExecuteAsync(
                "--filter \"ClassName~Uniqueness|ClassName~DataRowUniqueness|ClassName~PassCleanup|ClassName~FailRetain\"",
                environmentVariables: new()
                {
                    ["TESTTEMPDIR_RECORD_DIR"] = recordDirectory,
                },
                cancellationToken: TestContext.CancellationToken);

            // A failing test is present, so the run reports failure.
            testHostResult.AssertExitCodeIs(2);
            testHostResult.AssertOutputContainsSummary(failed: 1, passed: 8, skipped: 0);

            Dictionary<string, List<string>> records = ReadRecords(recordDirectory);

            // Uniqueness across concurrently running tests.
            List<string> uniquePaths = records["unique"];
            Assert.AreEqual(4, uniquePaths.Count);
            Assert.AreEqual(uniquePaths.Count, uniquePaths.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Temp directories must be unique across concurrent tests.");

            // Uniqueness across [DataRow] cases of the same method.
            List<string> dataRowPaths = records["datarow"];
            Assert.AreEqual(3, dataRowPaths.Count);
            Assert.AreEqual(dataRowPaths.Count, dataRowPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Temp directories must be unique across data-driven cases.");

            // All paths across every test are distinct.
            var allPaths = records.Values.SelectMany(v => v).Where(p => p != "NONE").ToList();
            Assert.AreEqual(allPaths.Count, allPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count());

            // Cleanup on pass: the passing test's directory is deleted.
            string passingPath = Assert.ContainsSingle(records["pass"]);
            Assert.IsFalse(Directory.Exists(passingPath), $"Passing test temp directory should be cleaned up but still exists: '{passingPath}'.");

            // Retain on failure: the failing test's directory (and its artifact) survive.
            string failingPath = Assert.ContainsSingle(records["fail"]);
            Assert.IsTrue(Directory.Exists(failingPath), $"Failing test temp directory should be retained but is missing: '{failingPath}'.");
            Assert.IsTrue(File.Exists(Path.Combine(failingPath, "artifact.txt")), "Failing test artifact should be retained for inspection.");
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
                Assert.AreEqual(ownerOnly, File.GetUnixFileMode(failingPath), "Test temp directories must be accessible only to their owner.");
            }
        }
        finally
        {
            TryDeleteDirectory(recordDirectory);
        }
    }

    [TestMethod]
    public async Task TestTempDirectory_IsOwnerOnlyAndUsable_WithRestrictiveUmask()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        string recordDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(recordDirectory);
        string? tempDirectory = null;
        try
        {
            var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
            TestHostResult testHostResult = await testHost.ExecuteAsync(
                "--filter ClassName~RestrictiveUmask",
                environmentVariables: new()
                {
                    ["TESTTEMPDIR_RECORD_DIR"] = recordDirectory,
                    ["MSTEST_TEST_TEMP_DIRECTORY_RETAIN"] = "1",
                },
                cancellationToken: TestContext.CancellationToken);

            testHostResult.AssertExitCodeIs(0);
            testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

            tempDirectory = Assert.ContainsSingle(ReadRecords(recordDirectory)["umask"]);
            Assert.IsTrue(File.Exists(Path.Combine(tempDirectory, "write-check.txt")), "The test must be able to write inside its temporary directory.");
            UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            Assert.AreEqual(ownerOnly, File.GetUnixFileMode(tempDirectory));
        }
        finally
        {
            TryDeleteDirectory(recordDirectory);
            if (tempDirectory is not null)
            {
                TryDeleteDirectory(tempDirectory);
            }
        }
    }

    [TestMethod]
    public async Task TestTempDirectory_IsCreatedLazily()
    {
        string recordDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(recordDirectory);
        try
        {
            var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);

            // Retain everything so the accessor's directory survives for inspection.
            TestHostResult testHostResult = await testHost.ExecuteAsync(
                "--filter ClassName~Lazy",
                environmentVariables: new()
                {
                    ["TESTTEMPDIR_RECORD_DIR"] = recordDirectory,
                    ["MSTEST_TEST_TEMP_DIRECTORY_RETAIN"] = "1",
                },
                cancellationToken: TestContext.CancellationToken);

            testHostResult.AssertExitCodeIs(0);
            testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);

            Dictionary<string, List<string>> records = ReadRecords(recordDirectory);

            string accessorPath = Assert.ContainsSingle(records["accessor"]);
            Assert.IsTrue(Directory.Exists(accessorPath), $"Accessor test temp directory should exist: '{accessorPath}'.");

            // The non-accessor test never touched the property, so no directory is created for it.
            string? resultsDirectory = Path.GetDirectoryName(accessorPath);
            Assert.IsNotNull(resultsDirectory);
            string[] siblings = Directory.GetDirectories(resultsDirectory);
            Assert.IsTrue(siblings.Any(d => Path.GetFileName(d).StartsWith("AccessorTest", StringComparison.Ordinal)), "Accessor test directory should be present.");
            Assert.IsFalse(siblings.Any(d => Path.GetFileName(d).StartsWith("NonAccessorTest", StringComparison.Ordinal)), "No directory should be created for a test that never accesses TestTempDirectory.");
        }
        finally
        {
            TryDeleteDirectory(recordDirectory);
        }
    }

    [TestMethod]
    public async Task TestTempDirectory_IsRetained_WhenDataRowCleanupFails()
    {
        // Regression: a folded [DataRow] whose body passes but whose [TestCleanup] fails must have
        // each row's temp directory retained. The framework sets the per-row (cloned) context to
        // Passed before cleanup runs, so without re-syncing the post-cleanup outcome the folded
        // path would delete a failed row's directory.
        string recordDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(recordDirectory);
        try
        {
            var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
            TestHostResult testHostResult = await testHost.ExecuteAsync(
                "--filter \"ClassName~DataRowCleanupFailure\"",
                environmentVariables: new()
                {
                    ["TESTTEMPDIR_RECORD_DIR"] = recordDirectory,
                },
                cancellationToken: TestContext.CancellationToken);

            // Both rows fail (via cleanup), so the run reports failure.
            testHostResult.AssertExitCodeIs(2);

            Dictionary<string, List<string>> records = ReadRecords(recordDirectory);
            List<string> paths = records["datarowcleanupfail"];
            Assert.AreEqual(2, paths.Count);
            foreach (string path in paths)
            {
                Assert.IsTrue(Directory.Exists(path), $"Row temp directory should be retained after cleanup failure but is missing: '{path}'.");
            }
        }
        finally
        {
            TryDeleteDirectory(recordDirectory);
        }
    }

    [TestMethod]
    public async Task TestTempDirectory_IsRetained_OnPass_WhenResultFileRegisteredUnderIt()
    {
        // A passing test that writes a file into its temp directory and registers it via
        // TestContext.AddResultFile must keep that file: the host collects the attachment after the
        // context is disposed, so deleting the directory on pass would break the attachment.
        string recordDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(recordDirectory);
        try
        {
            var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
            TestHostResult testHostResult = await testHost.ExecuteAsync(
                "--filter \"ClassName~AddResultFileRetention\"",
                environmentVariables: new()
                {
                    ["TESTTEMPDIR_RECORD_DIR"] = recordDirectory,
                },
                cancellationToken: TestContext.CancellationToken);

            testHostResult.AssertExitCodeIs(0);
            testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

            Dictionary<string, List<string>> records = ReadRecords(recordDirectory);
            string attachment = Assert.ContainsSingle(records["addresultfile"]);
            Assert.IsTrue(File.Exists(attachment), $"Registered result file should be retained on pass but is missing: '{attachment}'.");
        }
        finally
        {
            TryDeleteDirectory(recordDirectory);
        }
    }

    private static Dictionary<string, List<string>> ReadRecords(string recordDirectory)
    {
        Dictionary<string, List<string>> result = [];
        foreach (string file in Directory.GetFiles(recordDirectory, "*.txt"))
        {
            string content = File.ReadAllText(file);
            int separatorIndex = content.IndexOf('|');
            string key = content.Substring(0, separatorIndex);
            string value = content.Substring(separatorIndex + 1);
            if (!result.TryGetValue(key, out List<string>? list))
            {
                list = [];
                result[key] = list;
            }

            list.Add(value);
        }

        return result;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception)
        {
            // Best effort cleanup of the test's own scratch directory.
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "TestTempDirectoryAsset";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file TestTempDirectoryAsset.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>preview</LangVersion>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]

internal static class Recorder
{
    public static void Record(string key, string value)
    {
        string dir = Environment.GetEnvironmentVariable("TESTTEMPDIR_RECORD_DIR");
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, Guid.NewGuid().ToString("N") + ".txt"), key + "|" + value);
    }
}

[TestClass]
public class Uniqueness
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void A() => Recorder.Record("unique", TestContext.TestTempDirectory);

    [TestMethod]
    public void B() => Recorder.Record("unique", TestContext.TestTempDirectory);

    [TestMethod]
    public void C() => Recorder.Record("unique", TestContext.TestTempDirectory);

    [TestMethod]
    public void D() => Recorder.Record("unique", TestContext.TestTempDirectory);
}

[TestClass]
public class DataRowUniqueness
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void Row(int i) => Recorder.Record("datarow", TestContext.TestTempDirectory);
}

[TestClass]
public class PassCleanup
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Passing()
    {
        string dir = TestContext.TestTempDirectory;
        File.WriteAllText(Path.Combine(dir, "artifact.txt"), "hello");
        Recorder.Record("pass", dir);
    }
}

[TestClass]
public class FailRetain
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Failing()
    {
        string dir = TestContext.TestTempDirectory;
        File.WriteAllText(Path.Combine(dir, "artifact.txt"), "hello");
        Recorder.Record("fail", dir);
        Assert.Fail("Intentional failure to verify retention on failure.");
    }
}

[TestClass]
public class AddResultFileRetention
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void PassingWithAttachment()
    {
        string dir = TestContext.TestTempDirectory;
        string attachment = Path.Combine(dir, "attachment.txt");
        File.WriteAllText(attachment, "hello");
        TestContext.AddResultFile(attachment);
        Recorder.Record("addresultfile", attachment);
    }
}

[TestClass]
[DoNotParallelize]
public class RestrictiveUmask
{
    private const uint AllPermissions = 0x1FF;

    public TestContext TestContext { get; set; }

    [DllImport("libc", EntryPoint = "umask")]
    private static extern uint Umask(uint mask);

    [TestMethod]
    public void TestTempDirectoryRemainsUsable()
    {
        string tempDirectory;
        uint previousUmask = Umask(AllPermissions);
        try
        {
            tempDirectory = TestContext.TestTempDirectory;
            string writeCheckPath = Path.Combine(tempDirectory, "write-check.txt");
            File.WriteAllText(writeCheckPath, "data");
            Assert.IsTrue(File.Exists(writeCheckPath));
        }
        finally
        {
            Umask(previousUmask);
        }

        Recorder.Record("umask", tempDirectory);
    }
}

[TestClass]
public class DataRowCleanupFailure
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void Row(int i) => Recorder.Record("datarowcleanupfail", TestContext.TestTempDirectory);

    [TestCleanup]
    public void Cleanup() => throw new InvalidOperationException("Intentional cleanup failure.");
}

[TestClass]
public class Lazy
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void AccessorTest() => Recorder.Record("accessor", TestContext.TestTempDirectory);

    [TestMethod]
    public void NonAccessorTest() => Recorder.Record("nonaccessor", "NONE");
}
""";
    }
}
