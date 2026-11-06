// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class PartitionTests : AcceptanceTestBase<PartitionTests.TestAssetFixture>
{
    private const string Independent = "PartitionIndependent";
    private const string Dependencies = "PartitionDependencies";
    private const string Rows = "PartitionRows";
    private const string RuntimeRows = "PartitionRuntimeRows";
    private const string Checkout = "Acme.CheckoutTests";
    private const string Cart = "Acme.CartTests";
    private const string Coffee = "Acme.Caf\u00e9Tests.Coffee";
    private const string ProbeVariable = "PARTITION_TEST_PROBE";
    private const string SentinelVariable = "PARTITION_TEST_RETRY_SENTINEL";

    private static readonly string[] Baseline = [$"{Checkout}.Keep", $"{Checkout}.Other", $"{Cart}.Keep", $"{Cart}.Other", Coffee];
    private static readonly string[] DependencyTests = [$"{Checkout}.Prepare", $"{Checkout}.Other", $"{Cart}.Buy", $"{Cart}.Other"];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Partition_AbsentAndOneOfOne_PreserveExactBaselineIdentitiesAndOutcomes()
    {
        DiscoveredNode[] baseline = await DiscoverAsync(Independent, string.Empty);
        DiscoveredNode[] oneOfOne = await DiscoverAsync(Independent, "--partition 1/1");
        AssertDiscovery(baseline, Baseline);
        AssertDiscovery(oneOfOne, Baseline);
        Assert.AreSequenceEqual(
            baseline.Select(node => node.FullyQualifiedName + "|" + node.Uid).Order(StringComparer.Ordinal),
            oneOfOne.Select(node => node.FullyQualifiedName + "|" + node.Uid).Order(StringComparer.Ordinal));

        foreach (string options in new[] { string.Empty, "--partition 1/1" })
        {
            using var run = new TempDirectory("partition-baseline");
            RunResult result = await RunAsync(Independent, options, run);
            result.Host.AssertExitCodeIs(ExitCode.Success);
            AssertResults(result.Results, Baseline);
            AssertDiscoveryIdentities(baseline, result.Results);
        }
    }

    [TestMethod]
    public async Task Partition_DiscoveryAndRun_SelectSameDisjointClasses()
    {
        HashSet<string> allUids = [];
        List<string> union = [];
        for (int index = 1; index <= 4; index++)
        {
            string[] expected = ExpectedBucket(index);
            DiscoveredNode[] discovered = await DiscoverAsync(Independent, $"--partition {index}/4", expected.Length == 0 ? ExitCode.ZeroTests : ExitCode.Success);
            AssertDiscovery(discovered, expected);
            foreach (DiscoveredNode node in discovered)
            {
                Assert.IsTrue(allUids.Add(node.Uid), $"UID {node.Uid} appeared in more than one partition.");
                union.Add(node.FullyQualifiedName);
            }

            using var run = new TempDirectory("partition-bucket");
            RunResult result = await RunAsync(Independent, $"--partition {index}/4", run);
            result.Host.AssertExitCodeIs(expected.Length == 0 ? ExitCode.ZeroTests : ExitCode.Success);
            AssertResults(result.Results, expected);
            AssertDiscoveryIdentities(discovered, result.Results);
        }

        Assert.AreSequenceEqual(Baseline, union, SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public async Task Partition_NativeDotnetTest_MatchesConsoleSelection()
    {
        foreach (int index in new[] { 1, 2 })
        {
            DiscoveredNode[] discovered = await DiscoverAsync(Independent, $"--partition {index}/4");
            using var run = new TempDirectory("partition-dotnet-test");
            string arguments = $"test --project \"{AssetFixture.ProjectPath(Independent)}\" -c Release --no-build -p:SkipUapTargetFramework=true --partition {index}/4"
                + $" --report-trx --report-trx-filename run.trx --results-directory \"{run.Path}\" --no-ansi --progress off";
            DotnetMuxerResult result = await DotnetCli.RunAsync(
                arguments,
                workingDirectory: AssetFixture.AssetPath(Independent),
                environmentVariables: AssetFixture.EnvironmentFor(Independent, run, forDotnetCli: true),
                failIfReturnValueIsNotZero: false,
                cancellationToken: TestContext.CancellationToken);

            RecordChildRun(Independent, MetadataMode.Reflection, "dotnet-test", arguments, run,
                result.ExitCode, result.StandardOutput, result.StandardError);
            Assert.AreEqual(0, result.ExitCode, result.StandardOutput + result.StandardError);
            ReportResult[] results = ReadResults(Path.Combine(run.Path, "run.trx"));
            AssertResults(results, ExpectedBucket(index));
            AssertDiscoveryIdentities(discovered, results);
        }
    }

    [TestMethod]
    [CombinatorialData]
    public async Task Partition_MetadataModes_PreserveClassAndDependencyPlacement([MetadataModeValues] MetadataMode metadataMode)
    {
        DiscoveredNode[] independent = await DiscoverAsync(Independent, "--partition 1/4", metadataMode: metadataMode);
        AssertDiscovery(independent, ExpectedBucket(1));
        using var independentRun = new TempDirectory("partition-metadata-independent");
        RunResult selected = await RunAsync(Independent, "--partition 1/4", independentRun, metadataMode);
        selected.Host.AssertExitCodeIs(ExitCode.Success);
        AssertResults(selected.Results, ExpectedBucket(1));
        AssertDiscoveryIdentities(independent, selected.Results);

        DiscoveredNode[] dependencies = await DiscoverAsync(Dependencies, "--partition 4/4", metadataMode: metadataMode);
        AssertDiscovery(dependencies, DependencyTests);
        using var dependencyRun = new TempDirectory("partition-metadata-dependencies");
        RunResult component = await RunAsync(Dependencies, "--partition 4/4", dependencyRun, metadataMode);
        component.Host.AssertExitCodeIs(ExitCode.Success);
        AssertResults(component.Results, DependencyTests);
        AssertDiscoveryIdentities(dependencies, component.Results);
    }

    [TestMethod]
    public async Task Partition_AttributeAndConfiguredDependencies_KeepFullComponentTogether()
    {
        using var attributeRun = new TempDirectory("partition-attribute-dependencies");
        RunResult attributeResult = await RunAsync(Dependencies, "--partition 4/4", attributeRun);
        attributeResult.Host.AssertExitCodeIs(ExitCode.Success);
        AssertResults(attributeResult.Results, DependencyTests);
        string[] events = ReadProbe(attributeRun);
        Assert.AreSequenceEqual(
            new[] { "Checkout.Prepare", "Checkout.Other", "Cart.Buy", "Cart.Other" },
            events,
            SequenceOrder.InAnyOrder);
        Assert.IsTrue(Array.IndexOf(events, "Checkout.Prepare") < Array.IndexOf(events, "Cart.Buy"), string.Join(",", events));
        attributeResult.Host.AssertOutputDoesNotContain("which matches no test");

        using var configuration = new TempDirectory("partition-config-dependencies");
        await TempDirectory.WriteFileAsync(configuration.Path, "dependencies.testconfig.json", """
            {
              "mstest": {
                "execution": {
                  "dependencies": {
                    "chains": [ [ "Acme.CheckoutTests.Keep", "Acme.CartTests.Keep" ] ],
                    "nodes": [ { "test": "Acme.CartTests.*", "dependsOn": [ "Acme.CheckoutTests.Keep" ] } ]
                  }
                }
              }
            }
            """);
        string config = Path.Combine(configuration.Path, "dependencies.testconfig.json");
        string options = $"--config-file \"{config}\"";
        string[] connected = [$"{Checkout}.Keep", $"{Checkout}.Other", $"{Cart}.Keep", $"{Cart}.Other"];
        DiscoveredNode[] discovered = await DiscoverAsync(Independent, $"--partition 4/4 {options}");
        AssertDiscovery(discovered, connected);
        using var connectedRun = new TempDirectory("partition-config-connected");
        RunResult configured = await RunAsync(Independent, $"--partition 4/4 {options}", connectedRun);
        configured.Host.AssertExitCodeIs(ExitCode.Success);
        AssertResults(configured.Results, connected);
        configured.Host.AssertOutputDoesNotContain("which matches no test");

        // The configured dependent lives in bucket 4, not the current bucket 1. It still exists
        // in ordinary user selection, so partitioning must not invent an unmatched warning.
        using var otherBucketRun = new TempDirectory("partition-config-other-bucket");
        RunResult otherBucket = await RunAsync(Independent, $"--partition 1/4 {options}", otherBucketRun);
        otherBucket.Host.AssertExitCodeIs(ExitCode.Success);
        AssertResults(otherBucket.Results, [Coffee]);
        otherBucket.Host.AssertOutputDoesNotContain("which matches no test in this run");
    }

    [TestMethod]
    public async Task Partition_FilteredOrMissingDependencies_PreserveOrdinaryDiagnostics()
    {
        using var filteredRun = new TempDirectory("partition-filtered-prerequisite");
        RunResult filtered = await RunAsync(
            Dependencies, "--partition 4/4 --filter FullyQualifiedName=Acme.CartTests.Buy", filteredRun);
        filtered.Host.AssertExitCodeIs(ExitCode.Success);
        AssertResults(filtered.Results, [$"{Cart}.Buy"]);
        Assert.AreSequenceEqual(new[] { "Cart.Buy" }, ReadProbe(filteredRun));
        filtered.Host.AssertOutputContains("Test 'Acme.CartTests.Buy' declares a dependency on 'Acme.CheckoutTests.Prepare'");
        filtered.Host.AssertOutputContains("The dependency is ignored.");

        using var configuration = new TempDirectory("partition-unmatched-dependency");
        await TempDirectory.WriteFileAsync(configuration.Path, "missing.testconfig.json", """
            {
              "mstest": {
                "execution": {
                  "dependencies": {
                    "nodes": [ { "test": "Acme.MissingTests.Buy", "dependsOn": [ "Acme.CheckoutTests.Keep" ] } ]
                  }
                }
              }
            }
            """);
        using var missingRun = new TempDirectory("partition-missing-dependent");
        RunResult missing = await RunAsync(
            Independent, $"--partition 1/4 --config-file \"{Path.Combine(configuration.Path, "missing.testconfig.json")}\"", missingRun);
        missing.Host.AssertExitCodeIs(ExitCode.Success);
        AssertResults(missing.Results, ExpectedBucket(1));
        missing.Host.AssertOutputContains("declares dependencies for 'Acme.MissingTests.Buy', which matches no test in this run");
    }

    [TestMethod]
    public async Task Partition_HostFilters_AreIntersections()
    {
        DiscoveredNode selected = (await DiscoverAsync(Independent, "--filter FullyQualifiedName=Acme.CheckoutTests.Keep")).Single();
        using var settings = new TempDirectory("partition-filter-settings");
        await TempDirectory.WriteFileAsync(settings.Path, "fast.runsettings", """
            <RunSettings><RunConfiguration><TestCaseFilter>TestCategory=Fast</TestCaseFilter></RunConfiguration></RunSettings>
            """);
        string options = "--partition 1/4 --filter ClassName=Acme.CheckoutTests"
            + $" --filter-uid {selected.Uid} --settings \"{Path.Combine(settings.Path, "fast.runsettings")}\"";
        DiscoveredNode[] discovered = await DiscoverAsync(Independent, options);
        AssertDiscovery(discovered, [$"{Checkout}.Keep"]);
        Assert.AreEqual(selected.Uid, discovered.Single().Uid);
        using var run = new TempDirectory("partition-filter-intersection");
        RunResult intersection = await RunAsync(Independent, options, run);
        intersection.Host.AssertExitCodeIs(ExitCode.Success);
        AssertResults(intersection.Results, [$"{Checkout}.Keep"]);
        AssertDiscoveryIdentities(discovered, intersection.Results);

        using var disjointRun = new TempDirectory("partition-filter-disjoint");
        RunResult disjoint = await RunAsync(Independent, "--partition 1/4 --filter ClassName=Acme.CartTests", disjointRun);
        disjoint.Host.AssertExitCodeIs(ExitCode.ZeroTests);
        AssertResults(disjoint.Results, []);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Partition data rows discovered before execution")]
    [DataRow(true, DisplayName = "Partition data rows discovered during execution")]
    public async Task Partition_DataRows_RemainWithTheirConcreteClass(bool runtimeOnly)
    {
        string asset = runtimeOnly ? RuntimeRows : Rows;
        DiscoveredNode[] discovered = await DiscoverAsync(asset, "--partition 1/4");
        Assert.IsTrue(discovered.All(node => node.ClassName == Checkout));
        Assert.AreSequenceEqual(
            new[] { "DroppedTest", "Expanded", "Folded", "RunTest", "RuntimeOnly", "SkippedTest" },
            discovered.Select(node => node.MethodName).Distinct().Order(StringComparer.Ordinal));
        Assert.AreEqual(runtimeOnly ? 1 : 2, discovered.Count(node => node.MethodName == "Expanded"));
        Assert.AreEqual(1, discovered.Count(node => node.MethodName == "Folded"));
        Assert.AreEqual(runtimeOnly ? 1 : 2, discovered.Count(node => node.MethodName == "RuntimeOnly"));
        if (!runtimeOnly)
        {
            Assert.AreSequenceEqual(
                new[] { "expanded seventeen", "expanded twenty-three" },
                discovered.Where(node => node.MethodName == "Expanded").Select(node => node.DisplayName),
                SequenceOrder.InAnyOrder);
        }

        using var run = new TempDirectory("partition-data-rows");
        RunResult result = await RunAsync(asset, "--partition 1/4", run);
        result.Host.AssertExitCodeIs(ExitCode.Success);
        Assert.AreSequenceEqual(
            new[]
            {
                $"{Checkout}.RunTest|RunTest|Passed",
                $"{Checkout}.SkippedTest|SkippedTest|NotExecuted",
                $"{Checkout}.Expanded|expanded seventeen|Passed",
                $"{Checkout}.Expanded|expanded twenty-three|Passed",
                $"{Checkout}.Folded|folded 31|Passed",
                $"{Checkout}.Folded|folded 37|Passed",
                $"{Checkout}.RuntimeOnly|runtime 41|Passed",
                $"{Checkout}.RuntimeOnly|runtime 43|Passed",
            },
            result.Results.Select(test => $"{test.FullyQualifiedName}|{test.DisplayName}|{test.Outcome}"),
            SequenceOrder.InAnyOrder);
        Assert.AreSequenceEqual(
            new[] { "RunTest", "Expanded:17", "Expanded:23", "Folded:31", "Folded:37", "RuntimeOnly:41", "RuntimeOnly:43" },
            ReadProbe(run),
            SequenceOrder.InAnyOrder);

        using var outsideRun = new TempDirectory("partition-data-outside");
        RunResult outside = await RunAsync(asset, "--partition 2/4", outsideRun);
        outside.Host.AssertExitCodeIs(ExitCode.Success);
        AssertResults(outside.Results, [$"{Cart}.Outside"]);
        Assert.AreSequenceEqual(new[] { "Outside" }, ReadProbe(outsideRun));
    }

    [TestMethod]
    public async Task Partition_ProgrammaticDropAndSkip_AreNotBroadened()
    {
        using var run = new TempDirectory("partition-provider");
        RunResult result = await RunAsync(
            Rows, "--partition 1/4 --filter \"Name=RunTest|Name=SkippedTest|Name=DroppedTest\"", run);
        result.Host.AssertExitCodeIs(ExitCode.Success);
        Assert.AreSequenceEqual(
            new[] { $"{Checkout}.RunTest|Passed", $"{Checkout}.SkippedTest|NotExecuted" },
            result.Results.Select(test => test.FullyQualifiedName + "|" + test.Outcome),
            SequenceOrder.InAnyOrder);
        result.Host.AssertOutputContains("Skipped by the partition fixture provider");
        Assert.AreSequenceEqual(new[] { "RunTest" }, ReadProbe(run));

        using var droppedRun = new TempDirectory("partition-provider-drop");
        RunResult dropped = await RunAsync(Rows, "--partition 1/4 --filter Name=DroppedTest", droppedRun);
        dropped.Host.AssertExitCodeIs(ExitCode.ZeroTests);
        AssertResults(dropped.Results, []);

        foreach ((string policy, ExitCode exit) in new[] { (string.Empty, ExitCode.Success), ("--zero-tests-policy strict", ExitCode.ZeroTests) })
        {
            using var skippedRun = new TempDirectory("partition-provider-skip");
            RunResult skipped = await RunAsync(Rows, $"--partition 1/4 --filter Name=SkippedTest {policy}", skippedRun);
            skipped.Host.AssertExitCodeIs(exit);
            Assert.AreSequenceEqual(
                new[] { $"{Checkout}.SkippedTest|NotExecuted" },
                skipped.Results.Select(test => test.FullyQualifiedName + "|" + test.Outcome));
        }
    }

    [TestMethod]
    public async Task Partition_RetryUidNarrowing_DoesNotMoveDependencyComponent()
    {
        using var run = new TempDirectory("partition-retry");
        string sentinel = Path.Combine(run.Path, "failed-once");
        RunResult result = await RunAsync(
            Dependencies, "--partition 4/4 --retry-failed-tests 1", run,
            extraEnvironment: new Dictionary<string, string?> { [SentinelVariable] = sentinel });
        result.Host.AssertExitCodeIs(ExitCode.Success);
        // TRX publishes the final attempt at the top level; unlike a retry-aware consolidated reporter,
        // it does not repeat the successful tests from attempt one.
        AssertResults(result.Results, [$"{Cart}.Buy"]);
        Assert.IsTrue(File.Exists(sentinel), "The first attempt must actually have reached the deliberate failure.");

        string retryRoot = Path.Combine(run.Path, "Retries");
        string[] reports = Directory.GetFiles(retryRoot, "*.trx", SearchOption.AllDirectories);
        Assert.HasCount(2, reports);
        ReportResult[] first = ReadResults(reports.Single(path => Path.GetFileName(Path.GetDirectoryName(path)) == "1"));
        ReportResult[] second = ReadResults(reports.Single(path => Path.GetFileName(Path.GetDirectoryName(path)) == "2"));
        Assert.AreSequenceEqual(
            DependencyTests.Select(name => name + (name == $"{Cart}.Buy" ? "|Failed" : "|Passed")),
            first.Select(test => test.FullyQualifiedName + "|" + test.Outcome),
            SequenceOrder.InAnyOrder);
        AssertResults(second, [$"{Cart}.Buy"]);
        Assert.AreEqual(first.Single(test => test.FullyQualifiedName == $"{Cart}.Buy").Id, second.Single().Id);
        Assert.AreSequenceEqual(
            second.Select(test => test.Id + "|" + test.FullyQualifiedName + "|" + test.Outcome),
            result.Results.Select(test => test.Id + "|" + test.FullyQualifiedName + "|" + test.Outcome));
        ReportResult[] logicalResults = [.. first.Concat(second).GroupBy(test => test.Id, StringComparer.Ordinal).Select(group => group.Last())];
        AssertResults(logicalResults, DependencyTests);
        Assert.AreSequenceEqual(
            new[] { "Checkout.Prepare", "Checkout.Other", "Cart.Other", "Cart.Buy", "Cart.Buy" },
            ReadProbe(run),
            SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public async Task Partition_EmptyBucket_PreservesExitEight()
    {
        DiscoveredNode[] discovered = await DiscoverAsync(Independent, "--partition 3/4", ExitCode.ZeroTests);
        AssertDiscovery(discovered, []);
        foreach (string options in new[] { "--partition 3/4", "--filter Name=NotAnExistingMethod" })
        {
            using var run = new TempDirectory("partition-empty");
            RunResult result = await RunAsync(Independent, options, run);
            result.Host.AssertExitCodeIs(ExitCode.ZeroTests);
            AssertResults(result.Results, []);
            Assert.IsFalse(File.Exists(ProbePath(run)));
        }
    }

    [TestMethod]
    public async Task Partition_MinimumExpectedTests_UsesSelectedCountAndExitNine()
    {
        foreach ((string partition, int minimum, ExitCode exit, string[] expected) in new (string, int, ExitCode, string[])[]
        {
            ("2/4", 1, ExitCode.Success, [$"{Cart}.Keep", $"{Cart}.Other"]),
            ("2/4", 2, ExitCode.Success, [$"{Cart}.Keep", $"{Cart}.Other"]),
            ("2/4", 3, ExitCode.MinimumExpectedTestsPolicyViolation, [$"{Cart}.Keep", $"{Cart}.Other"]),
            ("3/4", 1, ExitCode.MinimumExpectedTestsPolicyViolation, []),
        })
        {
            using var run = new TempDirectory("partition-minimum");
            RunResult result = await RunAsync(Independent, $"--partition {partition} --minimum-expected-tests {minimum}", run);
            result.Host.AssertExitCodeIs(exit);
            AssertResults(result.Results, expected);
        }
    }

    [TestMethod]
    public async Task Partition_IgnoreExitCode_PreservesExistingPolicy()
    {
        foreach ((string extra, ExitCode exit) in new[]
        {
            ("--ignore-exit-code 8", ExitCode.Success),
            ("--ignore-exit-code 9", ExitCode.ZeroTests),
            ("--minimum-expected-tests 1 --ignore-exit-code 9", ExitCode.Success),
            ("--minimum-expected-tests 1 --ignore-exit-code 8", ExitCode.MinimumExpectedTestsPolicyViolation),
        })
        {
            using var run = new TempDirectory("partition-ignore-exit");
            RunResult result = await RunAsync(Independent, $"--partition 3/4 {extra}", run);
            result.Host.AssertExitCodeIs(exit);
            AssertResults(result.Results, []);
            Assert.IsFalse(File.Exists(ProbePath(run)));
        }
    }

    [TestMethod]
    public async Task Partition_CommandLineWhitespaceNormalization_PreservesExistingPolicy()
    {
        // MTP's common argument parser trims outer whitespace before provider validation. Preserve
        // that existing CLI policy; TestPartition.TryParse still rejects the same raw whitespace.
        foreach (string arguments in new[] { "--partition=\"1/4 \"", "--partition=\" 1/4\"" })
        {
            using var run = new TempDirectory("partition-cli-normalization");
            RunResult result = await RunAsync(Independent, arguments, run);
            result.Host.AssertExitCodeIs(ExitCode.Success);
            AssertResults(result.Results, ExpectedBucket(1));
        }
    }

    [TestMethod]
    [DataRow("missing", DisplayName = "Partition invalid missing argument")]
    [DataRow("extra", DisplayName = "Partition invalid extra argument")]
    [DataRow("zero-index", DisplayName = "Partition invalid zero index")]
    [DataRow("zero-count", DisplayName = "Partition invalid zero count")]
    [DataRow("reversed", DisplayName = "Partition invalid index above count")]
    [DataRow("index-overflow", DisplayName = "Partition invalid index overflow")]
    [DataRow("count-overflow", DisplayName = "Partition invalid count overflow")]
    [DataRow("negative", DisplayName = "Partition invalid negative index")]
    [DataRow("malformed", DisplayName = "Partition invalid extra slash")]
    [DataRow("nondecimal", DisplayName = "Partition invalid nondecimal digits")]
    [DataRow("whitespace", DisplayName = "Partition invalid embedded whitespace")]
    public async Task Partition_InvalidArgument_FailsBeforeRunningTests(string caseId)
    {
        string arguments = caseId switch
        {
            "missing" => "--partition",
            "extra" => "--partition 1/2 extra",
            "zero-index" => "--partition 0/2",
            "zero-count" => "--partition 1/0",
            "reversed" => "--partition 3/2",
            "index-overflow" => "--partition 2147483648/2147483647",
            "count-overflow" => "--partition 1/2147483648",
            "negative" => "--partition=-1/2",
            "malformed" => "--partition 1//2",
            "nondecimal" => "--partition one/two",
            "whitespace" => "--partition=\"1/ 2\"",
            _ => throw new ArgumentOutOfRangeException(nameof(caseId)),
        };
        using var run = new TempDirectory("partition-invalid");
        TestHostResult result = await AssetFixture.Host(Independent).ExecuteAsync(
            arguments,
            environmentVariables: AssetFixture.EnvironmentFor(Independent, run),
            cancellationToken: TestContext.CancellationToken);
        RecordChildRun(Independent, MetadataMode.Reflection, "invalid-option", arguments, run,
            result.ExitCode, result.StandardOutput, result.StandardError);
        result.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        result.AssertOutputContains("--partition");
        if (caseId is not ("missing" or "extra"))
        {
            result.AssertOutputContains(
                "The '--partition' option requires 'index/count', where both values are positive integers and index is less than or equal to count.");
        }

        Assert.IsFalse(File.Exists(ProbePath(run)), "An invalid option must not execute a test body.");
    }

    private async Task<DiscoveredNode[]> DiscoverAsync(
        string asset, string options, ExitCode expectedExit = ExitCode.Success, MetadataMode metadataMode = MetadataMode.Reflection)
    {
        using var run = new TempDirectory("partition-discovery");
        TestHostResult result = await AssetFixture.Host(asset, metadataMode).ExecuteAsync(
            $"{options} --list-tests json --results-directory \"{run.Path}\"",
            environmentVariables: AssetFixture.EnvironmentFor(asset, run),
            cancellationToken: TestContext.CancellationToken);
        RecordChildRun(asset, metadataMode, "discovery", options, run,
            result.ExitCode, result.StandardOutput, result.StandardError);
        File.WriteAllText(Path.Combine(run.Path, "discovery.json"), result.StandardOutput);
        result.AssertExitCodeIs(expectedExit);
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.AreEqual(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        DiscoveredNode[] nodes =
        [
            .. document.RootElement.GetProperty("tests").EnumerateArray().Select(test =>
            {
                JsonElement type = test.GetProperty("type");
                string typeName = type.GetProperty("typeName").GetString()!;
                string? typeNamespace = type.TryGetProperty("namespace", out JsonElement value) ? value.GetString() : null;
                return new DiscoveredNode(
                    test.GetProperty("uid").GetString()!,
                    string.IsNullOrEmpty(typeNamespace) ? typeName : typeNamespace + "." + typeName,
                    type.GetProperty("methodName").GetString()!,
                    test.GetProperty("displayName").GetString()!);
            }),
        ];
        Assert.AreEqual(nodes.Length, nodes.Select(node => node.Uid).Distinct(StringComparer.Ordinal).Count());
        Assert.IsFalse(File.Exists(ProbePath(run)), "Discovery must not execute the test bodies.");
        return nodes;
    }

    private async Task<RunResult> RunAsync(
        string asset,
        string options,
        TempDirectory run,
        MetadataMode metadataMode = MetadataMode.Reflection,
        Dictionary<string, string?>? extraEnvironment = null)
    {
        Dictionary<string, string?> environment = AssetFixture.EnvironmentFor(asset, run);
        if (extraEnvironment is not null)
        {
            foreach ((string key, string? value) in extraEnvironment)
            {
                environment.Add(key, value);
            }
        }

        TestHostResult result = await AssetFixture.Host(asset, metadataMode).ExecuteAsync(
            $"{options} --report-trx --report-trx-filename run.trx --results-directory \"{run.Path}\"",
            environmentVariables: environment,
            cancellationToken: TestContext.CancellationToken);
        RecordChildRun(asset, metadataMode, "execution", options, run,
            result.ExitCode, result.StandardOutput, result.StandardError);
        string report = Path.Combine(run.Path, "run.trx");
        Assert.IsTrue(File.Exists(report), result.StandardOutput + result.StandardError);
        return new RunResult(result, ReadResults(report));
    }

    private void RecordChildRun(
        string asset, MetadataMode metadataMode, string route, string arguments, TempDirectory run,
        int exitCode, string standardOutput, string standardError)
    {
        string manifest = Path.Combine(run.Path, "partition-child-evidence.json");
        File.WriteAllText(manifest, JsonSerializer.Serialize(new
        {
            Asset = asset,
            AssetPath = AssetFixture.AssetPath(asset),
            Project = AssetFixture.ProjectPath(asset),
            MetadataMode = metadataMode.ToString(),
            Route = route,
            Arguments = arguments,
            ExitCode = exitCode,
            Host = AssetFixture.Host(asset, metadataMode).FullName,
            ExpectedSdkVersion = AssetFixture.SdkVersion,
            NuGetPackages = Path.Combine(AssetFixture.AssetPath(asset), ".nuget-packages"),
            ResultsDirectory = run.Path,
        }));
        File.WriteAllText(Path.Combine(run.Path, "stdout.txt"), standardOutput);
        File.WriteAllText(Path.Combine(run.Path, "stderr.txt"), standardError);
        TestContext.WriteLine($"PARTITION-EVIDENCE {manifest}");
    }

    private static string[] ExpectedBucket(int index) => index switch
    {
        1 => [$"{Checkout}.Keep", $"{Checkout}.Other", Coffee],
        2 => [$"{Cart}.Keep", $"{Cart}.Other"],
        _ => [],
    };

    private static void AssertDiscovery(DiscoveredNode[] actual, string[] expected)
        => Assert.AreSequenceEqual(expected, actual.Select(node => node.FullyQualifiedName), SequenceOrder.InAnyOrder);

    private static void AssertResults(ReportResult[] actual, string[] expected)
        => Assert.AreSequenceEqual(
            expected.Select(name => name + "|Passed"),
            actual.Select(result => result.FullyQualifiedName + "|" + result.Outcome),
            SequenceOrder.InAnyOrder);

    private static void AssertDiscoveryIdentities(DiscoveredNode[] discovered, ReportResult[] results)
    {
        Assert.HasCount(discovered.Length, results);
        foreach (DiscoveredNode node in discovered)
        {
            ReportResult result = results.Single(result => result.FullyQualifiedName == node.FullyQualifiedName);
            Assert.AreEqual(Guid.Parse(node.Uid), Guid.Parse(result.Id), node.FullyQualifiedName);
            Assert.AreEqual(node.DisplayName, result.DisplayName);
        }
    }

    private static ReportResult[] ReadResults(string report)
    {
        Assert.IsTrue(File.Exists(report), $"Missing actual TRX report '{report}'.");
        var document = XDocument.Load(report);
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        Assert.AreEqual(ns + "TestRun", document.Root!.Name);
        var definitions = document.Descendants(ns + "UnitTest")
            .ToDictionary(test => test.Attribute("id")!.Value, test => test.Element(ns + "TestMethod")!);
        IEnumerable<XElement> results = document.Root.Element(ns + "Results")?.Elements(ns + "UnitTestResult") ?? [];
        return
        [
            .. results.Select(result =>
            {
                string id = result.Attribute("testId")!.Value;
                XElement method = definitions[id];
                return new ReportResult(
                    id,
                    method.Attribute("className")!.Value,
                    method.Attribute("name")!.Value,
                    result.Attribute("testName")!.Value,
                    result.Attribute("outcome")!.Value);
            }),
        ];
    }

    private static string ProbePath(TempDirectory run) => Path.Combine(run.Path, "bodies.txt");

    private static string[] ReadProbe(TempDirectory run) => File.ReadAllLines(ProbePath(run));

    private sealed record DiscoveredNode(string Uid, string ClassName, string MethodName, string DisplayName)
    {
        internal string FullyQualifiedName => ClassName + "." + MethodName;
    }

    private sealed record ReportResult(string Id, string ClassName, string MethodName, string DisplayName, string Outcome)
    {
        internal string FullyQualifiedName => ClassName + "." + MethodName;
    }

    private sealed record RunResult(TestHostResult Host, ReportResult[] Results);

    public sealed class TestAssetFixture : ITestAssetFixture
    {
        private readonly Dictionary<string, TestAsset> _assets = [];

        internal string SdkVersion { get; private set; } = string.Empty;

        internal string AssetPath(string asset) => _assets[asset].TargetAssetPath;

        internal string ProjectPath(string asset) => Path.Combine(AssetPath(asset), "Acme.Tests.csproj");

        internal TestHost Host(string asset, MetadataMode metadataMode = MetadataMode.Reflection)
            => TestHost.LocateFrom(AssetPath(asset), "Acme.Tests", TargetFrameworks.NetCurrent, metadataMode: metadataMode);

        internal Dictionary<string, string?> EnvironmentFor(string asset, TempDirectory? run = null, bool forDotnetCli = false)
        {
            var environment = new Dictionary<string, string?>
            {
                ["NUGET_PACKAGES"] = Path.Combine(AssetPath(asset), ".nuget-packages"),
                ["TESTINGPLATFORM_UI_LANGUAGE"] = "en-US",
            };
            if (run is not null)
            {
                environment.Add(ProbeVariable, ProbePath(run));
            }

            if (forDotnetCli)
            {
                string dotnet = Path.Combine(RootFinder.Find(), ".dotnet", "dotnet" + Constants.ExecutableExtension);
                ConfigureDotnetSdkEnvironment(environment, dotnet, SdkVersion);
            }

            return environment;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            using var globalJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(RootFinder.Find(), "global.json")));
            string sdkVersion = globalJson.RootElement.GetProperty("sdk").GetProperty("version").GetString()!;
            SdkVersion = sdkVersion;
            await GenerateAsync(Independent, IndependentSource, sdkVersion, includeMetadataModes: true, cancellationToken);
            await GenerateAsync(Dependencies, DependencySource, sdkVersion, includeMetadataModes: true, cancellationToken);
            await GenerateAsync(Rows, RowsSource, sdkVersion, includeMetadataModes: false, cancellationToken);
            await GenerateAsync(RuntimeRows, RowsSource.Replace("TestDataSourceDiscoveryOption.DuringDiscovery", "TestDataSourceDiscoveryOption.DuringExecution", StringComparison.Ordinal),
                sdkVersion, includeMetadataModes: false, cancellationToken);
        }

        private async Task GenerateAsync(string name, string tests, string sdkVersion, bool includeMetadataModes, CancellationToken cancellationToken)
        {
            string source = (ProjectSource + tests + ProbeSource)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$PlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$RetryVersion$", MicrosoftTestingExtensionsRetryVersion)
                .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$SdkVersion$", sdkVersion);
            TestAsset asset = await TestAsset.GenerateAssetAsync(name, source);
            _assets.Add(name, asset);
            MetadataMode[] modes = includeMetadataModes
                ? MetadataModesToRun
                : [MetadataMode.Reflection];
            foreach (MetadataMode mode in modes)
            {
                string metadataArguments = mode == MetadataMode.Reflection
                    ? string.Empty
                    : await AcceptanceSourceGen.PrepareBuildArgumentsAsync(asset.TargetAssetPath, mode);
                string logDirectory = Path.Combine(Constants.Root, "artifacts", "log", Constants.BuildConfiguration);
                Directory.CreateDirectory(logDirectory);
                string binlog = Path.Combine(logDirectory, $"partition-{name}-{mode}-{Guid.NewGuid():N}.binlog");
                DotnetMuxerResult build = await DotnetCli.RunAsync(
                    $"build \"{ProjectPath(name)}\" -c Release -p:SkipUapTargetFramework=true -bl:\"{binlog}\" {metadataArguments}",
                    workingDirectory: asset.TargetAssetPath,
                    environmentVariables: EnvironmentFor(name, forDotnetCli: true),
                    failIfReturnValueIsNotZero: false,
                    cancellationToken: cancellationToken);
                Assert.AreEqual(0, build.ExitCode, $"{name}/{mode}: {build.StandardOutput}{build.StandardError}");
                File.WriteAllText(Path.Combine(asset.TargetAssetPath, $"partition-build-{mode}.json"), JsonSerializer.Serialize(new
                {
                    Asset = name,
                    Project = ProjectPath(name),
                    MetadataMode = mode.ToString(),
                    ExpectedSdkVersion = sdkVersion,
                    build.ExitCode,
                    Binlog = binlog,
                    Host = Host(name, mode).FullName,
                    NuGetPackages = Path.Combine(asset.TargetAssetPath, ".nuget-packages"),
                }));
            }
        }

        public void Dispose()
        {
            foreach (TestAsset asset in _assets.Values)
            {
                asset.Dispose();
            }
        }

        private const string ProjectSource = """
            #file Acme.Tests.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <EnableMSTestRunner>true</EnableMSTestRunner>
                <TargetFramework>$TargetFramework$</TargetFramework>
                <AssemblyName>Acme.Tests</AssemblyName>
                <LangVersion>preview</LangVersion>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
                <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
                <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$PlatformVersion$" />
                <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$RetryVersion$" />
              </ItemGroup>
            </Project>
            #file global.json
            {
              "sdk": { "version": "$SdkVersion$", "allowPrerelease": true, "rollForward": "disable" },
              "test": { "runner": "Microsoft.Testing.Platform" }
            }

            """;

        private const string IndependentSource = """
            #file Tests.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: DoNotParallelize]
            namespace Acme;
            [TestClass]
            public sealed class CheckoutTests
            {
                [TestMethod, TestCategory("Fast")]
                public void Keep() => Probe.Record("Checkout.Keep");
                [TestMethod, TestCategory("Slow")]
                public void Other() => Probe.Record("Checkout.Other");
            }
            [TestClass]
            public sealed class CartTests
            {
                [TestMethod, TestCategory("Fast")]
                public void Keep() => Probe.Record("Cart.Keep");
                [TestMethod, TestCategory("Slow")]
                public void Other() => Probe.Record("Cart.Other");
            }
            [TestClass]
            public sealed class CaféTests
            {
                [TestMethod, TestCategory("Fast")]
                public void Coffee() => Probe.Record("Coffee");
            }

            """;

        private const string DependencySource = """
            #file Tests.cs
            using System;
            using System.IO;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [assembly: DoNotParallelize]
            namespace Acme;
            [TestClass]
            public sealed class CheckoutTests
            {
                [TestMethod]
                public void Prepare() => Probe.Record("Checkout.Prepare");
                [TestMethod]
                public void Other() => Probe.Record("Checkout.Other");
            }
            [TestClass]
            [DependsOn(typeof(CheckoutTests))]
            public sealed class CartTests
            {
                [TestMethod]
                [DependsOn(typeof(CheckoutTests), nameof(CheckoutTests.Prepare))]
                public void Buy()
                {
                    Probe.Record("Cart.Buy");
                    string? sentinel = Environment.GetEnvironmentVariable("PARTITION_TEST_RETRY_SENTINEL");
                    if (sentinel is not null && !File.Exists(sentinel))
                    {
                        File.WriteAllText(sentinel, "first attempt reached Buy");
                        Assert.Fail("Deliberate first attempt failure for partition UID narrowing");
                    }
                }
                [TestMethod]
                public void Other() => Probe.Record("Cart.Other");
            }

            """;

        private const string RowsSource = """
            #file Tests.cs
            using System;
            using System.Collections.Generic;
            using System.Reflection;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            #pragma warning disable MSTESTEXP
            [assembly: DoNotParallelize]
            [assembly: TestFilterProvider(typeof(Acme.SelectionFilter))]
            [assembly: TestDataSourceDiscovery(TestDataSourceDiscoveryOption.DuringDiscovery)]
            namespace Acme;
            public sealed class SelectionFilter : ITestFilter
            {
                public TestFilterResult Filter(TestFilterContext context) => context.MethodName switch
                {
                    "SkippedTest" => TestFilterResult.Skip("Skipped by the partition fixture provider"),
                    "DroppedTest" => TestFilterResult.Drop,
                    _ => TestFilterResult.Run,
                };
            }
            [TestClass]
            public sealed class CheckoutTests
            {
                [TestMethod]
                public void RunTest() => Probe.Record("RunTest");
                [TestMethod]
                public void SkippedTest() => Assert.Fail("Provider Skip must not run this body");
                [TestMethod]
                public void DroppedTest() => Assert.Fail("Provider Drop must not run this body");
                [TestMethod]
                [DataRow(17, DisplayName = "expanded seventeen")]
                [DataRow(23, DisplayName = "expanded twenty-three")]
                public void Expanded(int value)
                {
                    Assert.IsTrue(value is 17 or 23);
                    Probe.Record("Expanded:" + value);
                }
                public static IEnumerable<object[]> FoldedRows => new[] { new object[] { 31 }, new object[] { 37 } };
                [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
                [DynamicData(nameof(FoldedRows), DynamicDataDisplayName = nameof(FoldedName))]
                public void Folded(int value)
                {
                    Assert.IsTrue(value is 31 or 37);
                    Probe.Record("Folded:" + value);
                }
                public static string FoldedName(MethodInfo method, object[] data) => "folded " + data[0];
                public static IEnumerable<object[]> RuntimeValues => new[] { new object[] { 41 }, new object[] { 43 } };
                [TestMethod]
                [DynamicData(nameof(RuntimeValues), DynamicDataDisplayName = nameof(RuntimeName))]
                public void RuntimeOnly(int value)
                {
                    Assert.IsTrue(value is 41 or 43);
                    Probe.Record("RuntimeOnly:" + value);
                }
                public static string RuntimeName(MethodInfo method, object[] data) => "runtime " + data[0];
            }
            [TestClass]
            public sealed class CartTests
            {
                [TestMethod]
                public void Outside() => Probe.Record("Outside");
            }

            """;

        private const string ProbeSource = """
            #file Probe.cs
            using System;
            using System.IO;
            namespace Acme;
            internal static class Probe
            {
                public static void Record(string identity)
                {
                    string? path = Environment.GetEnvironmentVariable("PARTITION_TEST_PROBE");
                    if (path is not null)
                    {
                        File.AppendAllText(path, identity + Environment.NewLine);
                    }
                }
            }

            """;
    }
}
