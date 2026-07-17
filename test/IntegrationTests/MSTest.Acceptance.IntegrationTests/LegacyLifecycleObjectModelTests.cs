// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.Loader;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

namespace MSTest.Acceptance.IntegrationTests;

// Legacy equivalence map:
// - MSTest.VstestConsoleWrapper.IntegrationTests.SuiteLifeCycleTests.ValidateTestRunLifecycle:
//   * its non-inherited assembly/class/test/IDisposable ordering is already equivalent to
//     MSTest.Acceptance.IntegrationTests.LifecycleTests.LifecycleTest;
//   * the inheritance-mode matrix, inherited method execution, exact per-result channel ownership,
//     duration/outcome assertions, and modern IAsyncDisposable ordering are preserved below.
// - MSTest.VstestConsoleWrapper.IntegrationTests.SuiteLifeCycleTests.ValidateInheritanceBehavior:
//   its three inheritance paths are preserved by MultiLevelBeforeEachDerivedClass_HasExactPerTestMessages;
//   assembly boundary ordering is already equivalent to LifecycleTests.LifecycleTest.
[TestClass]
public sealed class LegacyLifecycleObjectModelTests : AcceptanceTestBase<LegacyLifecycleObjectModelTests.TestAssetFixture>
{
    private static readonly Lock AssemblyLoadLock = new();
    private static readonly HashSet<string> LoadedAssemblies = [];

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task LifecycleInheritanceModes_HaveExactPerTestObjectModelMessages()
    {
        string tfm = TargetFrameworks.NetCurrent;
        var testCases = DiscoverTests(tfm)
            .Where(testCase => testCase.FullyQualifiedName.EndsWith("Derived.Test", StringComparison.Ordinal)
                && !testCase.FullyQualifiedName.Contains("InheritedDerived", StringComparison.Ordinal))
            .ToImmutableArray();

        Assert.HasCount(4, testCases);

        await RunSingleAndAssertAsync(
            testCases.Single(testCase => testCase.FullyQualifiedName.Contains(".BothBeforeDerived.", StringComparison.Ordinal)),
            tfm,
            "BothBefore",
            [
                "BothBeforeBase.ClassInitialize",
                "BothBeforeDerived.ClassInitialize",
                "BothBeforeBase.ctor",
                "BothBeforeDerived.ctor",
                "BothBeforeBase.TestInitialize",
                "BothBeforeDerived.TestInitialize",
                "BothBeforeDerived.Test",
                "BothBeforeDerived.TestCleanup",
                "BothBeforeBase.TestCleanup",
                .. DisposeEvents("BothBeforeBase", tfm),
                "BothBeforeDerived.ClassCleanup",
                "BothBeforeBase.ClassCleanup",
            ]);

        await RunSingleAndAssertAsync(
            testCases.Single(testCase => testCase.FullyQualifiedName.Contains(".BothNoneDerived.", StringComparison.Ordinal)),
            tfm,
            "BothNone",
            [
                "BothNoneDerived.ClassInitialize",
                "BothNoneBase.ctor",
                "BothNoneDerived.ctor",
                "BothNoneBase.TestInitialize",
                "BothNoneDerived.TestInitialize",
                "BothNoneDerived.Test",
                "BothNoneDerived.TestCleanup",
                "BothNoneBase.TestCleanup",
                .. DisposeEvents("BothNoneBase", tfm),
                "BothNoneDerived.ClassCleanup",
            ]);

        await RunSingleAndAssertAsync(
            testCases.Single(testCase => testCase.FullyQualifiedName.Contains(".InitBeforeCleanupNoneDerived.", StringComparison.Ordinal)),
            tfm,
            "InitBeforeCleanupNone",
            [
                "InitBeforeCleanupNoneBase.ClassInitialize",
                "InitBeforeCleanupNoneDerived.ClassInitialize",
                "InitBeforeCleanupNoneBase.ctor",
                "InitBeforeCleanupNoneDerived.ctor",
                "InitBeforeCleanupNoneBase.TestInitialize",
                "InitBeforeCleanupNoneDerived.TestInitialize",
                "InitBeforeCleanupNoneDerived.Test",
                "InitBeforeCleanupNoneDerived.TestCleanup",
                "InitBeforeCleanupNoneBase.TestCleanup",
                .. DisposeEvents("InitBeforeCleanupNoneBase", tfm),
                "InitBeforeCleanupNoneDerived.ClassCleanup",
            ]);

        await RunSingleAndAssertAsync(
            testCases.Single(testCase => testCase.FullyQualifiedName.Contains(".InitNoneCleanupBeforeDerived.", StringComparison.Ordinal)),
            tfm,
            "InitNoneCleanupBefore",
            [
                "InitNoneCleanupBeforeDerived.ClassInitialize",
                "InitNoneCleanupBeforeBase.ctor",
                "InitNoneCleanupBeforeDerived.ctor",
                "InitNoneCleanupBeforeBase.TestInitialize",
                "InitNoneCleanupBeforeDerived.TestInitialize",
                "InitNoneCleanupBeforeDerived.Test",
                "InitNoneCleanupBeforeDerived.TestCleanup",
                "InitNoneCleanupBeforeBase.TestCleanup",
                .. DisposeEvents("InitNoneCleanupBeforeBase", tfm),
                "InitNoneCleanupBeforeDerived.ClassCleanup",
                "InitNoneCleanupBeforeBase.ClassCleanup",
            ]);
    }

    [TestMethod]
    public async Task InheritedTestMethod_HasDerivedLifecycleAndIsIsolatedFromSiblingResult()
    {
        string tfm = TargetFrameworks.NetCurrent;
        var inheritedCases = DiscoverTests(tfm)
            .Where(testCase => testCase.FullyQualifiedName.Contains(".InheritedDerived.", StringComparison.Ordinal))
            .ToImmutableArray();

        Assert.HasCount(2, inheritedCases);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "LifecycleObjectModel.InheritedDerived.BaseTest",
                "LifecycleObjectModel.InheritedDerived.DerivedTest",
            },
            inheritedCases.Select(testCase => testCase.FullyQualifiedName).ToArray());

        foreach (TestCase testCase in inheritedCases)
        {
            string methodName = testCase.FullyQualifiedName.EndsWith(".BaseTest", StringComparison.Ordinal)
                ? "BaseTest"
                : "DerivedTest";
            string otherMethodName = methodName == "BaseTest" ? "DerivedTest" : "BaseTest";

            TestResult result = await RunSingleAndAssertAsync(
                testCase,
                tfm,
                "Inherited",
                [
                    "InheritedBase.ClassInitialize",
                    "InheritedDerived.ClassInitialize",
                    "InheritedBase.ctor",
                    "InheritedDerived.ctor",
                    "InheritedBase.TestInitialize",
                    "InheritedDerived.TestInitialize",
                    $"InheritedDerived.{methodName}",
                    "InheritedDerived.TestCleanup",
                    "InheritedBase.TestCleanup",
                    .. DisposeEvents("InheritedBase", tfm),
                    "InheritedDerived.ClassCleanup",
                    "InheritedBase.ClassCleanup",
                ]);

            Assert.IsFalse(
                result.Messages.Any(message => message.Text?.Contains($"InheritedDerived.{otherMethodName}", StringComparison.Ordinal) == true),
                $"Output from {otherMethodName} leaked into {methodName}.");
        }
    }

    [TestMethod]
    public async Task MultiLevelBeforeEachDerivedClass_HasExactPerTestMessages()
    {
        string tfm = TargetFrameworks.NetCurrent;
        var testCases = DiscoverTests(tfm)
            .Where(testCase => testCase.FullyQualifiedName.Contains(".Inheritance", StringComparison.Ordinal))
            .ToImmutableArray();

        Assert.HasCount(3, testCases);

        await RunSingleAndAssertAsync(
            testCases.Single(testCase => testCase.FullyQualifiedName.Contains(".InheritanceDirect.", StringComparison.Ordinal)),
            tfm,
            "Inheritance",
            [
                "InheritanceBase.ClassInitialize",
                "InheritanceDirect.Test",
                "InheritanceBase.ClassCleanup",
            ]);

        await RunSingleAndAssertAsync(
            testCases.Single(testCase => testCase.FullyQualifiedName.Contains(".InheritanceWithOwnCleanup.", StringComparison.Ordinal)),
            tfm,
            "Inheritance",
            [
                "InheritanceBase.ClassInitialize",
                "InheritanceIntermediate.ClassInitialize",
                "InheritanceWithOwnCleanup.Test",
                "InheritanceWithOwnCleanup.ClassCleanup",
                "InheritanceIntermediate.ClassCleanup",
                "InheritanceBase.ClassCleanup",
            ]);

        await RunSingleAndAssertAsync(
            testCases.Single(testCase => testCase.FullyQualifiedName.Contains(".InheritanceWithoutOwnCleanup.", StringComparison.Ordinal)),
            tfm,
            "Inheritance",
            [
                "InheritanceBase.ClassInitialize",
                "InheritanceIntermediate.ClassInitialize",
                "InheritanceWithoutOwnCleanup.Test",
                "InheritanceIntermediate.ClassCleanup",
                "InheritanceBase.ClassCleanup",
            ]);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task LifecycleInheritanceModes_RunInTargetFrameworkHost(string tfm)
    {
        string assemblyName = $"LegacyLifecycleObjectModel_{tfm.Replace('.', '_')}";
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, assemblyName, tfm);
        TestHostResult result = await testHost.ExecuteAsync(
            "--output Detailed",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContainsSummary(failed: 0, passed: 10, skipped: 0);
        LegacyAcceptanceAssert.Passed(
            result,
            "Test",
            "BaseTest",
            "DerivedTest");
        LegacyAcceptanceAssert.OutputContains(
            result,
            "BothBeforeBase.ClassInitialize",
            "BothBeforeDerived.ClassCleanup",
            "BothNoneDerived.ClassInitialize",
            "InitBeforeCleanupNoneBase.ClassInitialize",
            "InitNoneCleanupBeforeBase.ClassCleanup",
            "InheritedBase.ClassInitialize",
            "InheritedDerived.BaseTest",
            "InheritedDerived.DerivedTest",
            "InheritanceWithOwnCleanup.ClassCleanup",
            "InheritanceIntermediate.ClassCleanup",
            "InheritanceBase.ClassCleanup");

        string disposeEvent = tfm.StartsWith("net4", StringComparison.Ordinal) ? ".Dispose" : ".DisposeAsync";
        result.AssertOutputContains(disposeEvent);
    }

    private static ImmutableArray<TestCase> DiscoverTests(string tfm)
    {
        string assemblyPath = AssetFixture.GetAssemblyPath(tfm);
        EnsureAssemblyIsLoaded(assemblyPath);
        return CLITestBase.DiscoverTests(assemblyPath);
    }

    private static void EnsureAssemblyIsLoaded(string assemblyPath)
    {
        lock (AssemblyLoadLock)
        {
            if (LoadedAssemblies.Add(assemblyPath))
            {
                AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            }
        }
    }

    private static async Task<TestResult> RunSingleAndAssertAsync(
        TestCase testCase,
        string tfm,
        string scenarioPrefix,
        string[] expectedEvents)
    {
        ImmutableArray<TestResult> results = await CLITestBase.RunTestsAsync([testCase]);
        Assert.HasCount(1, results);

        TestResult result = results[0];
        Assert.AreEqual(testCase.FullyQualifiedName, result.TestCase.FullyQualifiedName);
        Assert.AreEqual(TestOutcome.Passed, result.Outcome);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(20), result.Duration);
        Assert.IsLessThan(TimeSpan.FromSeconds(5), result.Duration);
        Assert.IsLessThanOrEqualTo(result.EndTime, result.StartTime);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(20), result.EndTime - result.StartTime);
        Assert.IsLessThan(TimeSpan.FromSeconds(5), result.EndTime - result.StartTime);
        Assert.HasCount(3, result.Messages);

        string newLine = Environment.NewLine;
        string expectedConsole = string.Join(newLine, expectedEvents.Select(entry => $"Console: {entry}")) + newLine;
        string expectedTrace = $"{newLine}{newLine}Debug Trace:{newLine}"
            + string.Join(newLine, expectedEvents.Select(entry => $"Trace: {entry}"))
            + newLine;
        string expectedTestContext = $"{newLine}{newLine}TestContext Messages:{newLine}"
            + string.Join(newLine, expectedEvents)
            + newLine;

        Assert.AreEqual(expectedConsole, result.Messages[0].Text, $"{tfm}: unexpected console ownership for {testCase.FullyQualifiedName}.");
        Assert.AreEqual(expectedTrace, result.Messages[1].Text, $"{tfm}: unexpected trace ownership for {testCase.FullyQualifiedName}.");
        Assert.AreEqual(expectedTestContext, result.Messages[2].Text, $"{tfm}: unexpected TestContext ownership for {testCase.FullyQualifiedName}.");

        string[] otherPrefixes = ["BothBefore", "BothNone", "InitBeforeCleanupNone", "InitNoneCleanupBefore", "Inherited"];
        foreach (string otherPrefix in otherPrefixes.Where(prefix => prefix != scenarioPrefix))
        {
            Assert.IsFalse(
                result.Messages.Any(message => message.Text?.Contains(otherPrefix, StringComparison.Ordinal) == true),
                $"{otherPrefix} output leaked into {testCase.FullyQualifiedName}.");
        }

        return result;
    }

    private static string[] DisposeEvents(string prefix, string tfm)
        => tfm.StartsWith("net4", StringComparison.Ordinal)
            ? [$"{prefix}.Dispose"]
            : [$"{prefix}.DisposeAsync", $"{prefix}.Dispose"];

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        public const string ProjectName = "LegacyLifecycleObjectModel";

        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [];

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public string GetAssemblyPath(string tfm)
        {
            string assemblyName = $"LegacyLifecycleObjectModel_{tfm.Replace('.', '_')}";
            return Path.Combine(GetAssetPath(ProjectName), "bin", "Release", tfm, $"{assemblyName}.dll");
        }

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file LegacyLifecycleObjectModel.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <LangVersion>preview</LangVersion>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <AssemblyName>LegacyLifecycleObjectModel_$([System.String]::Copy('$(TargetFramework)').Replace('.', '_'))</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file LifecycleCases.cs
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LifecycleObjectModel;

internal static class Recorder
{
    public static void Write(TestContext context, string value)
    {
        context.WriteLine(value);
        Console.WriteLine($"Console: {value}");
        Trace.WriteLine($"Trace: {value}");
    }

}

public abstract class InstanceLifecycleBase
#if NETFRAMEWORK
    : IDisposable
#else
    : IDisposable, IAsyncDisposable
#endif
{
    private readonly string _prefix;
    protected TestContext Context { get; }

    protected InstanceLifecycleBase(TestContext context, string prefix)
    {
        Context = context;
        _prefix = prefix;
        Recorder.Write(context, $"{prefix}.ctor");
    }

    [TestInitialize]
    public void BaseTestInitialize() => Recorder.Write(Context, $"{_prefix}.TestInitialize");

    [TestCleanup]
    public void BaseTestCleanup() => Recorder.Write(Context, $"{_prefix}.TestCleanup");

#if !NETFRAMEWORK
    public ValueTask DisposeAsync()
    {
        Recorder.Write(Context, $"{_prefix}.DisposeAsync");
        return ValueTask.CompletedTask;
    }
#endif

    public void Dispose() => Recorder.Write(Context, $"{_prefix}.Dispose");
}

[TestClass]
public class BothBeforeBase : InstanceLifecycleBase
{
    public BothBeforeBase(TestContext context) : base(context, nameof(BothBeforeBase)) { }
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassInitialize(TestContext context) => Recorder.Write(context, "BothBeforeBase.ClassInitialize");
    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassCleanup(TestContext context) => Recorder.Write(context, "BothBeforeBase.ClassCleanup");
}

[TestClass]
public class BothBeforeDerived : BothBeforeBase
{
    public BothBeforeDerived(TestContext context) : base(context) => Recorder.Write(context, "BothBeforeDerived.ctor");
    [ClassInitialize] public static new void ClassInitialize(TestContext context) => Recorder.Write(context, "BothBeforeDerived.ClassInitialize");
    [TestInitialize] public void TestInitialize() => Recorder.Write(Context, "BothBeforeDerived.TestInitialize");
    [TestMethod] public void Test() { Recorder.Write(Context, "BothBeforeDerived.Test"); Thread.Sleep(20); }
    [TestCleanup] public void TestCleanup() => Recorder.Write(Context, "BothBeforeDerived.TestCleanup");
    [ClassCleanup] public static new void ClassCleanup(TestContext context) => Recorder.Write(context, "BothBeforeDerived.ClassCleanup");
}

[TestClass]
public class BothNoneBase : InstanceLifecycleBase
{
    public BothNoneBase(TestContext context) : base(context, nameof(BothNoneBase)) { }
    [ClassInitialize(InheritanceBehavior.None)]
    public static void ClassInitialize(TestContext context) => Recorder.Write(context, "BothNoneBase.ClassInitialize");
    [ClassCleanup(InheritanceBehavior.None)]
    public static void ClassCleanup(TestContext context) => Recorder.Write(context, "BothNoneBase.ClassCleanup");
}

[TestClass]
public class BothNoneDerived : BothNoneBase
{
    public BothNoneDerived(TestContext context) : base(context) => Recorder.Write(context, "BothNoneDerived.ctor");
    [ClassInitialize] public static new void ClassInitialize(TestContext context) => Recorder.Write(context, "BothNoneDerived.ClassInitialize");
    [TestInitialize] public void TestInitialize() => Recorder.Write(Context, "BothNoneDerived.TestInitialize");
    [TestMethod] public void Test() { Recorder.Write(Context, "BothNoneDerived.Test"); Thread.Sleep(20); }
    [TestCleanup] public void TestCleanup() => Recorder.Write(Context, "BothNoneDerived.TestCleanup");
    [ClassCleanup] public static new void ClassCleanup(TestContext context) => Recorder.Write(context, "BothNoneDerived.ClassCleanup");
}

[TestClass]
public class InitBeforeCleanupNoneBase : InstanceLifecycleBase
{
    public InitBeforeCleanupNoneBase(TestContext context) : base(context, nameof(InitBeforeCleanupNoneBase)) { }
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassInitialize(TestContext context) => Recorder.Write(context, "InitBeforeCleanupNoneBase.ClassInitialize");
    [ClassCleanup(InheritanceBehavior.None)]
    public static void ClassCleanup(TestContext context) => Recorder.Write(context, "InitBeforeCleanupNoneBase.ClassCleanup");
}

[TestClass]
public class InitBeforeCleanupNoneDerived : InitBeforeCleanupNoneBase
{
    public InitBeforeCleanupNoneDerived(TestContext context) : base(context) => Recorder.Write(context, "InitBeforeCleanupNoneDerived.ctor");
    [ClassInitialize] public static new void ClassInitialize(TestContext context) => Recorder.Write(context, "InitBeforeCleanupNoneDerived.ClassInitialize");
    [TestInitialize] public void TestInitialize() => Recorder.Write(Context, "InitBeforeCleanupNoneDerived.TestInitialize");
    [TestMethod] public void Test() { Recorder.Write(Context, "InitBeforeCleanupNoneDerived.Test"); Thread.Sleep(20); }
    [TestCleanup] public void TestCleanup() => Recorder.Write(Context, "InitBeforeCleanupNoneDerived.TestCleanup");
    [ClassCleanup] public static new void ClassCleanup(TestContext context) => Recorder.Write(context, "InitBeforeCleanupNoneDerived.ClassCleanup");
}

[TestClass]
public class InitNoneCleanupBeforeBase : InstanceLifecycleBase
{
    public InitNoneCleanupBeforeBase(TestContext context) : base(context, nameof(InitNoneCleanupBeforeBase)) { }
    [ClassInitialize(InheritanceBehavior.None)]
    public static void ClassInitialize(TestContext context) => Recorder.Write(context, "InitNoneCleanupBeforeBase.ClassInitialize");
    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassCleanup(TestContext context) => Recorder.Write(context, "InitNoneCleanupBeforeBase.ClassCleanup");
}

[TestClass]
public class InitNoneCleanupBeforeDerived : InitNoneCleanupBeforeBase
{
    public InitNoneCleanupBeforeDerived(TestContext context) : base(context) => Recorder.Write(context, "InitNoneCleanupBeforeDerived.ctor");
    [ClassInitialize] public static new void ClassInitialize(TestContext context) => Recorder.Write(context, "InitNoneCleanupBeforeDerived.ClassInitialize");
    [TestInitialize] public void TestInitialize() => Recorder.Write(Context, "InitNoneCleanupBeforeDerived.TestInitialize");
    [TestMethod] public void Test() { Recorder.Write(Context, "InitNoneCleanupBeforeDerived.Test"); Thread.Sleep(20); }
    [TestCleanup] public void TestCleanup() => Recorder.Write(Context, "InitNoneCleanupBeforeDerived.TestCleanup");
    [ClassCleanup] public static new void ClassCleanup(TestContext context) => Recorder.Write(context, "InitNoneCleanupBeforeDerived.ClassCleanup");
}

[TestClass]
public class InheritedBase : InstanceLifecycleBase
{
    public InheritedBase(TestContext context) : base(context, nameof(InheritedBase)) { }
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassInitialize(TestContext context) => Recorder.Write(context, "InheritedBase.ClassInitialize");
    [TestMethod] public void BaseTest() { Recorder.Write(Context, "InheritedDerived.BaseTest"); Thread.Sleep(20); }
    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassCleanup(TestContext context) => Recorder.Write(context, "InheritedBase.ClassCleanup");
}

[TestClass]
public class InheritedDerived : InheritedBase
{
    public InheritedDerived(TestContext context) : base(context) => Recorder.Write(context, "InheritedDerived.ctor");
    [ClassInitialize] public static new void ClassInitialize(TestContext context) => Recorder.Write(context, "InheritedDerived.ClassInitialize");
    [TestInitialize] public void TestInitialize() => Recorder.Write(Context, "InheritedDerived.TestInitialize");
    [TestMethod] public void DerivedTest() { Recorder.Write(Context, "InheritedDerived.DerivedTest"); Thread.Sleep(20); }
    [TestCleanup] public void TestCleanup() => Recorder.Write(Context, "InheritedDerived.TestCleanup");
    [ClassCleanup] public static new void ClassCleanup(TestContext context) => Recorder.Write(context, "InheritedDerived.ClassCleanup");
}

public class InheritanceBase
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassInitialize(TestContext context) => Recorder.Write(context, "InheritanceBase.ClassInitialize");
    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void ClassCleanup(TestContext context) => Recorder.Write(context, "InheritanceBase.ClassCleanup");
}

public class InheritanceIntermediate : InheritanceBase
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static new void ClassInitialize(TestContext context) => Recorder.Write(context, "InheritanceIntermediate.ClassInitialize");
    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static new void ClassCleanup(TestContext context) => Recorder.Write(context, "InheritanceIntermediate.ClassCleanup");
}

[TestClass]
public class InheritanceDirect : InheritanceBase
{
    public TestContext TestContext { get; set; } = null!;
    [TestMethod] public void Test() { Recorder.Write(TestContext, "InheritanceDirect.Test"); Thread.Sleep(20); }
}

[TestClass]
public class InheritanceWithOwnCleanup : InheritanceIntermediate
{
    public TestContext TestContext { get; set; } = null!;
    [TestMethod] public void Test() { Recorder.Write(TestContext, "InheritanceWithOwnCleanup.Test"); Thread.Sleep(20); }
    [ClassCleanup] public static new void ClassCleanup(TestContext context) => Recorder.Write(context, "InheritanceWithOwnCleanup.ClassCleanup");
}

[TestClass]
public class InheritanceWithoutOwnCleanup : InheritanceIntermediate
{
    public TestContext TestContext { get; set; } = null!;
    [TestMethod] public void Test() { Recorder.Write(TestContext, "InheritanceWithoutOwnCleanup.Test"); Thread.Sleep(20); }
}
""";
    }
}
