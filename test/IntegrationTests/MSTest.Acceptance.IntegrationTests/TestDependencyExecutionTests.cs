// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Acceptance tests for <c>[DependsOn]</c> and its <c>testconfig.json</c> equivalent. The generated assets
/// record, for every test body, the millisecond at which it entered and left, which makes both claims
/// checkable end to end: that a dependent never starts before its prerequisite finishes, and - the point of
/// a graph rather than a flat order - that two tests sharing a prerequisite really do overlap.
/// </summary>
/// <remarks>
/// The overlap claim is asserted by a bounded rendezvous inside the asset rather than by comparing elapsed
/// times, so it does not depend on how busy the machine is and this class needs no isolation from the rest
/// of the suite. Each test method also generates and runs its own project, so nothing is shared between them.
/// </remarks>
[TestClass]
public sealed class TestDependencyExecutionTests : AcceptanceTestBase<TestDependencyExecutionTests.TestAssetFixture>
{
    private const string GraphProjectName = "TestDependencyGraphTestProject";
    private const string FailureProjectName = "TestDependencyFailureTestProject";
    private const string CycleProjectName = "TestDependencyCycleTestProject";
    private const string TestConfigProjectName = "TestDependencyConfigTestProject";

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
        //
        // Asserted by rendezvous rather than by comparing timestamps: each branch signals its own arrival and
        // then waits for the other, so the run only completes if both were genuinely dispatched concurrently.
        // Inferring overlap from elapsed times would be a race - under CI load the second branch might not
        // start before the first one's sleep elapsed, failing a correct scheduler. If the branches are
        // serialized the barrier is never satisfied, the waits hit their timeout, and the asset fails the
        // tests rather than this assertion having to notice a suspicious ordering.
        Assert.Contains("BranchA:rendezvous-ok", result.StandardOutput, "BranchA did not overlap BranchB");
        Assert.Contains("BranchB:rendezvous-ok", result.StandardOutput, "BranchB did not overlap BranchA");
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
    public async Task DependencyConfiguration_OrdersTestsThatDeclareNoAttribute(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(TestConfigProjectName, tfm);

        string probePath = Path.Combine(testHost.DirectoryName, "OrderProbe.txt");
        File.Delete(probePath);

        // The asset ships a testconfig.json declaring the order; no test carries an attribute, and the
        // classes are declared in source in the reverse of the required order.
        string testConfigPath = Path.Combine(testHost.DirectoryName, "pipeline.testconfig.json");

        TestHostResult result = await testHost.ExecuteAsync($"--config-file \"{testConfigPath}\"", cancellationToken: TestContext.CancellationToken);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);

        // 'chains' ordered First -> Second -> Third, and 'nodes' hung Verify and Audit off Second.
        // Second fails, so the skip propagates to everything downstream that did not opt out: Third (next
        // in the chain) and Verify (a node). Audit runs anyway because its node set proceedOnFailure, and
        // First already ran. Hence 1 failed, 2 skipped, 2 succeeded.
        Assert.Contains("failed: 1", result.StandardOutput);
        Assert.Contains("skipped: 2", result.StandardOutput);
        Assert.Contains("succeeded: 2", result.StandardOutput);

        // Both skips name the prerequisite that did not pass.
        Assert.Contains("skipped Verify", result.StandardOutput);
        Assert.Contains("skipped Third", result.StandardOutput);
        Assert.Contains("Test skipped because it depends on 'Contoso.Tests.StepTwo.Second', which did not pass.", result.StandardOutput);

        string[] order = [.. File.ReadAllLines(probePath).Where(l => l.Length > 0)];

        // The classes are declared in source in the reverse of this order and carry no attribute, so only
        // the configuration can be responsible for First running before Second.
        CollectionAssert.AreEqual(new[] { "First", "Second" }, order.Take(2).ToArray(), string.Join(",", order));

        // Neither skipped test executed its body.
        CollectionAssert.DoesNotContain(order, "Verify");
        CollectionAssert.DoesNotContain(order, "Third");

        // Audit ran anyway, because its node set proceedOnFailure.
        CollectionAssert.Contains(order, "Audit");
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
            await GenerateAsync(TestConfigProjectName, TestConfigSourceCode, cancellationToken).ConfigureAwait(false);
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

    private static readonly Dictionary<string, int> s_entered = new Dictionary<string, int>(StringComparer.Ordinal);

    public static void Enter(string name)
    {
        lock (s_gate)
        {
            s_entered[name] = (int)s_clock.ElapsedMilliseconds;
        }
    }

    public static void Exit(string name)
    {
        lock (s_gate)
        {
            s_spans.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", name, s_entered[name], (int)s_clock.ElapsedMilliseconds));
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
using System;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class GraphTests
{
    // The two branches rendezvous instead of sleeping a fixed span: each signals its arrival and waits for
    // the other, so concurrent dispatch is proven rather than inferred from timestamps. A generous timeout
    // keeps a serialized scheduler from hanging the run - it fails the test instead.
    private static readonly CountdownEvent Rendezvous = new(2);

    [TestMethod]
    public void Root() => SpanProbe.Run("Root", 300);

    [TestMethod]
    [DependsOn(nameof(Root))]
    public void BranchA() => RunBranch("BranchA");

    [TestMethod]
    [DependsOn(nameof(Root))]
    public void BranchB() => RunBranch("BranchB");

    [TestMethod]
    [DependsOn(nameof(BranchA))]
    [DependsOn(nameof(BranchB))]
    public void Join() => SpanProbe.Run("Join", 50);

    private static void RunBranch(string name)
    {
        SpanProbe.Enter(name);
        Rendezvous.Signal();
        bool met = Rendezvous.Wait(TimeSpan.FromSeconds(60));
        SpanProbe.Exit(name);

        Assert.IsTrue(met, name + " timed out waiting for the other branch, so the two never ran concurrently.");
        Console.WriteLine(name + ":rendezvous-ok");
    }
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

        private const string TestConfigSourceCode = """
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

  <ItemGroup>
    <None Update="pipeline.testconfig.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>

#file pipeline.testconfig.json
{
  "mstest": {
    "execution": {
      "dependencies": {
        "chains": [
          [
            "Contoso.Tests.StepOne.First",
            "Contoso.Tests.StepTwo.Second",
            "Contoso.Tests.StepThree.Third"
          ]
        ],
        "nodes": [
          {
            "test": "Contoso.Tests.VerifyStep.Verify",
            "dependsOn": [ "Contoso.Tests.StepTwo.Second" ]
          },
          {
            "test": "Contoso.Tests.AuditStep.Audit",
            "dependsOn": [ "Contoso.Tests.StepTwo.Second" ],
            "proceedOnFailure": true
          }
        ]
      }
    }
  }
}

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

// Declared in the reverse of the order the configuration asks for, and carrying no dependency attribute,
// so a correct run can only be explained by testconfig.json.
[TestClass]
public sealed class AuditStep
{
    // Runs even though its prerequisite fails, because its node sets proceedOnFailure.
    [TestMethod]
    public void Audit() => OrderProbe.Record("Audit");
}

[TestClass]
public sealed class VerifyStep
{
    // Must be skipped: same prerequisite, but no proceedOnFailure.
    [TestMethod]
    public void Verify() => OrderProbe.Record("Verify");
}

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
    public void Second()
    {
        OrderProbe.Record("Second");
        Assert.Fail("deliberate failure to exercise skip propagation through configuration");
    }
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
