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
    private const string ClassLevelProjectName = "ResourceLockClassLevelTestProject";

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

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ResourceLock_UnderClassLevelScope_LocksWholeClassChunk_AndMergesModes(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(ClassLevelProjectName, tfm);

        TestHostResult result = await testHost.ExecuteAsync("--output detailed", cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode, result.StandardOutput);
        Assert.Contains("succeeded: 13", result.StandardOutput);

        IReadOnlyDictionary<string, int> probe = AssetFixture.ReadProbe(ClassLevelProjectName, tfm);

        Assert.AreEqual(0, probe["violations"], "no reader/writer overlap on any shared key");

        // Four classes (one per worker) declare a class-level ReadWrite lock on the same key. They must be
        // fully serialized against each other, and saturating every worker on one key must not deadlock or
        // lose tests - the run still completes with all tests passing.
        Assert.AreEqual(1, probe["key:CL"], "class-level ReadWrite locks on the same key are exclusive");

        // Chunk-level mode merging: one class declares Read on "MK" on one method and ReadWrite on another.
        // Under ClassLevel the chunk unions both and must take the strongest mode, so it excludes the
        // separate reader class. If the chunk took only Read, the reader would overlap it and this would be 2.
        Assert.AreEqual(1, probe["key:MK"], "a ReadWrite method promotes the whole class chunk's lock on that key");

        // Unlocked classes still run alongside the locked ones.
        Assert.IsGreaterThanOrEqualTo(2, probe["global"], "unrelated classes are not blocked by locks");
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
            await GenerateAsync(ProjectName, SourceCode, cancellationToken).ConfigureAwait(false);
            await GenerateAsync(ClassLevelProjectName, ClassLevelSourceCode, cancellationToken).ConfigureAwait(false);
        }

        private async Task GenerateAsync(string projectName, string sourceCode, CancellationToken cancellationToken)
        {
            string patched = sourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$ProjectName$", projectName);
            TestAsset asset = await TestAsset.GenerateAssetAsync(projectName, patched, _tempDirectory);
            await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\" -c Release", callerMemberName: projectName, cancellationToken: cancellationToken);
            _assets.Add(projectName, asset);
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
#file $ProjectName$.csproj
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
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        // This asset's assertions are lower bounds - Read locks and unlocked tests must be observed running
        // *concurrently* - so they need enough runnable pool threads, not just enough workers. A chunk blocked
        // on a lock awaits and releases its thread, but a running body pins one for its Thread.Sleep. Min
        // threads defaults to Environment.ProcessorCount and injection beyond that is throttled, so on a
        // low-core agent the extra threads may not arrive before the sleeps end and these assertions would
        // fail even though locking is correct. Raising the minimum removes that dependency, matching the
        // class-level asset below.
        _ = context;
        ThreadPool.SetMinThreads(16, 16);
    }

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

        /// <summary>
        /// A second asset running under <c>ExecutionScope.ClassLevel</c> - the default scope, and the only one
        /// where a scheduling chunk spans more than one test. It covers what the method-level asset cannot:
        /// class-level <c>[ResourceLock]</c> declarations, and the merging of several methods' locks into one
        /// chunk lock with the strongest mode winning.
        /// </summary>
        private const string ClassLevelSourceCode = """
#file $ProjectName$.csproj
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
            Thread.Sleep(200);
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

// IMPORTANT: this asset's concurrency is load-bearing, not arbitrary tuning. Two things must hold for the
// "key:MK" merge assertion to be meaningful rather than vacuous:
//
//   1. Workers MUST stay greater than the number of classes contending on the "CL" key below (currently 4).
//      A blocked chunk occupies its worker, so if Workers <= the contending-class count those classes consume
//      every worker and MergePromoteTests / MergeReaderTests can never overlap. Verified: with Workers = 4 a
//      deliberately broken merge still passed; with Workers = 8 it is caught.
//   2. Enough runnable thread-pool threads must exist for those two classes to run at once - see
//      AssemblyInitialize below, which raises the pool minimum. Worker count alone is not sufficient, because
//      a chunk waiting on a lock awaits and releases its pool thread while a running body pins one.
//
// If either is weakened, mode merging can break completely while this test still passes. Do not lower them.
[assembly: Parallelize(Workers = 8, Scope = ExecutionScope.ClassLevel)]

namespace ResourceLockClassLevelTestProject;

// Four classes contend on a single class-level ReadWrite key. This proves class-chunk exclusivity and
// exercises the blocked-worker path: the run must still complete with no lost or deadlocked tests.
// Deliberately no assertion about workers being starved - that would be timing-dependent, and it would
// also fail if lock-aware dispatch is implemented later, penalizing a fix for removing a limitation.
[TestClass]
[ResourceLock("CL")]
public class ClassLockedA
{
    [TestMethod]
    public void A1() => LockProbe.Run("CL", isWriter: true);

    [TestMethod]
    public void A2() => LockProbe.Run("CL", isWriter: true);
}

[TestClass]
[ResourceLock("CL")]
public class ClassLockedB
{
    [TestMethod]
    public void B1() => LockProbe.Run("CL", isWriter: true);

    [TestMethod]
    public void B2() => LockProbe.Run("CL", isWriter: true);
}

[TestClass]
[ResourceLock("CL")]
public class ClassLockedC
{
    [TestMethod]
    public void C1() => LockProbe.Run("CL", isWriter: true);

    [TestMethod]
    public void C2() => LockProbe.Run("CL", isWriter: true);
}

[TestClass]
[ResourceLock("CL")]
public class ClassLockedD
{
    [TestMethod]
    public void D1() => LockProbe.Run("CL", isWriter: true);

    [TestMethod]
    public void D2() => LockProbe.Run("CL", isWriter: true);
}

// Mode merging within one chunk: this class declares Read on "MK" for one method and ReadWrite for
// another. Under ClassLevel both are unioned into the chunk's lock set and the strongest mode wins, so
// the whole class holds "MK" exclusively - excluding MergeReaderTests below. Both methods therefore probe
// as writers. Were the merge to keep only Read, MergeReaderTests would overlap and key:MK would reach 2.
[TestClass]
public class MergePromoteTests
{
    [TestMethod]
    [ResourceLock("MK", Mode = ResourceAccessMode.Read)]
    public void MergeRead() => LockProbe.Run("MK", isWriter: true);

    [TestMethod]
    [ResourceLock("MK")]
    public void MergeWrite() => LockProbe.Run("MK", isWriter: true);
}

[TestClass]
[ResourceLock("MK", Mode = ResourceAccessMode.Read)]
public class MergeReaderTests
{
    [TestMethod]
    public void ReaderOnly() => LockProbe.Run("MK", isWriter: false);
}

[TestClass]
public class IndependentClassTests
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        // Keep this asset's observable concurrency independent of thread-pool injection heuristics.
        // Concurrency here is bounded by runnable pool threads, not by Workers: a chunk blocked on a lock
        // awaits and releases its pool thread, while a *running* test body pins one for its Thread.Sleep.
        // Min threads defaults to Environment.ProcessorCount and injection beyond that is throttled, so on a
        // low-core CI agent the assertions could in principle observe less overlap than intended. Raising the
        // minimum removes that variable at no cost to run time.
        _ = context;
        ThreadPool.SetMinThreads(16, 16);
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup() => LockProbe.WriteResult();

    [TestMethod]
    public void Free1() => LockProbe.Run("free", isWriter: false);

    [TestMethod]
    public void Free2() => LockProbe.Run("free", isWriter: false);
}
""";
    }
}
