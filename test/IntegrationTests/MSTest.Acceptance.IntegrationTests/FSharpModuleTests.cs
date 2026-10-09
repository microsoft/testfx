// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Compiles real F# modules against the packed framework and adapter, then exercises the same
/// immutable assembly through both hosts. Each process has its own markers and result directory.
/// </summary>
[TestClass]
public sealed class FSharpModuleTests : AcceptanceTestBase<FSharpModuleTests.TestAssetFixture>
{
    private const string ProjectName = "FSharpModuleTestProject";
    private const string MarkerDirectoryVariable = "FSHARP_MODULE_MARKER_DIRECTORY";
    private static readonly XNamespace TrxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    private static readonly string[] DiscoveryNames =
    [
        "FileLevelModuleTest",
        "Named module test with a . in its name",
        "NestedModuleTest",
        "ExplicitModuleTest",
        "CustomModuleTest",
        "ExistingClassTest",
        "RowZero",
        "RowFive",
        "DynamicNegative",
        "DynamicPositive",
        "TaskReturn",
        "ValueTaskReturn",
        "ModuleMetadata",
        "MethodMetadata",
        "NestedMetadata",
        "IgnoredModuleTest",
        "IgnoredMethodTest",
        "NestedNotIgnored",
        "ModuleRetry",
        "MethodRetry",
        "NestedRetryNotInherited",
        "LifecycleOrder",
        "NonParallelFirst",
        "NonParallelSecond",
        "ParallelFirst",
        "ParallelSecond",
        "BodyFailure",
        "InitializeFailure",
        "CleanupFailure",
        "CooperativeTimeout",
        "NonCooperativeTimeout",
    ];

    public TestContext TestContext { get; set; } = default!;

    public static IEnumerable<(string Tfm, bool UseVstest)> ExecutionCases
        => TargetFrameworks.Net.SelectMany(tfm => new[] { (tfm, false), (tfm, true) });

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task DiscoversModulesWithoutTestClassAndPreservesFSharpClassValidation(string tfm, bool useVstest)
    {
        using TempDirectory run = new("discovery");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, listTests: true);

        Assert.AreEqual(0, result.ExitCode, result.ToString());
        Assert.DoesNotContain("non-public class FSharpModuleTestProject.MicrosoftTestingPlatformApplication", result.StandardOutput);
        Assert.Contains("F# module FSharpModuleTestProject.NamedModule", result.StandardOutput);
        Assert.Contains("must be static and public", result.StandardOutput);
        foreach (string name in DiscoveryNames)
        {
            Assert.Contains(name, result.StandardOutput, result.ToString());
        }

        // Invalid-signature warnings may mention our negative controls. Check discovered entries,
        // not the entire output, so those diagnostics do not masquerade as discovered tests.
        if (useVstest)
        {
            string[] names = result.StandardOutputLines
                .SkipWhile(line => !line.Contains("The following Tests are available:", StringComparison.Ordinal))
                .Skip(1)
                .Where(line => line.StartsWith("    ", StringComparison.Ordinal))
                .Select(line => line.Trim())
                .ToArray();
            Assert.AreSequenceEqual(
                DiscoveryNames.Order(StringComparer.Ordinal).ToArray(),
                names.Order(StringComparer.Ordinal).ToArray(),
                result.ToString());
        }
        else
        {
            result.AssertOutputContains($"found {DiscoveryNames.Length} test(s)");
        }

        // Discovery must not invoke assembly/global/local fixtures or test bodies.
        Assert.IsEmpty(Directory.GetFiles(Markers(run), "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task RunsNamedNestedExplicitAndCustomModulesAlongsideExistingClass(string tfm, bool useVstest)
    {
        using TempDirectory run = new("basic");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, "TestCategory=basic");
        XDocument trx = AssertResults(result, useVstest, run, passed: 6);
        AssertNames(trx, "FileLevelModuleTest", "Named module test with a . in its name", "NestedModuleTest", "ExplicitModuleTest", "CustomModuleTest", "ExistingClassTest");

        Assert.Contains("FSharpModuleTestProject.NamedModule", trx.ToString());
        Assert.Contains("FSharpModuleTestProject.Outer+Nested", trx.ToString());
        Assert.AreEqual(
            """
            custom-before
            custom-body
            custom-after
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "custom"));
        Assert.AreEqual(
            """
            constructor
            initialize
            body
            cleanup
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "existing-class"));
        AssertAssemblyFixtures(run);
        Assert.IsFalse(File.Exists(MarkerPath(run, "nested-global")));
        Assert.IsFalse(File.Exists(MarkerPath(run, "ordinary-class-global")));
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task RunsDataRowsDynamicDataTaskAndValueTaskModuleFunctions(string tfm, bool useVstest)
    {
        using TempDirectory run = new("data");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, "TestCategory=data");
        XDocument trx = AssertResults(result, useVstest, run, passed: 6);
        AssertNames(trx, "RowZero", "RowFive", "DynamicNegative", "DynamicPositive", "TaskReturn", "ValueTaskReturn");
        Assert.AreSequenceEqual(
            new[] { "dynamic:-3", "dynamic:4", "row:0", "row:5", "task-after-yield", "valuetask-after-yield" },
            File.ReadAllLines(MarkerPath(run, "data")).Order(StringComparer.Ordinal).ToArray());
        AssertAssemblyFixtures(run);
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task HonorsModuleCategoriesAndPropertiesWithMethodMetadata(string tfm, bool useVstest)
    {
        using TempDirectory categoryRun = new("categories");
        TestHostResult categoryResult = await ExecuteAsync(tfm, useVstest, categoryRun, "TestCategory=module-category");
        AssertNames(AssertResults(categoryResult, useVstest, categoryRun, passed: 2), "ModuleMetadata", "MethodMetadata");

        using TempDirectory propertyRun = new("properties");
        TestHostResult propertyResult = await ExecuteAsync(tfm, useVstest, propertyRun, "module-property=module-value");
        AssertNames(AssertResults(propertyResult, useVstest, propertyRun, passed: 2), "ModuleMetadata", "MethodMetadata");

        using TempDirectory methodRun = new("method-metadata");
        TestHostResult methodResult = await ExecuteAsync(
            tfm, useVstest, methodRun, "TestCategory=method-category&method-property=method-value");
        AssertNames(AssertResults(methodResult, useVstest, methodRun, passed: 1), "MethodMetadata");
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task DoesNotInheritContainingModuleMetadataOrLocalFixtures(string tfm, bool useVstest)
    {
        using TempDirectory run = new("nested-metadata");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, "TestCategory=nested-metadata");
        AssertNames(AssertResults(result, useVstest, run, passed: 1), "NestedMetadata");
        Assert.AreEqual(
            """
            global-initialize
            body
            global-cleanup
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "NestedMetadata"));
        Assert.IsFalse(File.Exists(MarkerPath(run, "containing-module-fixture")));
        Assert.IsFalse(File.Exists(MarkerPath(run, "nested-global")));
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task HonorsModuleAndMethodIgnoreButDoesNotIgnoreNestedModule(string tfm, bool useVstest)
    {
        using TempDirectory run = new("ignore");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, "TestCategory=ignore");
        XDocument trx = AssertResults(result, useVstest, run, passed: 1, skipped: 2);
        AssertNames(trx, "IgnoredModuleTest", "IgnoredMethodTest", "NestedNotIgnored");
        Assert.IsFalse(File.Exists(MarkerPath(run, "IgnoredModuleTest")));
        Assert.IsFalse(File.Exists(MarkerPath(run, "IgnoredMethodTest")));
        Assert.AreEqual(
            """
            global-initialize
            body
            global-cleanup
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "NestedNotIgnored"));
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task HonorsModuleRetryAndMethodOverride(string tfm, bool useVstest)
    {
        using TempDirectory run = new("retry");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, "TestCategory=retry");
        AssertNames(AssertResults(result, useVstest, run, passed: 2), "ModuleRetry", "MethodRetry");
        Assert.AreEqual(
            """
            global-initialize
            attempt:1
            global-cleanup
            global-initialize
            attempt:2
            global-cleanup
            global-initialize
            attempt:3
            global-cleanup
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "ModuleRetry"));
        Assert.AreEqual(
            """
            global-initialize
            attempt:1
            global-cleanup
            global-initialize
            attempt:2
            global-cleanup
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "MethodRetry"));
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task DoesNotInheritRetryIntoNestedModule(string tfm, bool useVstest)
    {
        using TempDirectory run = new("nested-retry");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, "Name=NestedRetryNotInherited");
        XDocument trx = AssertResults(result, useVstest, run, passed: 0, failed: 1);
        Assert.Contains("nested-module-must-not-retry", trx.ToString());
        Assert.AreEqual(
            """
            global-initialize
            attempt:1
            global-cleanup
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "NestedRetryNotInherited"));
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task RunsStaticLifecycleAndGlobalFixturesWithCurrentContextAndResultFile(string tfm, bool useVstest)
    {
        using TempDirectory run = new("lifecycle");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, "Name=LifecycleOrder");
        XDocument trx = AssertResults(result, useVstest, run, passed: 1);
        Assert.AreEqual(
            """
            class-initialize
            global-initialize
            test-initialize
            body
            test-cleanup
            global-cleanup
            class-cleanup
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "LifecycleOrder"));
        AssertAssemblyFixtures(run);
        Assert.IsFalse(File.Exists(MarkerPath(run, "nested-global")));

        XElement attachment = Assert.ContainsSingle(trx.Descendants(TrxNamespace + "ResultFile"));
        Assert.EndsWith("module-result.txt", attachment.Attribute("path")!.Value);
        string copiedAttachment = Assert.ContainsSingle(Directory.GetFiles(Results(run), "module-result.txt", SearchOption.AllDirectories));
        Assert.AreEqual("module attachment", File.ReadAllText(copiedAttachment));
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task HonorsModuleDoNotParallelizeAndKeepsNestedModuleParallelizable(string tfm, bool useVstest)
    {
        using TempDirectory run = new("parallel");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, "TestCategory=parallel");
        AssertNames(AssertResults(result, useVstest, run, passed: 4), "NonParallelFirst", "NonParallelSecond", "ParallelFirst", "ParallelSecond");
        Assert.AreSequenceEqual(
            new[] { "nonparallel:first", "nonparallel:second", "parallel:first", "parallel:second" },
            File.ReadAllLines(MarkerPath(run, "parallel")).Order(StringComparer.Ordinal).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task BodyFailureStillRunsStaticAndGlobalCleanup(string tfm, bool useVstest)
    {
        using TempDirectory run = new("body-failure");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, "Name=BodyFailure");
        XDocument trx = AssertResults(result, useVstest, run, passed: 0, failed: 1);
        Assert.Contains("module-body-failure", trx.ToString());
        Assert.Contains("Test method FSharpModuleTestProject.BodyFailureModule.BodyFailure threw exception:", trx.ToString());
        Assert.DoesNotContain("Unable to create instance", trx.ToString());
        Assert.AreEqual(
            """
            global-initialize
            test-initialize
            body
            test-cleanup
            global-cleanup
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "BodyFailure"));
        AssertAssemblyFixtures(run);
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task InitializeFailureSkipsBodyButStillRunsStaticAndGlobalCleanup(string tfm, bool useVstest)
    {
        using TempDirectory run = new("initialize-failure");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, "Name=InitializeFailure");
        XDocument trx = AssertResults(result, useVstest, run, passed: 0, failed: 1);
        Assert.Contains("module-initialize-failure", trx.ToString());
        Assert.AreEqual(
            """
            global-initialize
            test-initialize
            test-cleanup
            global-cleanup
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "InitializeFailure"));
        AssertAssemblyFixtures(run);
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task CleanupFailureIsReportedAndGlobalCleanupStillRuns(string tfm, bool useVstest)
    {
        using TempDirectory run = new("cleanup-failure");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, "Name=CleanupFailure");
        XDocument trx = AssertResults(result, useVstest, run, passed: 0, failed: 1);
        Assert.Contains("module-cleanup-failure", trx.ToString());
        Assert.AreEqual(
            """
            global-initialize
            test-initialize
            body
            test-cleanup
            global-cleanup
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "CleanupFailure"));
        AssertAssemblyFixtures(run);
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task CooperativeTimeoutRunsCleanupOnceWithFreshCancellationToken(string tfm, bool useVstest)
    {
        using TempDirectory run = new("cooperative-timeout");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, "Name=CooperativeTimeout");
        XDocument trx = AssertResults(result, useVstest, run, passed: 0, failed: 1);
        Assert.Contains("Test 'CooperativeTimeout' timed out after 1000ms", trx.ToString());
        Assert.AreEqual(
            """
            global-initialize
            test-initialize
            body
            cancelled
            test-cleanup
            global-cleanup
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "CooperativeTimeout"));
        AssertAssemblyFixtures(run);
    }

    [TestMethod]
    [DynamicData(nameof(ExecutionCases))]
    public async Task NonCooperativeTimeoutRunsCleanupOnceWithoutConstructingModule(string tfm, bool useVstest)
    {
        using TempDirectory run = new("noncooperative-timeout");
        TestHostResult result = await ExecuteAsync(tfm, useVstest, run, "Name=NonCooperativeTimeout");
        XDocument trx = AssertResults(result, useVstest, run, passed: 0, failed: 1);
        Assert.Contains("Test 'NonCooperativeTimeout' timed out after 1000ms", trx.ToString());
        Assert.AreEqual(
            """
            global-initialize
            test-initialize
            body
            test-cleanup
            global-cleanup
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "NonCooperativeTimeout"));
        AssertAssemblyFixtures(run);
    }

    private async Task<TestHostResult> ExecuteAsync(
        string tfm, bool useVstest, TempDirectory run, string? filter = null, bool listTests = false)
    {
        Directory.CreateDirectory(Markers(run));
        Directory.CreateDirectory(Results(run));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        // This is a process safety bound, not an assertion about scheduling or timeout duration.
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var environment = new Dictionary<string, string?>
        {
            [MarkerDirectoryVariable] = Markers(run),
        };
        string settings = Path.Combine(AssetFixture.GetAssetPath(ProjectName), "module.runsettings");
        var host = TestHost.LocateFrom(AssetFixture.GetAssetPath(ProjectName), ProjectName, tfm);

        if (!useVstest)
        {
            string arguments = listTests
                ? "--list-tests"
                : $"--report-trx --report-trx-filename modules.trx --results-directory \"{Results(run)}\"";
            if (filter is not null)
            {
                arguments += $" --filter \"{filter}\"";
            }

            return await host.ExecuteAsync(
                $"{arguments} --settings \"{settings}\"",
                environment,
                cancellationToken: timeout.Token);
        }

        string dotnetRoot = Path.Combine(RootFinder.Find(), ".dotnet");
        string dotnet = Path.Combine(dotnetRoot, $"dotnet{Constants.ExecutableExtension}");
        environment["DOTNET_ROOT"] = dotnetRoot;
        environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        string assembly = Path.Combine(host.DirectoryName, $"{ProjectName}.dll");
        string vstestArguments = listTests
            ? "/ListTests"
            : $"/Logger:\"trx;LogFileName=modules.trx\" /ResultsDirectory:\"{Results(run)}\"";
        if (filter is not null)
        {
            vstestArguments += $" /TestCaseFilter:\"{filter}\"";
        }

        string command = $"\"{dotnet}\" vstest \"{assembly}\" {vstestArguments} /Settings:\"{settings}\"";
        using CommandLine commandLine = new();
        int exitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
            command,
            environmentVariables: environment,
            workingDirectory: run.Path,
            cancellationToken: timeout.Token);
        return new TestHostResult(command, exitCode, commandLine.StandardOutput, commandLine.StandardOutputLines, commandLine.ErrorOutput, commandLine.ErrorOutputLines);
    }

    private static XDocument AssertResults(TestHostResult result, bool useVstest, TempDirectory run, int passed, int failed = 0, int skipped = 0)
    {
        int expectedExitCode = failed == 0 ? 0 : useVstest ? 1 : (int)ExitCode.AtLeastOneTestFailed;
        Assert.AreEqual(expectedExitCode, result.ExitCode, result.ToString());
        if (!useVstest)
        {
            result.AssertOutputContainsSummary(failed, passed, skipped);
        }

        string trxPath = Assert.ContainsSingle(Directory.GetFiles(Results(run), "modules.trx", SearchOption.AllDirectories));
        var trx = XDocument.Load(trxPath);
        XElement[] results = LeafResults(trx);
        Assert.HasCount(passed + failed + skipped, results, result.ToString());
        Assert.HasCount(passed, results.Where(element => (string?)element.Attribute("outcome") == "Passed"), result.ToString());
        Assert.HasCount(skipped, results.Where(element => (string?)element.Attribute("outcome") == "NotExecuted"), result.ToString());
        Assert.HasCount(failed, results.Where(element => (string?)element.Attribute("outcome") is "Failed" or "Timeout" or "Error"), result.ToString());
        Assert.AreEqual(results.Length, results.Select(element => (string?)element.Attribute("executionId")).Distinct().Count());
        return trx;
    }

    private static XElement[] LeafResults(XDocument trx)
        => trx.Descendants(TrxNamespace + "UnitTestResult")
            .Where(element => !element.Descendants(TrxNamespace + "UnitTestResult").Any())
            .ToArray();

    private static void AssertNames(XDocument trx, params string[] expected)
        => Assert.AreSequenceEqual(
            expected.Order(StringComparer.Ordinal).ToArray(),
            LeafResults(trx).Select(element => element.Attribute("testName")!.Value).Order(StringComparer.Ordinal).ToArray());

    private static string Markers(TempDirectory run) => Path.Combine(run.Path, "markers");

    private static string Results(TempDirectory run) => Path.Combine(run.Path, "results");

    private static string MarkerPath(TempDirectory run, string name) => Path.Combine(Markers(run), $"{name}.marker");

    private static string ReadMarker(TempDirectory run, string name) => string.Join("\n", File.ReadAllLines(MarkerPath(run, name)));

    private static void AssertAssemblyFixtures(TempDirectory run)
        => Assert.AreEqual(
            """
            assembly-initialize
            assembly-cleanup
            """.ReplaceLineEndings("\n"),
            ReadMarker(run, "assembly"));

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        // The metadata generators emit C#, not F#. Build this single asset only in reflection mode.
        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [];

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (ProjectName, ProjectName, SourceCode
                .PatchTargetFrameworks(TargetFrameworks.Net)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion)
                .PatchCodeWithReplace("$MarkerDirectoryVariable$", MarkerDirectoryVariable));

        private const string SourceCode = """
#file FSharpModuleTestProject.fsproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <GenerateProgramFile>false</GenerateProgramFile>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>$(NoWarn);57</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="FileLevelModule.fs" />
    <Compile Include="Tests.fs" />
  </ItemGroup>
</Project>

#file module.runsettings
<RunSettings>
  <MSTest>
    <ClassCleanupLifecycle>EndOfClass</ClassCleanupLifecycle>
    <TreatDiscoveryWarningsAsErrors>false</TreatDiscoveryWarningsAsErrors>
  </MSTest>
</RunSettings>

#file FileLevelModule.fs
module FSharpModuleTestProject.FileLevelModule

open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestMethod; TestCategory("basic")>]
let FileLevelModuleTest () =
    Assert.AreEqual(4, 2 + 2)

#file Tests.fs
namespace FSharpModuleTestProject

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting

[<assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)>]
do ()

module Marker =
    let private syncRoot = obj()
    let directory () =
        match Environment.GetEnvironmentVariable("$MarkerDirectoryVariable$") with
        | null -> failwith "marker directory is required"
        | path -> path
    let path name = Path.Combine(directory(), name + ".marker")
    let append name line =
        lock syncRoot (fun () -> File.AppendAllText(path name, line + "\n"))
    let current line =
        Assert.IsNotNull(TestContext.Current)
        append TestContext.Current.TestName line
    let assertFreshCleanupToken () =
        Assert.IsNotNull(TestContext.Current)
        Assert.IsFalse(TestContext.Current.CancellationToken.IsCancellationRequested)

// These fixtures intentionally have no TestClass and no test methods.
// Global fixture validation requires a top-level public declaring type.
module AssemblyAndGlobalFixtures =
    [<AssemblyInitialize>]
    let AssemblyInitialize (context: TestContext) : Task =
        task {
            do! Task.Yield()
            Assert.AreSame(context, TestContext.Current)
            Marker.append "assembly" "assembly-initialize"
        } :> Task

    [<AssemblyCleanup>]
    let AssemblyCleanup (context: TestContext) : ValueTask =
        ValueTask(task {
            do! Task.Yield()
            Assert.AreSame(context, TestContext.Current)
            Marker.append "assembly" "assembly-cleanup"
        } :> Task)

    [<GlobalTestInitialize>]
    let GlobalInitialize (context: TestContext) : Task =
        task {
            do! Task.Yield()
            Assert.AreSame(context, TestContext.Current)
            Marker.append context.TestName "global-initialize"
        } :> Task

    [<GlobalTestCleanup>]
    let GlobalCleanup (context: TestContext) : ValueTask =
        ValueTask(task {
            do! Task.Yield()
            Assert.AreSame(context, TestContext.Current)
            Marker.append context.TestName "global-cleanup"
        } :> Task)

module NamedModule =
    [<TestMethod; TestCategory("basic")>]
    let ``Named module test with a . in its name`` () =
        Marker.current "body"

    [<TestMethod>]
    let private PrivateMethodNotATest () =
        Assert.Fail("private functions must not run")

    [<TestMethod>]
    let FSharpAsyncNotSupported () =
        async { return () }

module Outer =
    module Nested =
        [<TestMethod; TestCategory("basic")>]
        let NestedModuleTest () =
            Marker.current "body"

    // Nested public modules have IsNestedPublic, not IsPublic. Keep global predicate semantics.
    module NestedGlobalFixtures =
        [<GlobalTestInitialize>]
        let MustNotInitialize (_: TestContext) =
            Marker.append "nested-global" "unexpected-initialize"
        [<GlobalTestCleanup>]
        let MustNotCleanup (_: TestContext) =
            Marker.append "nested-global" "unexpected-cleanup"

[<TestClass>]
module ExplicitModule =
    [<TestMethod; TestCategory("basic")>]
    let ExplicitModuleTest () =
        Marker.current "body"

type WrappedMethodAttribute () =
    inherit TestMethodAttribute()
    override _.ExecuteAsync(testMethod: ITestMethod) : Task<TestResult array> =
        task {
            Marker.append "custom" "custom-before"
            let! result = testMethod.InvokeAsync(null)
            Marker.append "custom" "custom-after"
            return [| result |]
        }

type CustomModuleClassAttribute () =
    inherit TestClassAttribute()
    override _.GetTestMethodAttribute(_: TestMethodAttribute) =
        WrappedMethodAttribute() :> TestMethodAttribute

[<CustomModuleClass>]
module CustomModule =
    [<TestMethod; TestCategory("basic")>]
    let CustomModuleTest () =
        Assert.IsNotNull(TestContext.Current)
        Assert.AreEqual("custom-before\n", File.ReadAllText(Marker.path "custom"))
        Marker.append "custom" "custom-body"

type OrdinaryClass () =
    [<TestMethod>]
    member _.OrdinaryClassNotATest () =
        Assert.Fail("ordinary F# classes still require TestClass")
    [<GlobalTestInitialize>]
    static member MustNotInitialize (_: TestContext) =
        Marker.append "ordinary-class-global" "unexpected"

type OrdinaryRecord =
    { Value: int }
    [<TestMethod>]
    member _.RecordNotATest () =
        Assert.Fail("F# records are not modules")

[<TestClass>]
type ExistingClass () =
    do Marker.append "existing-class" "constructor"
    member val TestContext: TestContext = Unchecked.defaultof<TestContext> with get, set
    [<TestInitialize>]
    member this.Initialize () =
        Assert.AreSame(this.TestContext, TestContext.Current)
        Marker.append "existing-class" "initialize"
    [<TestCleanup>]
    member this.Cleanup () =
        Assert.AreSame(this.TestContext, TestContext.Current)
        Marker.append "existing-class" "cleanup"
    [<TestMethod; TestCategory("basic")>]
    member this.ExistingClassTest () =
        Assert.AreSame(this.TestContext, TestContext.Current)
        Marker.append "existing-class" "body"
    [<TestMethod>]
    static member StaticClassMethodNotATest () =
        Assert.Fail("static test methods on ordinary F# classes are still invalid")

module internal InternalModule =
    [<TestMethod>]
    let InternalModuleNotATest () =
        Assert.Fail("internal modules require DiscoverInternals")

module DataModule =
    [<TestMethod; TestCategory("data")>]
    [<DataRow(0, 0, DisplayName = "RowZero")>]
    [<DataRow(5, 10, DisplayName = "RowFive")>]
    let Rows (value: int, expected: int) =
        Assert.AreEqual(expected, value * 2)
        Marker.append "data" (sprintf "row:%d" value)

    let DynamicRows () : seq<obj array> =
        seq {
            yield [| box (-3); box (-6) |]
            yield [| box 4; box 8 |]
        }

    let DynamicName (_: System.Reflection.MethodInfo) (data: obj array) =
        if unbox<int> data.[0] < 0 then "DynamicNegative" else "DynamicPositive"

    [<TestMethod; TestCategory("data")>]
    [<DynamicData("DynamicRows", DynamicDataSourceType.Method, DynamicDataDisplayName = "DynamicName")>]
    let Dynamic (value: int, expected: int) =
        Assert.AreEqual(expected, value * 2)
        Marker.append "data" (sprintf "dynamic:%d" value)

    [<TestMethod; TestCategory("data")>]
    let TaskReturn () : Task =
        task {
            let context = TestContext.Current
            do! Task.Yield()
            Assert.AreSame(context, TestContext.Current)
            Marker.append "data" "task-after-yield"
        } :> Task

    [<TestMethod; TestCategory("data")>]
    let ValueTaskReturn () : ValueTask =
        ValueTask(task {
            let context = TestContext.Current
            do! Task.Yield()
            Assert.AreSame(context, TestContext.Current)
            Marker.append "data" "valuetask-after-yield"
        } :> Task)

[<TestCategory("module-category"); TestProperty("module-property", "module-value")>]
module MetadataModule =
    [<TestInitialize>]
    let Initialize () = Marker.append "containing-module-fixture" "initialize"
    [<TestCleanup>]
    let Cleanup () = Marker.append "containing-module-fixture" "cleanup"

    [<TestMethod>]
    let ModuleMetadata () =
        Assert.AreEqual("module-value", unbox<string> TestContext.Current.Properties.["module-property"])
        Assert.IsTrue(TestContext.Current.Properties.ContainsKey("module-category"))

    [<TestMethod; TestCategory("method-category"); TestProperty("method-property", "method-value")>]
    let MethodMetadata () =
        Assert.AreEqual("module-value", unbox<string> TestContext.Current.Properties.["module-property"])
        Assert.AreEqual("method-value", unbox<string> TestContext.Current.Properties.["method-property"])
        Assert.IsTrue(TestContext.Current.Properties.ContainsKey("module-category"))
        Assert.IsTrue(TestContext.Current.Properties.ContainsKey("method-category"))

    module Nested =
        [<TestMethod; TestCategory("nested-metadata")>]
        let NestedMetadata () =
            Assert.IsFalse(TestContext.Current.Properties.ContainsKey("module-property"))
            Assert.IsFalse(TestContext.Current.Properties.ContainsKey("module-category"))
            Assert.IsFalse(File.Exists(Marker.path "containing-module-fixture"))
            Marker.current "body"

[<Ignore("module ignored")>]
module IgnoredModule =
    [<TestMethod; TestCategory("ignore")>]
    let IgnoredModuleTest () =
        Marker.current "unexpected-body"
        Assert.Fail("module Ignore must be honored")

    module Nested =
        [<TestMethod; TestCategory("ignore")>]
        let NestedNotIgnored () =
            Marker.current "body"

module MethodIgnoredModule =
    [<TestMethod; TestCategory("ignore"); Ignore("method ignored")>]
    let IgnoredMethodTest () =
        Marker.current "unexpected-body"
        Assert.Fail("method Ignore must be honored")

[<Retry(2)>]
module RetryModule =
    let mutable private moduleAttempts = 0
    let mutable private methodAttempts = 0

    [<TestMethod; TestCategory("retry")>]
    let ModuleRetry () =
        moduleAttempts <- moduleAttempts + 1
        Marker.current (sprintf "attempt:%d" moduleAttempts)
        if moduleAttempts < 3 then Assert.Fail("retry the module function")
        Assert.AreEqual(3, moduleAttempts)

    [<TestMethod; TestCategory("retry"); Retry(1)>]
    let MethodRetry () =
        methodAttempts <- methodAttempts + 1
        Marker.current (sprintf "attempt:%d" methodAttempts)
        if methodAttempts < 2 then Assert.Fail("retry the method once")
        Assert.AreEqual(2, methodAttempts)

    module Nested =
        let mutable private attempts = 0
        [<TestMethod; TestCategory("failure")>]
        let NestedRetryNotInherited () =
            attempts <- attempts + 1
            Marker.current (sprintf "attempt:%d" attempts)
            Assert.Fail("nested-module-must-not-retry")

module LifecycleModule =
    [<ClassInitialize>]
    let InitializeClass (context: TestContext) : Task =
        task {
            do! Task.Yield()
            Assert.AreSame(context, TestContext.Current)
            Marker.append "LifecycleOrder" "class-initialize"
        } :> Task

    [<ClassCleanup>]
    let CleanupClass (context: TestContext) : ValueTask =
        ValueTask(task {
            do! Task.Yield()
            Assert.AreSame(context, TestContext.Current)
            Marker.append "LifecycleOrder" "class-cleanup"
        } :> Task)

    [<TestInitialize>]
    let Initialize () : Task =
        task {
            let context = TestContext.Current
            do! Task.Yield()
            Assert.AreSame(context, TestContext.Current)
            Marker.current "test-initialize"
        } :> Task

    [<TestCleanup>]
    let Cleanup () : ValueTask =
        ValueTask(task {
            let context = TestContext.Current
            do! Task.Yield()
            Assert.AreSame(context, TestContext.Current)
            Assert.AreEqual(UnitTestOutcome.Passed, context.CurrentTestOutcome)
            Marker.current "test-cleanup"
        } :> Task)

    [<TestMethod; TestCategory("lifecycle")>]
    let LifecycleOrder () =
        Marker.current "body"
        let attachment = Path.Combine(Marker.directory(), "module-result.txt")
        File.WriteAllText(attachment, "module attachment")
        TestContext.Current.AddResultFile(attachment)

module ParallelState =
    let mutable private entered = 0
    let mutable private finished = 0
    let private release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
    let participate name : Task =
        task {
            if Interlocked.Increment(&entered) = 2 then release.SetResult(())
            do! release.Task.WaitAsync(TestContext.Current.CancellationToken)
            Marker.append "parallel" ("parallel:" + name)
            Interlocked.Increment(&finished) |> ignore
        } :> Task
    let assertParallelPhaseFinished name =
        Assert.AreEqual(2, Volatile.Read(&finished))
        Marker.append "parallel" ("nonparallel:" + name)

[<DoNotParallelize>]
module NonParallelModule =
    [<TestMethod; TestCategory("parallel")>]
    let NonParallelFirst () = ParallelState.assertParallelPhaseFinished "first"
    [<TestMethod; TestCategory("parallel")>]
    let NonParallelSecond () = ParallelState.assertParallelPhaseFinished "second"

    // Both functions must be eligible for the parallel phase despite their containing module.
    // The handshake also detects accidental assembly-wide serialization, without a sleep.
    module Nested =
        [<TestMethod; TestCategory("parallel")>]
        let ParallelFirst () : Task = ParallelState.participate "first"
        [<TestMethod; TestCategory("parallel")>]
        let ParallelSecond () : Task = ParallelState.participate "second"

module BodyFailureModule =
    [<TestInitialize>]
    let Initialize () = Marker.current "test-initialize"
    [<TestCleanup>]
    let Cleanup () =
        Assert.AreEqual(UnitTestOutcome.Failed, TestContext.Current.CurrentTestOutcome)
        Marker.current "test-cleanup"
    [<TestMethod; TestCategory("failure")>]
    let BodyFailure () : unit =
        Marker.current "body"
        failwith "module-body-failure"

module InitializeFailureModule =
    [<TestInitialize>]
    let Initialize () =
        Marker.current "test-initialize"
        Assert.Fail("module-initialize-failure")
    [<TestCleanup>]
    let Cleanup () =
        Assert.AreEqual(UnitTestOutcome.Failed, TestContext.Current.CurrentTestOutcome)
        Marker.current "test-cleanup"
    [<TestMethod; TestCategory("failure")>]
    let InitializeFailure () =
        Marker.current "unexpected-body"
        Assert.Fail("test body must not run after initialize failure")

module CleanupFailureModule =
    [<TestInitialize>]
    let Initialize () = Marker.current "test-initialize"
    [<TestCleanup>]
    let Cleanup () =
        Marker.current "test-cleanup"
        Assert.Fail("module-cleanup-failure")
    [<TestMethod; TestCategory("failure")>]
    let CleanupFailure () = Marker.current "body"

module CooperativeTimeoutModule =
    [<TestInitialize>]
    let Initialize () = Marker.current "test-initialize"
    [<TestCleanup>]
    let Cleanup () =
        Marker.assertFreshCleanupToken()
        Assert.AreEqual(UnitTestOutcome.Timeout, TestContext.Current.CurrentTestOutcome)
        Marker.current "test-cleanup"
    [<TestMethod; TestCategory("timeout"); Timeout(1000, CooperativeCancellation = true)>]
    let CooperativeTimeout () : Task =
        task {
            Marker.current "body"
            try
                do! Task.Delay(Timeout.Infinite, TestContext.Current.CancellationToken)
            finally
                Marker.current "cancelled"
        } :> Task

module NonCooperativeTimeoutModule =
    let private release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
    [<TestInitialize>]
    let Initialize () = Marker.current "test-initialize"
    [<TestCleanup>]
    let Cleanup () =
        Marker.assertFreshCleanupToken()
        Marker.current "test-cleanup"
        // Release the abandoned invocation only after cleanup has claimed its exactly-once gate.
        // The test task deliberately does not observe the cancellation token.
        release.TrySetResult(()) |> ignore
    [<TestMethod; TestCategory("timeout"); Timeout(1000, CooperativeCancellation = false)>]
    let NonCooperativeTimeout () : Task =
        Marker.current "body"
        release.Task :> Task
""";
    }
}
