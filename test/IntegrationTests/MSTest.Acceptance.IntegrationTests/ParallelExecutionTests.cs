// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Acceptance-style rewrite of the former CLITestBase-based parallel execution tests. The parallel
/// test assets (method-level, class-level and do-not-parallelize) are generated inline, built, then
/// executed out-of-process through the MSTest runner (Microsoft.Testing.Platform) host.
/// </summary>
/// <remarks>
/// The generated assets record their maximum concurrent test count. This deterministically verifies
/// that method-level and class-level parallelization overlap tests while <see cref="DoNotParallelizeAttribute"/>
/// keeps execution serial, without relying on a flaky wall-clock threshold.
/// </remarks>
[TestClass]
public sealed class ParallelExecutionTests : AcceptanceTestBase<ParallelExecutionTests.TestAssetFixture>
{
    private const string MethodParallelProjectName = "ParallelMethodsTestProject";
    private const string ClassParallelProjectName = "ParallelClassesTestProject";
    private const string DoNotParallelizeProjectName = "DoNotParallelizeTestProject";
    private const string DoNotParallelizeOrderingProjectName = "DoNotParallelizeOrderingTestProject";

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AllMethodsShouldRunInParallel(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(MethodParallelProjectName, tfm);

        TestHostResult result = await testHost.ExecuteAsync("--output detailed", cancellationToken: TestContext.CancellationToken);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);
        Assert.Contains("failed: 2", result.StandardOutput);
        Assert.Contains("succeeded: 4", result.StandardOutput);
        Assert.Contains("SimpleTest12", result.StandardOutput);
        Assert.Contains("SimpleTest22", result.StandardOutput);
        Assert.IsGreaterThanOrEqualTo(2, AssetFixture.ReadMaximumConcurrency(MethodParallelProjectName, tfm));
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AllClassesShouldRunInParallel(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(ClassParallelProjectName, tfm);

        TestHostResult result = await testHost.ExecuteAsync("--output detailed", cancellationToken: TestContext.CancellationToken);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);
        Assert.Contains("failed: 3", result.StandardOutput);
        Assert.Contains("succeeded: 4", result.StandardOutput);
        Assert.Contains("SimpleTest12", result.StandardOutput);
        Assert.Contains("SimpleTest22", result.StandardOutput);
        Assert.Contains("SimpleTest32", result.StandardOutput);
        Assert.IsGreaterThanOrEqualTo(2, AssetFixture.ReadMaximumConcurrency(ClassParallelProjectName, tfm));
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task NothingShouldRunInParallel(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(DoNotParallelizeProjectName, tfm);

        string settingsPath = AssetFixture.GetRunSettingsPath(DoNotParallelizeProjectName);
        TestHostResult result = await testHost.ExecuteAsync($"--settings \"{settingsPath}\" --output detailed", cancellationToken: TestContext.CancellationToken);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);
        Assert.Contains("failed: 2", result.StandardOutput);
        Assert.Contains("succeeded: 3", result.StandardOutput);
        Assert.Contains("SimpleTest12", result.StandardOutput);
        Assert.Contains("SimpleTest22", result.StandardOutput);
        Assert.AreEqual(1, AssetFixture.ReadMaximumConcurrency(DoNotParallelizeProjectName, tfm));
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DoNotParallelizeTestRunsAfterAllParallelizableTests(string tfm)
    {
        // Pins the defer-to-end scheduling of [DoNotParallelize]: the parallelizable tests each
        // increment a shared counter as they complete, and the single [DoNotParallelize] test
        // asserts the counter already equals the parallelizable count when it runs. The scheduler
        // awaits the whole parallelizable set before starting the non-parallelizable set, so this is
        // deterministic and does not rely on any wall-clock timing.
        TestHost testHost = AssetFixture.GetTestHost(DoNotParallelizeOrderingProjectName, tfm);

        TestHostResult result = await testHost.ExecuteAsync("--output detailed", cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode, result.StandardOutput);
        Assert.Contains("succeeded: 5", result.StandardOutput);
    }

    public sealed class TestAssetFixture : ITestAssetFixture
    {
        private readonly TempDirectory _tempDirectory = new();
        private readonly Dictionary<string, TestAsset> _assets = [];

        public TestHost GetTestHost(string projectName, string tfm)
            => TestHost.LocateFrom(_assets[projectName].TargetAssetPath, projectName, tfm);

        public string GetRunSettingsPath(string projectName)
            => Path.Combine(_assets[projectName].TargetAssetPath, "parallel.runsettings");

        public int ReadMaximumConcurrency(string projectName, string tfm)
        {
            string probePath = Path.Combine(GetTestHost(projectName, tfm).DirectoryName, "ParallelProbe.txt");
            return int.Parse(File.ReadAllText(probePath), CultureInfo.InvariantCulture);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            foreach ((string projectName, string code) in new[]
            {
                (MethodParallelProjectName, MethodParallelSourceCode),
                (ClassParallelProjectName, ClassParallelSourceCode),
                (DoNotParallelizeProjectName, DoNotParallelizeSourceCode),
                (DoNotParallelizeOrderingProjectName, DoNotParallelizeOrderingSourceCode),
            })
            {
                string patched = code
                    .PatchTargetFrameworks(TargetFrameworks.All)
                    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);
                TestAsset asset = await TestAsset.GenerateAssetAsync(projectName, patched, _tempDirectory);
                await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\" -c Release", callerMemberName: projectName, cancellationToken: cancellationToken);
                _assets.Add(projectName, asset);
            }
        }

        public void Dispose()
        {
            foreach (TestAsset asset in _assets.Values)
            {
                asset.Dispose();
            }

            _tempDirectory.Dispose();
        }

        private const string ProjectFile = """
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
""";

        private const string ParallelProbeSourceCode = """

#file ParallelProbe.cs
using System;
using System.Globalization;
using System.IO;
using System.Threading;

internal static class ParallelProbe
{
    private static int s_active;
    private static int s_maximum;

    public static void Run(Action assertion)
    {
        int active = Interlocked.Increment(ref s_active);
        int maximum;
        while (active > (maximum = Volatile.Read(ref s_maximum))
            && Interlocked.CompareExchange(ref s_maximum, active, maximum) != maximum)
        {
        }

        try
        {
            Thread.Sleep(1000);
        }
        finally
        {
            Interlocked.Decrement(ref s_active);
        }

        assertion();
    }

    public static void WriteResult()
        => File.WriteAllText(
            Path.Combine(AppContext.BaseDirectory, "ParallelProbe.txt"),
            Volatile.Read(ref s_maximum).ToString(CultureInfo.InvariantCulture));
}
""";

        private static readonly string MethodParallelSourceCode = ProjectFile.Replace("$ProjectName$", MethodParallelProjectName) + ParallelProbeSourceCode + """

#file Tests.cs
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)]

namespace ParallelMethodsTestProject;

internal static class Constants
{
    internal const int WaitTimeInMS = 1000;
}

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void SimpleTest11()
        => ParallelProbe.Run(() => Assert.AreEqual(1, 1));

    [TestMethod]
    public void SimpleTest12()
        => ParallelProbe.Run(() => Assert.Fail());

    [TestMethod]
    public void SimpleTest13()
        => ParallelProbe.Run(() => Assert.AreEqual(1, 1));
}

[TestClass]
public class UnitTest2
{
    private static bool s_assemblyInitCalled;
    private static bool s_assemblyCleanCalled;
    private static bool s_classInitCalled;
    private static bool s_classCleanCalled;

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        Assert.IsFalse(s_assemblyInitCalled);
        s_assemblyInitCalled = true;
    }

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        Assert.IsFalse(s_classInitCalled);
        s_classInitCalled = true;
    }

    [TestInitialize]
    public void Initialize()
    {
    }

    [TestCleanup]
    public void Cleanup()
    {
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        Assert.IsFalse(s_classCleanCalled);
        s_classCleanCalled = true;
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        Assert.IsFalse(s_assemblyCleanCalled);
        s_assemblyCleanCalled = true;
        ParallelProbe.WriteResult();
    }

    [TestMethod]
    public void SimpleTest21()
        => ParallelProbe.Run(() => Assert.AreEqual(0, 0));

    [TestMethod]
    public void SimpleTest22()
        => ParallelProbe.Run(() => Assert.Fail());

    [TestMethod]
    [DoNotParallelize]
    public void IsolatedTest()
        => ParallelProbe.Run(() => Assert.IsTrue(true));
}
""";

        private static readonly string ClassParallelSourceCode = ProjectFile.Replace("$ProjectName$", ClassParallelProjectName) + ParallelProbeSourceCode + """

#file Tests.cs
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 2, Scope = ExecutionScope.ClassLevel)]

namespace ParallelClassesTestProject;

internal static class Constants
{
    internal const int WaitTimeInMS = 1000;
}

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void SimpleTest11()
        => ParallelProbe.Run(() => Assert.AreEqual(1, 1));

    [TestMethod]
    public void SimpleTest12()
        => ParallelProbe.Run(() => Assert.Fail());
}

[TestClass]
public class UnitTest2
{
    private static bool s_assemblyInitCalled;
    private static bool s_assemblyCleanCalled;
    private static bool s_classInitCalled;
    private static bool s_classCleanCalled;

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        Assert.IsFalse(s_assemblyInitCalled);
        s_assemblyInitCalled = true;
    }

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        Assert.IsFalse(s_classInitCalled);
        s_classInitCalled = true;
    }

    [TestInitialize]
    public void Initialize()
    {
    }

    [TestCleanup]
    public void Cleanup()
    {
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        Assert.IsFalse(s_classCleanCalled);
        s_classCleanCalled = true;
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        Assert.IsFalse(s_assemblyCleanCalled);
        s_assemblyCleanCalled = true;
        ParallelProbe.WriteResult();
    }

    [TestMethod]
    public void SimpleTest21()
        => ParallelProbe.Run(() => Assert.AreEqual(0, 0));

    [TestMethod]
    public void SimpleTest22()
        => ParallelProbe.Run(() => Assert.Fail());

    [TestMethod]
    [DoNotParallelize]
    public void IsolatedTest()
        => ParallelProbe.Run(() => Assert.IsTrue(true));
}

[TestClass]
public class UnitTest3
{
    [TestMethod]
    public void SimpleTest31()
        => ParallelProbe.Run(() => Assert.AreEqual(1, 1));

    [TestMethod]
    public void SimpleTest32()
        => ParallelProbe.Run(() => Assert.Fail());
}
""";

        private static readonly string DoNotParallelizeSourceCode = ProjectFile.Replace("$ProjectName$", DoNotParallelizeProjectName) + ParallelProbeSourceCode + """

#file Tests.cs
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace DoNotParallelizeTestProject;

internal static class Constants
{
    internal const int WaitTimeInMS = 1000;
}

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void SimpleTest11()
        => ParallelProbe.Run(() => Assert.AreEqual(1, 1));

    [TestMethod]
    public void SimpleTest12()
        => ParallelProbe.Run(() => Assert.Fail());

    [TestMethod]
    public void SimpleTest13()
        => ParallelProbe.Run(() => Assert.AreEqual(1, 1));
}

[TestClass]
public class UnitTest2
{
    [AssemblyCleanup]
    public static void AssemblyCleanup()
        => ParallelProbe.WriteResult();

    [TestMethod]
    public void SimpleTest21()
        => ParallelProbe.Run(() => Assert.AreEqual(1, 1));

    [TestMethod]
    public void SimpleTest22()
        => ParallelProbe.Run(() => Assert.Fail());
}

#file parallel.runsettings
<RunSettings>
  <MSTest>
    <Parallelize>
      <Workers>4</Workers>
      <Scope>ClassLevel</Scope>
    </Parallelize>
  </MSTest>
</RunSettings>
""";

        private static readonly string DoNotParallelizeOrderingSourceCode = ProjectFile.Replace("$ProjectName$", DoNotParallelizeOrderingProjectName) + """

#file Tests.cs
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 3, Scope = ExecutionScope.MethodLevel)]

namespace DoNotParallelizeOrderingTestProject;

internal static class OrderTracker
{
    internal const int ParallelizableCount = 4;
    private static int s_completedParallelizable;

    internal static void MarkParallelizableCompleted()
        => Interlocked.Increment(ref s_completedParallelizable);

    internal static int CompletedParallelizable => Volatile.Read(ref s_completedParallelizable);
}

[TestClass]
public class ParallelTests
{
    [TestMethod]
    public void Parallel1() => OrderTracker.MarkParallelizableCompleted();

    [TestMethod]
    public void Parallel2() => OrderTracker.MarkParallelizableCompleted();

    [TestMethod]
    public void Parallel3() => OrderTracker.MarkParallelizableCompleted();

    [TestMethod]
    public void Parallel4() => OrderTracker.MarkParallelizableCompleted();
}

[TestClass]
public class DeferredTests
{
    // Marked [DoNotParallelize], so this test is deferred to run after every parallelizable test in
    // the source has completed; the shared counter must therefore already equal the full
    // parallelizable count by the time it runs.
    [TestMethod]
    [DoNotParallelize]
    public void DeferredRunsAfterAllParallelizableTests()
        => Assert.AreEqual(
            OrderTracker.ParallelizableCount,
            OrderTracker.CompletedParallelizable,
            "The [DoNotParallelize] test must run only after every parallelizable test has completed.");
}
""";
    }
}
