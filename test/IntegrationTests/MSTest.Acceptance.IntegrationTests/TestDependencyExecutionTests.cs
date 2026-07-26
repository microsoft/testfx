// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Acceptance tests for <c>[DependsOn]</c> and the dependency chain file. The generated assets record, for
/// every test body, the millisecond at which it entered and left, which makes both claims checkable end to
/// end: that a dependent never starts before its prerequisite finishes, and - the point of a graph rather
/// than a flat order - that two tests sharing a prerequisite really do overlap.
/// </summary>
/// <remarks>
/// The assets are shared between the test methods of this class, so it is marked
/// <see cref="DoNotParallelizeAttribute"/>: the chain-file test writes into the test host directory.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class TestDependencyExecutionTests : AcceptanceTestBase<TestDependencyExecutionTests.TestAssetFixture>
{
    private const string GraphProjectName = "TestDependencyGraphTestProject";
    private const string FailureProjectName = "TestDependencyFailureTestProject";
    private const string CycleProjectName = "TestDependencyCycleTestProject";
    private const string ChainFileProjectName = "TestDependencyChainFileTestProject";

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DependsOn_RunsPrerequisitesFirst_AndLetsIndependentBranchesOverlap(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(GraphProjectName, tfm);

        TestHostResult result = await testHost.ExecuteAsync("--output detailed", cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode, result.StandardOutput);
        Assert.Contains("succeeded: 4", result.StandardOutput);

        IReadOnlyDictionary<string, (int Enter, int Exit)> spans = AssetFixture.ReadProbe(GraphProjectName, tfm);

        // Ordering: a dependent may not start before its prerequisite has finished.
        Assert.IsGreaterThanOrEqualTo(spans["Root"].Exit, spans["BranchA"].Enter, "BranchA started before Root finished");
        Assert.IsGreaterThanOrEqualTo(spans["Root"].Exit, spans["BranchB"].Enter, "BranchB started before Root finished");
        Assert.IsGreaterThanOrEqualTo(spans["BranchA"].Exit, spans["Join"].Enter, "Join started before BranchA finished");
        Assert.IsGreaterThanOrEqualTo(spans["BranchB"].Exit, spans["Join"].Enter, "Join started before BranchB finished");

        // Fan-out: the two branches share a prerequisite but not each other, so they must be allowed to run at
        // the same time. This is what a dependency graph buys over a flat ordering attribute, which would have
        // serialized them.
        bool branchesOverlap = spans["BranchA"].Enter < spans["BranchB"].Exit && spans["BranchB"].Enter < spans["BranchA"].Exit;
        Assert.IsTrue(branchesOverlap, $"BranchA {spans["BranchA"]} and BranchB {spans["BranchB"]} did not overlap");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DependsOn_WhenAPrerequisiteFails_SkipsDependentsTransitively_ButHonorsProceedOnFailure(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(FailureProjectName, tfm);

        TestHostResult result = await testHost.ExecuteAsync("--output detailed", cancellationToken: TestContext.CancellationToken);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);

        // One real failure, its two (direct and transitive) dependents skipped rather than failed, and the two
        // tests that must still run - the ProceedOnFailure audit step and the unrelated test - passing.
        Assert.Contains("failed: 1", result.StandardOutput);
        Assert.Contains("skipped: 2", result.StandardOutput);
        Assert.Contains("succeeded: 2", result.StandardOutput);

        // The skip must say why, otherwise a skipped test is indistinguishable from one that was filtered out.
        Assert.Contains("Failing", result.StandardOutput);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DependsOn_WhenDependenciesFormACycle_FailsTheTestsInTheCycle_AndStillRunsTheRest(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(CycleProjectName, tfm);

        TestHostResult result = await testHost.ExecuteAsync("--output detailed", cancellationToken: TestContext.CancellationToken);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);

        // The cycle is a configuration error, so it is loud: the two tests in it fail with the cycle path, and
        // the unrelated test still runs, so one bad declaration does not cost the whole run.
        Assert.Contains("failed: 2", result.StandardOutput);
        Assert.Contains("succeeded: 1", result.StandardOutput);
        Assert.Contains("Dependency cycle detected", result.StandardOutput);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DependencyChainFile_OrdersTestsThatDeclareNoAttribute(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(ChainFileProjectName, tfm);

        // The chain file and the runsettings that points at it are written here rather than shipped with the
        // asset so that the path can be absolute, which keeps the test independent of the host's working
        // directory.
        string chainFilePath = Path.Combine(testHost.DirectoryName, "chain.xml");
        string runSettingsPath = Path.Combine(testHost.DirectoryName, "chain.runsettings");
        string probePath = Path.Combine(testHost.DirectoryName, "OrderProbe.txt");
        File.Delete(probePath);

        File.WriteAllText(chainFilePath, """
<TestDependencies>
  <Chain>
    <Test name="Contoso.Tests.StepOne.First" />
    <Test name="Contoso.Tests.StepTwo.Second" />
    <Test name="Contoso.Tests.StepThree.Third" />
  </Chain>
</TestDependencies>
""");

        File.WriteAllText(runSettingsPath, $"""
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <MSTest>
    <TestDependencyChainFile>{chainFilePath}</TestDependencyChainFile>
  </MSTest>
</RunSettings>
""");

        TestHostResult result = await testHost.ExecuteAsync($"--settings \"{runSettingsPath}\"", cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode, result.StandardOutput);

        // The classes are declared in the reverse of the required order and carry no attribute at all, so only
        // the chain file can be responsible for the observed order.
        string[] order = File.ReadAllLines(probePath).Where(l => l.Length > 0).ToArray();
        Assert.AreEqual(3, order.Length, string.Join(",", order));
        CollectionAssert.AreEqual(new[] { "First", "Second", "Third" }, order);
    }

    public sealed class TestAssetFixture : ITestAssetFixture
    {
        private readonly TempDirectory _tempDirectory = new();
        private readonly Dictionary<string, TestAsset> _assets = [];

        public TestHost GetTestHost(string projectName, string tfm)
            => TestHost.LocateFrom(_assets[projectName].TargetAssetPath, projectName, tfm);

        /// <summary>
        /// Reads the enter/exit millisecond of each recorded test body from the probe file written by the
        /// generated asset.
        /// </summary>
        public IReadOnlyDictionary<string, (int Enter, int Exit)> ReadProbe(string projectName, string tfm)
        {
            string probePath = Path.Combine(GetTestHost(projectName, tfm).DirectoryName, "SpanProbe.txt");
            var spans = new Dictionary<string, (int Enter, int Exit)>(StringComparer.Ordinal);
            foreach (string line in File.ReadAllLines(probePath))
            {
                string[] parts = line.Split(',');
                if (parts.Length == 3)
                {
                    spans[parts[0]] = (
                        int.Parse(parts[1], CultureInfo.InvariantCulture),
                        int.Parse(parts[2], CultureInfo.InvariantCulture));
                }
            }

            return spans;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await GenerateAsync(GraphProjectName, GraphSourceCode, cancellationToken).ConfigureAwait(false);
            await GenerateAsync(FailureProjectName, FailureSourceCode, cancellationToken).ConfigureAwait(false);
            await GenerateAsync(CycleProjectName, CycleSourceCode, cancellationToken).ConfigureAwait(false);
            await GenerateAsync(ChainFileProjectName, ChainFileSourceCode, cancellationToken).ConfigureAwait(false);
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

        private const string GraphSourceCode = ProjectFile + """
#file SpanProbe.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

// Records when each test body ran, so the acceptance test can check both the ordering guarantee and the
// overlap that proves independent branches were not serialized.
internal static class SpanProbe
{
    private static readonly Stopwatch s_clock = Stopwatch.StartNew();
    private static readonly object s_gate = new object();
    private static readonly List<string> s_spans = new List<string>();

    public static void Run(string name, int milliseconds)
    {
        int enter = (int)s_clock.ElapsedMilliseconds;
        Thread.Sleep(milliseconds);
        int exit = (int)s_clock.ElapsedMilliseconds;
        lock (s_gate)
        {
            s_spans.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", name, enter, exit));
        }
    }

    public static void Flush()
    {
        lock (s_gate)
        {
            File.WriteAllText("SpanProbe.txt", string.Join(Environment.NewLine, s_spans));
        }
    }
}

[TestClass]
public sealed class ProbeLifecycle
{
    [AssemblyCleanup]
    public static void Flush() => SpanProbe.Flush();
}

#file Parallelization.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

// MethodLevel makes every test its own scheduling unit, which is what allows the two branches of the fan-out
// to be handed to different workers. Four workers guarantee there is capacity for them to overlap.
[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]

#file GraphTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class GraphTests
{
    [TestMethod]
    public void Root() => SpanProbe.Run("Root", 300);

    [TestMethod]
    [DependsOn(nameof(Root))]
    public void BranchA() => SpanProbe.Run("BranchA", 800);

    [TestMethod]
    [DependsOn(nameof(Root))]
    public void BranchB() => SpanProbe.Run("BranchB", 800);

    [TestMethod]
    [DependsOn(nameof(BranchA))]
    [DependsOn(nameof(BranchB))]
    public void Join() => SpanProbe.Run("Join", 50);
}
""";

        private const string FailureSourceCode = ProjectFile + """
#file Parallelization.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]

#file FailureTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class FailureTests
{
    [TestMethod]
    public void Failing() => Assert.Fail("deliberate failure");

    [TestMethod]
    [DependsOn(nameof(Failing))]
    public void DirectDependent() { }

    [TestMethod]
    [DependsOn(nameof(DirectDependent))]
    public void TransitiveDependent() { }

    // Must run even though its prerequisite failed: this is the audit/cleanup escape hatch.
    [TestMethod]
    [DependsOn(nameof(Failing), ProceedOnFailure = true)]
    public void Audit() { }

    [TestMethod]
    public void Unrelated() { }
}
""";

        private const string CycleSourceCode = ProjectFile + """
#file CycleTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class CycleTests
{
    [TestMethod]
    [DependsOn(nameof(Two))]
    public void One() { }

    [TestMethod]
    [DependsOn(nameof(One))]
    public void Two() { }

    [TestMethod]
    public void Unrelated() { }
}
""";

        private const string ChainFileSourceCode = ProjectFile + """
#file OrderProbe.cs
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

internal static class OrderProbe
{
    private static readonly object s_gate = new object();

    public static void Record(string name)
    {
        lock (s_gate)
        {
            File.AppendAllText("OrderProbe.txt", name + "\n");
        }
    }
}

#file Steps.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Contoso.Tests;

// Declared in the reverse of the order the chain file asks for, and carrying no dependency attribute, so a
// correct run can only be explained by the file.
[TestClass]
public sealed class StepThree
{
    [TestMethod]
    public void Third() => OrderProbe.Record("Third");
}

[TestClass]
public sealed class StepTwo
{
    [TestMethod]
    public void Second() => OrderProbe.Record("Second");
}

[TestClass]
public sealed class StepOne
{
    [TestMethod]
    public void First() => OrderProbe.Record("First");
}
""";
    }
}
