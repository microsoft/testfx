// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Acceptance tests for the <c>[ResourceLock]</c> attribute. A single generated asset declares tests
/// that lock several named resources; a probe records, per resource key, the maximum number of test
/// bodies that were ever active simultaneously, plus a global maximum and a violation counter.
/// </summary>
/// <remarks>
/// The assertions are deterministic (each test body sleeps while active, and there are enough parallel
/// workers that any tests allowed to overlap will), verifying that: two <c>ReadWrite</c> locks on the
/// same key never overlap; unrelated tests still run concurrently; multiple <c>Read</c> locks on the
/// same key overlap each other; and a writer never overlaps any other holder of its key.
/// </remarks>
[TestClass]
public sealed class ResourceLockExecutionTests : AcceptanceTestBase<ResourceLockExecutionTests.TestAssetFixture>
{
    private const string ProjectName = "ResourceLockTestProject";

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ResourceLock_SerializesConflictingTests_WhileUnrelatedRunConcurrently(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(ProjectName, tfm);

        TestHostResult result = await testHost.ExecuteAsync("--output detailed", cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode, result.StandardOutput);
        Assert.Contains("succeeded: 14", result.StandardOutput);

        IReadOnlyDictionary<string, int> probe = AssetFixture.ReadProbe(ProjectName, tfm);

        // A writer never overlaps another holder of the same key, and a reader never overlaps a writer.
        Assert.AreEqual(0, probe["violations"], "no reader/writer overlap on any shared key");

        // Two ReadWrite locks on the same key are serialized: at most one is ever active.
        Assert.AreEqual(1, probe["key:W"], "ReadWrite locks on the same key are exclusive");

        // Read locks on the same key run concurrently with each other.
        Assert.IsGreaterThanOrEqualTo(2, probe["key:R"], "Read locks on the same key run concurrently");

        // The lock is held across TestInitialize/TestCleanup: two conflicting tests never overlap even
        // when their occupancy of the key spans their lifecycle methods (occupancy stays at 1). If the
        // lock only covered the test body, one test's cleanup would overlap the other's initialize.
        Assert.AreEqual(1, probe["key:LC"], "lock spans TestInitialize/TestCleanup");

        // Unrelated tests still run in parallel despite the locked tests.
        Assert.IsGreaterThanOrEqualTo(2, probe["global"], "unrelated tests are not blocked by locks");
    }

    public sealed class TestAssetFixture : ITestAssetFixture
    {
        private readonly TempDirectory _tempDirectory = new();
        private readonly Dictionary<string, TestAsset> _assets = [];

        public TestHost GetTestHost(string projectName, string tfm)
            => TestHost.LocateFrom(_assets[projectName].TargetAssetPath, projectName, tfm);

        public IReadOnlyDictionary<string, int> ReadProbe(string projectName, string tfm)
        {
            string probePath = Path.Combine(GetTestHost(projectName, tfm).DirectoryName, "LockProbe.txt");
            var values = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string line in File.ReadAllLines(probePath))
            {
                int separator = line.LastIndexOf('=');
                if (separator < 0)
                {
                    continue;
                }

                values[line.Substring(0, separator)] = int.Parse(line.Substring(separator + 1), CultureInfo.InvariantCulture);
            }

            return values;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            string patched = SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);
            TestAsset asset = await TestAsset.GenerateAssetAsync(ProjectName, patched, _tempDirectory);
            await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\" -c Release", callerMemberName: ProjectName, cancellationToken: cancellationToken);
            _assets.Add(ProjectName, asset);
        }

        public void Dispose()
        {
            foreach (TestAsset asset in _assets.Values)
            {
                asset.Dispose();
            }

            _tempDirectory.Dispose();
        }

        private const string SourceCode = """
#file ResourceLockTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file LockProbe.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

internal static class LockProbe
{
    private static readonly object s_gate = new object();
    private static readonly Dictionary<string, KeyState> s_keys = new Dictionary<string, KeyState>(StringComparer.Ordinal);
    private static int s_globalActive;
    private static int s_globalMax;
    private static int s_violations;

    private sealed class KeyState
    {
        public int Active;
        public int Max;
        public int Writers;
    }

    public static void Run(string key, bool isWriter)
    {
        Enter(key, isWriter);
        try
        {
            Thread.Sleep(500);
        }
        finally
        {
            Exit(key, isWriter);
        }
    }

    public static void Enter(string key, bool isWriter)
    {
        lock (s_gate)
        {
            if (!s_keys.TryGetValue(key, out KeyState state))
            {
                state = new KeyState();
                s_keys[key] = state;
            }

            state.Active++;
            if (state.Active > state.Max)
            {
                state.Max = state.Active;
            }

            if (isWriter)
            {
                state.Writers++;
            }

            // A writer must hold the key alone; a reader must never coexist with a writer.
            if (state.Writers > 0 && state.Active > 1)
            {
                s_violations++;
            }

            s_globalActive++;
            if (s_globalActive > s_globalMax)
            {
                s_globalMax = s_globalActive;
            }
        }
    }

    public static void Exit(string key, bool isWriter)
    {
        lock (s_gate)
        {
            KeyState state = s_keys[key];
            state.Active--;
            if (isWriter)
            {
                state.Writers--;
            }

            s_globalActive--;
        }
    }

    public static void WriteResult()
    {
        lock (s_gate)
        {
            var builder = new StringBuilder();
            builder.AppendLine("global=" + s_globalMax.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("violations=" + s_violations.ToString(CultureInfo.InvariantCulture));
            foreach (KeyValuePair<string, KeyState> entry in s_keys)
            {
                builder.AppendLine("key:" + entry.Key + "=" + entry.Value.Max.ToString(CultureInfo.InvariantCulture));
            }

            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "LockProbe.txt"), builder.ToString());
        }
    }
}

#file Tests.cs
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]

namespace ResourceLockTestProject;

[TestClass]
public class SharedWriterTests
{
    [TestMethod]
    [ResourceLock("W")]
    public void Writer1() => LockProbe.Run("W", isWriter: true);

    [TestMethod]
    [ResourceLock("W")]
    public void Writer2() => LockProbe.Run("W", isWriter: true);

    [TestMethod]
    [ResourceLock("W")]
    public void Writer3() => LockProbe.Run("W", isWriter: true);
}

[TestClass]
public class SharedReaderTests
{
    [TestMethod]
    [ResourceLock("R", Mode = ResourceAccessMode.Read)]
    public void Reader1() => LockProbe.Run("R", isWriter: false);

    [TestMethod]
    [ResourceLock("R", Mode = ResourceAccessMode.Read)]
    public void Reader2() => LockProbe.Run("R", isWriter: false);

    [TestMethod]
    [ResourceLock("R", Mode = ResourceAccessMode.Read)]
    public void Reader3() => LockProbe.Run("R", isWriter: false);
}

[TestClass]
public class MixedReaderWriterTests
{
    [TestMethod]
    [ResourceLock("RW", Mode = ResourceAccessMode.Read)]
    public void MixedReader1() => LockProbe.Run("RW", isWriter: false);

    [TestMethod]
    [ResourceLock("RW", Mode = ResourceAccessMode.Read)]
    public void MixedReader2() => LockProbe.Run("RW", isWriter: false);

    [TestMethod]
    [ResourceLock("RW")]
    public void MixedWriter() => LockProbe.Run("RW", isWriter: true);
}

[TestClass]
public class LifecycleLockTests
{
    // Occupancy of the "LC" key is taken in TestInitialize and released in TestCleanup, so it spans the
    // whole method lifecycle. With the ReadWrite lock held across initialize -> body -> cleanup, two
    // conflicting methods never overlap and the observed max concurrency for "LC" stays at 1. If the
    // lock only covered the body, one method's cleanup would overlap the other's initialize (concurrency
    // 2 -> a recorded violation).
    [TestInitialize]
    public void TestInitialize()
    {
        LockProbe.Enter("LC", isWriter: true);
        Thread.Sleep(300);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        Thread.Sleep(300);
        LockProbe.Exit("LC", isWriter: true);
    }

    [TestMethod]
    [ResourceLock("LC")]
    public void Lifecycle1() => Thread.Sleep(200);

    [TestMethod]
    [ResourceLock("LC")]
    public void Lifecycle2() => Thread.Sleep(200);
}

[TestClass]
public class IndependentTests
{
    [AssemblyCleanup]
    public static void AssemblyCleanup() => LockProbe.WriteResult();

    [TestMethod]
    public void Free1() => LockProbe.Run("free", isWriter: false);

    [TestMethod]
    public void Free2() => LockProbe.Run("free", isWriter: false);

    [TestMethod]
    public void Free3() => LockProbe.Run("free", isWriter: false);
}
""";
    }
}
