// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
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
    private const string Cancellation = "PartitionCancellation";
    private const string Checkout = "Acme.CheckoutTests";
    private const string Cart = "Acme.CartTests";
    private const string Coffee = "Acme.Caf\u00e9Tests.Coffee";
    private const string ProbeVariable = "PARTITION_TEST_PROBE";
    private const string SentinelVariable = "PARTITION_TEST_RETRY_SENTINEL";

    private static readonly string[] Baseline = [$"{Checkout}.Keep", $"{Checkout}.Other", $"{Cart}.Keep", $"{Cart}.Other", Coffee];
    private static readonly string[] DependencyTests = [$"{Checkout}.Prepare", $"{Checkout}.Other", $"{Cart}.Buy", $"{Cart}.Other"];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, DisplayName = "Partition count SDK modules both pass")]
    [DataRow(true, DisplayName = "Partition count SDK modules preserve one failure")]
    public async Task PartitionCount_NativeDotnetTest_MultipleModulesPreserveResults(bool fail)
    {
        using var run = new TempDirectory("partition-count-modules");
        string solution = Path.Combine(run.Path, "Partitions.slnx");
        File.WriteAllText(solution, $"""
            <Solution>
              <Folder Name="/Independent/">
                <Project Path="{AssetFixture.ProjectPath(Independent)}" />
              </Folder>
              <Folder Name="/Dependencies/">
                <Project Path="{AssetFixture.ProjectPath(Dependencies)}" />
              </Folder>
            </Solution>
            """);
        string sentinel = Path.Combine(run.Path, "failure-reached");
        Dictionary<string, string?> environment = AssetFixture.EnvironmentFor(Independent, forDotnetCli: true);
        environment[ProbeVariable] = null;
        environment[SentinelVariable] = fail ? sentinel : null;
        string arguments = $"test --solution \"{solution}\" -c Release --no-build --max-parallel-test-modules 2"
            + $" --partition-count 4 --report-trx --report-trx-filename run.trx --results-directory \"{run.Path}\" --ansi off --progress off";
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            arguments, workingDirectory: AssetFixture.AssetPath(Independent), environmentVariables: environment,
            failIfReturnValueIsNotZero: false, cancellationToken: TestContext.CancellationToken);
        RecordChildRun(Independent, MetadataMode.Reflection, "partition-count-modules", arguments, run,
            result.ExitCode, result.StandardOutput, result.StandardError);
        Assert.AreEqual(fail ? 2 : 0, result.ExitCode, result.StandardOutput + result.StandardError);
        Assert.AreEqual(fail, File.Exists(sentinel));
        string[] modules = Directory.GetDirectories(Path.Combine(run.Path, "Partitions"));
        Assert.HasCount(2, modules);
        List<string> observedModules = [];
        foreach (string module in modules)
        {
            string[] reports = Directory.GetFiles(module, "run.trx", SearchOption.AllDirectories);
            Assert.HasCount(4, reports);
            ReportResult[] results = [.. reports.SelectMany(ReadResults)];
            bool dependencies = results.Any(test => test.FullyQualifiedName == $"{Cart}.Buy");
            observedModules.Add(dependencies ? Dependencies : Independent);
            string[] expected = dependencies ? DependencyTests : Baseline;
            Assert.AreSequenceEqual(
                expected.Select(name => name + (dependencies && fail && name == $"{Cart}.Buy" ? "|Failed" : "|Passed")),
                results.Select(test => test.FullyQualifiedName + "|" + test.Outcome),
                SequenceOrder.InAnyOrder);
        }

        Assert.AreSequenceEqual(new[] { Independent, Dependencies }, observedModules, SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    [DataRow("DOTNET_WATCH", DisplayName = "Partition count rejects dotnet watch")]
    [DataRow("TESTINGPLATFORM_HOTRELOAD_ENABLED", DisplayName = "Partition count rejects hot reload")]
    public async Task PartitionCount_HotReload_FailsBeforeExecution(string variable)
    {
        using var run = new TempDirectory("partition-count-hot-reload");
        Dictionary<string, string?> environment = AssetFixture.EnvironmentFor(Independent, run);
        environment[variable] = "1";
        TestHostResult result = await AssetFixture.Host(Independent).ExecuteAsync(
            "--partition-count 2", environmentVariables: environment, cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        result.AssertOutputContains("requires .NET 10 or later and local process execution");
        Assert.IsFalse(File.Exists(ProbePath(run)));
    }

    [TestMethod]
    public async Task PartitionCount_ExecutesEveryIdentityOnceInIsolatedReports()
    {
        DiscoveredNode[] discovered = await DiscoverAsync(Independent, string.Empty);
        using var run = new TempDirectory("partition-count");
        TestHostResult result = await RunFanOutAsync(Independent, "--partition-count 4", run);
        result.AssertExitCodeIs(ExitCode.Success);
        string[] reports = Directory.GetFiles(run.Path, "run.trx", SearchOption.AllDirectories);
        Assert.HasCount(4, reports);
        foreach (string report in reports)
        {
            int index = int.Parse(Path.GetFileName(Path.GetDirectoryName(report))!, System.Globalization.CultureInfo.InvariantCulture);
            AssertResults(ReadResults(report), ExpectedBucket(index));
        }

        ReportResult[] results = [.. reports.SelectMany(ReadResults)];
        AssertResults(results, Baseline);
        AssertDiscoveryIdentities(discovered, results);
        Assert.HasCount(Baseline.Length, results.Select(test => test.Id).Distinct(StringComparer.Ordinal));
        Assert.IsFalse(File.Exists(Path.Combine(run.Path, "run.trx")), "Partitions must not overwrite one shared report.");
    }

    [TestMethod]
    public async Task PartitionCount_ConfiguredOptionDoesNotRecurseIntoChildren()
    {
        using var configuration = new TempDirectory("partition-count-config");
        string configurationFile = Path.Combine(configuration.Path, "partition.testconfig.json");
        await TempDirectory.WriteFileAsync(configuration.Path, Path.GetFileName(configurationFile), """
            {
              "commandLineOptions": {
                "partition-count": [ "2" ]
              }
            }
            """);

        using var run = new TempDirectory("partition-count-configured");
        TestHostResult result = await RunFanOutAsync(
            Independent, $"--config-file \"{configurationFile}\"", run);

        result.AssertExitCodeIs(ExitCode.Success);
        string[] reports = Directory.GetFiles(run.Path, "run.trx", SearchOption.AllDirectories);
        Assert.HasCount(2, reports);
        AssertResults([.. reports.SelectMany(ReadResults)], Baseline);
    }

    [TestMethod]
    public async Task PartitionCount_FilterAndEmptySelections_PreserveAggregatePolicy()
    {
        foreach ((string filter, ExitCode exit, string[] expected) in new[]
        {
            ("FullyQualifiedName=Acme.CartTests.Keep", ExitCode.Success, new[] { $"{Cart}.Keep" }),
            ("Name=NotAnExistingMethod", ExitCode.ZeroTests, Array.Empty<string>()),
        })
        {
            using var run = new TempDirectory("partition-count-filter");
            TestHostResult result = await RunFanOutAsync(Independent, $"--partition-count 4 --filter {filter}", run);
            result.AssertExitCodeIs(exit);
            string[] reports = Directory.GetFiles(run.Path, "run.trx", SearchOption.AllDirectories);
            Assert.HasCount(4, reports);
            AssertResults([.. reports.SelectMany(ReadResults)], expected);
        }
    }

    [TestMethod]
    public async Task PartitionCount_FailureAndRetry_StayInsideDependencyPartition()
    {
        foreach (bool retry in new[] { false, true })
        {
            using var run = new TempDirectory("partition-count-retry");
            string sentinel = Path.Combine(run.Path, "retry-sentinel.txt");
            TestHostResult result = await RunFanOutAsync(
                Dependencies, "--partition-count 4" + (retry ? " --retry-failed-tests 1" : string.Empty), run,
                new Dictionary<string, string?> { [SentinelVariable] = sentinel });
            result.AssertExitCodeIs(retry ? ExitCode.Success : ExitCode.AtLeastOneTestFailed);
            Assert.IsTrue(File.Exists(sentinel), "The deliberate failure must execute.");
            string partitionRoot = Assert.ContainsSingle(Directory.GetDirectories(Path.Combine(run.Path, "Partitions")));
            string activePartition = Path.Combine(partitionRoot, "4");
            ReportResult[] first = ReadResults(retry
                ? Assert.ContainsSingle(Directory.GetFiles(Path.Combine(activePartition, "Retries"), "run.trx", SearchOption.AllDirectories))
                : Path.Combine(activePartition, "run.trx"));
            Assert.AreSequenceEqual(
                DependencyTests.Select(name => name + (name == $"{Cart}.Buy" ? "|Failed" : "|Passed")),
                first.Select(test => test.FullyQualifiedName + "|" + test.Outcome),
                SequenceOrder.InAnyOrder);
            if (retry)
            {
                ReportResult[] final = ReadResults(Path.Combine(activePartition, "run.trx"));
                AssertResults(final, [$"{Cart}.Buy"]);
                Assert.AreEqual(first.Single(test => test.FullyQualifiedName == $"{Cart}.Buy").Id, final.Single().Id);
            }
        }
    }

    [TestMethod]
    public async Task PartitionCount_Discovery_DoesNotFanOut()
        => AssertDiscovery(await DiscoverAsync(Independent, "--partition-count 4"), Baseline);

    [TestMethod]
    public async Task PartitionCount_LongResponseFile_PreservesSelection()
    {
        using var run = new TempDirectory("partition-count-response");
        string responseFile = Path.Combine(run.Path, "original.rsp");
        File.WriteAllText(responseFile, $"""
            --partition-count 4
            --filter FullyQualifiedName=Acme.CartTests.Keep|Name={new string('z', 35_000)}
            """);
        TestHostResult result = await RunFanOutAsync(Independent, $"@\"{responseFile}\"", run);
        result.AssertExitCodeIs(ExitCode.Success);
        string[] reports = Directory.GetFiles(run.Path, "run.trx", SearchOption.AllDirectories);
        Assert.HasCount(4, reports);
        AssertResults([.. reports.SelectMany(ReadResults)], [$"{Cart}.Keep"]);
    }

    [TestMethod]
    public async Task PartitionCount_MixedCaseOptions_ExecuteAllPartitions()
    {
        using var run = new TempDirectory("partition-count-case");
        TestHostResult result = await RunFanOutAsync(Independent, "--PARTITION-COUNT 2", run);
        result.AssertExitCodeIs(ExitCode.Success);
        string[] reports = Directory.GetFiles(run.Path, "run.trx", SearchOption.AllDirectories);
        Assert.HasCount(2, reports);
        AssertResults([.. reports.SelectMany(ReadResults)], Baseline);
    }

    [TestMethod]
    public async Task PartitionCount_LeadingHyphensInParameter_AreForwarded()
    {
        using var run = new TempDirectory("partition-count-hyphens");
        TestHostResult result = await RunFanOutAsync(
            Independent, "--test-parameter ---ansi=off --partition-count 2", run,
            new Dictionary<string, string?> { ["PARTITION_TEST_HYPHENS"] = "1" });
        result.AssertExitCodeIs(ExitCode.Success);
        string[] reports = Directory.GetFiles(run.Path, "run.trx", SearchOption.AllDirectories);
        Assert.HasCount(2, reports);
        AssertResults([.. reports.SelectMany(ReadResults)], Baseline);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Partition middleware wraps standalone distribution once")]
    [DataRow(true, DisplayName = "Partition middleware wraps distribution and retry once")]
    public async Task PartitionCount_UnconditionalMiddleware_WrapsOnlyTheOuterProcess(bool retry)
    {
        using var run = new TempDirectory("partition-count-middleware");
        string marker = Path.Combine(run.Path, "middleware.txt");
        TestHostResult result = await RunFanOutAsync(
            Cancellation, "--partition-count 2" + (retry ? " --retry-failed-tests 1" : string.Empty), run,
            new Dictionary<string, string?> { ["PARTITION_TEST_MIDDLEWARE"] = marker });
        result.AssertExitCodeIs(ExitCode.Success);
        Assert.AreSequenceEqual(new[] { "middleware" }, File.ReadAllLines(marker));
        string[] reports = Directory.GetFiles(run.Path, "run.trx", SearchOption.AllDirectories);
        Assert.HasCount(2, reports);
        AssertResults([.. reports.SelectMany(ReadResults)], Baseline);
    }

    [TestMethod]
    public async Task PartitionCount_ResponseFileAndMultilineArgument_PreserveParameter()
    {
        using var run = new TempDirectory("partition-count-multiline");
        string responseFile = Path.Combine(run.Path, "original.rsp");
        File.WriteAllText(responseFile, $"--partition-count 2\n--filter FullyQualifiedName~Acme|Name={new string('z', 35_000)}");
        TestHostResult result = await RunFanOutAsync(
            Independent, $"@\"{responseFile}\" --test-parameter \"Multiline=first\nsecond\"", run,
            new Dictionary<string, string?> { ["PARTITION_TEST_MULTILINE"] = "1" });
        result.AssertExitCodeIs(ExitCode.Success);
        string[] reports = Directory.GetFiles(run.Path, "run.trx", SearchOption.AllDirectories);
        Assert.HasCount(2, reports);
        AssertResults([.. reports.SelectMany(ReadResults)], Baseline);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Temporary response files are removed after success")]
    [DataRow(true, DisplayName = "Temporary response files are removed after child failure")]
    public async Task PartitionCount_ResponseFiles_ArePrivateAndRemoved(bool failChild)
    {
        using var run = new TempDirectory("partition-count-private-response");
        using var input = new TempDirectory("partition-count-input-response");
        string original = Path.Combine(input.Path, "original.rsp");
        File.WriteAllText(original, "--partition-count 1");
        string temp = Path.Combine(run.Path, "temp");
        string captures = Path.Combine(run.Path, "captures");
        Directory.CreateDirectory(temp);
        Directory.CreateDirectory(captures);
        Dictionary<string, string?> environment = AssetFixture.EnvironmentFor(Cancellation);
        environment["TEMP"] = temp;
        environment["TMP"] = temp;
        environment["TMPDIR"] = temp;
        environment["PARTITION_TEST_RESPONSE_CAPTURE"] = captures;
        environment["PARTITION_TEST_RESPONSE_ORIGINAL"] = original;
        environment["PARTITION_TEST_FAIL_CHILD"] = failChild ? "1" : null;
        TestHostResult result = await AssetFixture.Host(Cancellation).ExecuteAsync(
            $"@\"{original}\" --results-directory \"{run.Path}\"",
            environmentVariables: environment, cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(failChild ? 42 : 0, result.ExitCode, result.StandardOutput + result.StandardError);
        string capture = Assert.ContainsSingle(Directory.GetFiles(captures, "*.txt"));
        string[] paths = File.ReadAllLines(capture);
        Assert.IsNotEmpty(paths);
        foreach (string path in paths)
        {
            Assert.StartsWith(Path.GetFullPath(temp) + Path.DirectorySeparatorChar, Path.GetFullPath(path));
            Assert.IsFalse(File.Exists(path), $"Response file was not removed: {path}");
        }

        Assert.IsEmpty(Directory.GetFiles(Path.Combine(run.Path, "Partitions"), "*.rsp", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task PartitionCount_NativeDotnetTest_PreservesChildStartupDiagnostic()
    {
        using var run = new TempDirectory("partition-count-sdk-diagnostic");
        Dictionary<string, string?> environment = AssetFixture.EnvironmentFor(Cancellation, forDotnetCli: true);
        environment["PARTITION_TEST_CHILD_DIAGNOSTIC"] = "1";
        string arguments = $"test --project \"{AssetFixture.ProjectPath(Cancellation)}\" -c Release --no-build"
            + $" --partition-count 1 --results-directory \"{run.Path}\" --ansi off --progress off";
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            arguments, workingDirectory: AssetFixture.AssetPath(Cancellation), environmentVariables: environment,
            failIfReturnValueIsNotZero: false, cancellationToken: TestContext.CancellationToken);
        Assert.AreNotEqual(0, result.ExitCode);
        Assert.Contains("partition startup diagnostic marker", result.StandardOutput + result.StandardError);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "Enabled custom launcher is rejected")]
    [DataRow(false, DisplayName = "Disabled custom launcher permits fan out")]
    public async Task PartitionCount_CustomLauncher_RejectsOnlyEnabledLauncher(bool enabled)
    {
        using var run = new TempDirectory("partition-count-launcher");
        Dictionary<string, string?> environment = AssetFixture.EnvironmentFor(Cancellation);
        environment["PARTITION_TEST_CUSTOM_LAUNCHER"] = enabled ? "enabled" : "disabled";
        environment[ProbeVariable] = null;
        TestHostResult result = await AssetFixture.Host(Cancellation).ExecuteAsync(
            $"--partition-count 2 --results-directory \"{run.Path}\"",
            environmentVariables: environment, cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(enabled ? ExitCode.InvalidCommandLine : ExitCode.Success);
        if (enabled)
        {
            result.AssertOutputContains("requires .NET 10 or later and local process execution");
            Assert.IsFalse(Directory.Exists(Path.Combine(run.Path, "Partitions")));
        }
    }

    [TestMethod]
    [DataRow(true, DisplayName = "Cancellation stops inherited pipe readers")]
    [DataRow(false, DisplayName = "Inherited pipe readers time out without cancellation")]
    public async Task PartitionCount_ChildExitWithInheritedPipes_StopsReaders(bool cancel)
    {
        using var run = new TempDirectory("partition-count-orphan");
        ProcessStartInfo start = new(
            AssetFixture.Host(Cancellation).FullName,
            $"--partition-count 1 --filter FullyQualifiedName=Acme.CheckoutTests.Keep --results-directory \"{run.Path}\" --ansi off --progress off")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["DOTNET_ROOT"] = Path.Combine(RootFinder.Find(), ".dotnet");
        start.Environment["PARTITION_TEST_HANG"] = run.Path;
        start.Environment["PARTITION_TEST_ORPHAN"] = "1";
        using Process parent = Process.Start(start)!;
        Task<string> stdout = parent.StandardOutput.ReadToEndAsync();
        Task<string> stderr = parent.StandardError.ReadToEndAsync();
        Process? child = null;
        Process? grandchild = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(1));
        try
        {
            string marker = Path.Combine(run.Path, "Checkout.Keep");
            while (!File.Exists(marker))
            {
                await Task.Delay(25, deadline.Token);
            }

            child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(marker, deadline.Token), System.Globalization.CultureInfo.InvariantCulture));
            grandchild = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(marker + ".grandchild", deadline.Token), System.Globalization.CultureInfo.InvariantCulture));
            await File.WriteAllTextAsync(Path.Combine(run.Path, "release-child"), "release", deadline.Token);
            await child.WaitForExitAsync(deadline.Token);
            Assert.IsFalse(grandchild.HasExited);
            if (cancel)
            {
                await File.WriteAllTextAsync(Path.Combine(run.Path, "cancel"), "cancel application", deadline.Token);
            }

            Task exited = parent.WaitForExitAsync(deadline.Token);
            Assert.AreSame(exited, await Task.WhenAny(exited, Task.Delay(TimeSpan.FromSeconds(cancel ? 15 : 45), deadline.Token)),
                "Readers must stop even when a descendant retains inherited output handles.");
            await exited;
            if (cancel)
            {
                Assert.AreEqual((int)ExitCode.TestSessionAborted, parent.ExitCode);
            }
            else
            {
                Assert.AreNotEqual((int)ExitCode.Success, parent.ExitCode);
                grandchild.Kill(entireProcessTree: true);
                await grandchild.WaitForExitAsync(deadline.Token);
                Assert.Contains("Timed out while draining partition output.", await stdout + await stderr);
            }
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            foreach (Process? process in new[] { parent, child, grandchild })
            {
                if (process is not null)
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }

                    await process.WaitForExitAsync(cleanup.Token);
                }
            }

            await Task.WhenAll(stdout, stderr);
            child?.Dispose();
            grandchild?.Dispose();
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Cancellation joins uncooperative children")]
    [DataRow(true, DisplayName = "Cancellation removes temporary response files")]
    public async Task PartitionCount_Cancellation_JoinsUncooperativeChildren(bool useResponseFile)
    {
        using var run = new TempDirectory("partition-count-cancel");
        string original = Path.Combine(run.Path, "original.rsp");
        string captures = Path.Combine(run.Path, "captures");
        string temp = Path.Combine(run.Path, "temp");
        Directory.CreateDirectory(captures);
        Directory.CreateDirectory(temp);
        File.WriteAllText(original, "--filter FullyQualifiedName~Acme");
        ProcessStartInfo start = new(
            AssetFixture.Host(Cancellation).FullName,
            $"--partition-count 2 {(useResponseFile ? $"@\"{original}\"" : string.Empty)} --results-directory \"{run.Path}\" --ansi off --progress off")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["DOTNET_ROOT"] = Path.Combine(RootFinder.Find(), ".dotnet");
        start.Environment["PARTITION_TEST_HANG"] = run.Path;
        if (useResponseFile)
        {
            start.Environment["PARTITION_TEST_RESPONSE_CAPTURE"] = captures;
            start.Environment["PARTITION_TEST_RESPONSE_ORIGINAL"] = original;
            start.Environment["TEMP"] = temp;
            start.Environment["TMP"] = temp;
            start.Environment["TMPDIR"] = temp;
        }

        using Process parent = Process.Start(start)!;
        Task<string> stdout = parent.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        Task<string> stderr = parent.StandardError.ReadToEndAsync(TestContext.CancellationToken);
        List<Process> children = [];
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(1));
        try
        {
            foreach (string identity in new[] { "Checkout.Keep", "Cart.Keep" }.Take(Math.Min(2, Environment.ProcessorCount)))
            {
                string marker = Path.Combine(run.Path, identity);
                while (!File.Exists(marker))
                {
                    await Task.Delay(25, deadline.Token);
                }

                children.Add(Process.GetProcessById(int.Parse(
                    await File.ReadAllTextAsync(marker, deadline.Token), System.Globalization.CultureInfo.InvariantCulture)));
                children.Add(Process.GetProcessById(int.Parse(
                    await File.ReadAllTextAsync(marker + ".grandchild", deadline.Token), System.Globalization.CultureInfo.InvariantCulture)));
            }

            await File.WriteAllTextAsync(Path.Combine(run.Path, "cancel"), "cancel application", deadline.Token);
            await parent.WaitForExitAsync(deadline.Token);
            await Task.WhenAll(children.Select(child => child.WaitForExitAsync(deadline.Token)));
            Assert.AreEqual((int)ExitCode.TestSessionAborted, parent.ExitCode, await stdout + await stderr);
            Assert.IsTrue(children.All(child => child.HasExited), "Returning from cancellation must join every active partition.");
            if (useResponseFile)
            {
                string[] files = Directory.GetFiles(captures, "*.txt");
                Assert.HasCount(Math.Min(2, Environment.ProcessorCount), files);
                foreach (string path in files.SelectMany(File.ReadAllLines))
                {
                    Assert.IsFalse(File.Exists(path), $"Response file was not removed: {path}");
                }
            }
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            foreach (Process process in children.Append(parent))
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync(cleanup.Token);
            }

            await Task.WhenAll(stdout, stderr);
            foreach (Process child in children)
            {
                child.Dispose();
            }
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Partition count native SDK without retry")]
    [DataRow(true, DisplayName = "Partition count native SDK with per-partition retry")]
    public async Task PartitionCount_NativeDotnetTest_ExecutesAllPartitions(bool retry)
    {
        using var run = new TempDirectory("partition-count-sdk");
        string arguments = $"test --project \"{AssetFixture.ProjectPath(Independent)}\" -c Release --no-build -p:SkipUapTargetFramework=true"
            + $" --partition-count 4 --report-trx --report-trx-filename run.trx --results-directory \"{run.Path}\" --ansi off --progress off";
        if (retry)
        {
            arguments += " --retry-failed-tests 1";
        }

        Dictionary<string, string?> environment = AssetFixture.EnvironmentFor(Independent, forDotnetCli: true);
        environment[ProbeVariable] = null;
        environment["PARTITION_TEST_FANOUT_RETRY"] = retry ? run.Path : null;
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            arguments, workingDirectory: AssetFixture.AssetPath(Independent), environmentVariables: environment,
            failIfReturnValueIsNotZero: false, cancellationToken: TestContext.CancellationToken);
        RecordChildRun(Independent, MetadataMode.Reflection, "partition-count-sdk", arguments, run,
            result.ExitCode, result.StandardOutput, result.StandardError);
        Assert.AreEqual(0, result.ExitCode, result.StandardOutput + result.StandardError);
        string partitionRoot = Assert.ContainsSingle(Directory.GetDirectories(Path.Combine(run.Path, "Partitions")));
        List<ReportResult> logicalResults = [];
        foreach (string partition in Directory.GetDirectories(partitionRoot))
        {
            string retries = Path.Combine(partition, "Retries");
            IEnumerable<ReportResult> firstAttempts = Directory.Exists(retries)
                ? Directory.GetFiles(retries, "run.trx", SearchOption.AllDirectories).SelectMany(ReadResults)
                : [];
            logicalResults.AddRange(firstAttempts.Concat(ReadResults(Path.Combine(partition, "run.trx")))
                .GroupBy(test => test.Id, StringComparer.Ordinal).Select(group => group.Last()));
        }

        AssertResults([.. logicalResults], Baseline);
        if (retry)
        {
            Assert.IsTrue(File.Exists(Path.Combine(run.Path, "Checkout.Keep")));
            Assert.IsTrue(File.Exists(Path.Combine(run.Path, "Cart.Keep")));
            Assert.IsTrue(File.Exists(Path.Combine(run.Path, "checkout-recovered")));
        }
    }

    [TestMethod]
    [DataRow("--partition-count", DisplayName = "Partition count rejects missing count")]
    [DataRow("--partition-count 2 extra", DisplayName = "Partition count rejects extra count")]
    [DataRow("--partition-count 0", DisplayName = "Partition count rejects zero")]
    [DataRow("--partition-count=-1", DisplayName = "Partition count rejects negative")]
    [DataRow("--partition-count 2147483648", DisplayName = "Partition count rejects overflow")]
    [DataRow("--partition-count 1/2", DisplayName = "Partition count rejects fraction")]
    [DataRow("--partition-count 2 --partition 1/2", DisplayName = "Partition count rejects manual partition")]
    [DataRow("--partition-count 2 --server jsonrpc", DisplayName = "Partition count rejects JSON RPC server")]
    public async Task PartitionCount_InvalidOptions_FailBeforeExecution(string arguments)
    {
        using var run = new TempDirectory("partition-count-invalid");
        TestHostResult result = await AssetFixture.Host(Independent).ExecuteAsync(
            arguments, environmentVariables: AssetFixture.EnvironmentFor(Independent, run),
            cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        result.AssertOutputContains("--partition-count");
        Assert.IsFalse(File.Exists(ProbePath(run)));
    }

    private async Task<TestHostResult> RunFanOutAsync(
        string asset, string arguments, TempDirectory run, Dictionary<string, string?>? extraEnvironment = null)
    {
        Dictionary<string, string?> environment = AssetFixture.EnvironmentFor(asset);
        // The selector fixture's shared append-only body log is intentionally process-local.
        // Fan-out asserts the independent per-partition TRX identities instead.
        environment[ProbeVariable] = null;
        if (extraEnvironment is not null)
        {
            foreach ((string key, string? value) in extraEnvironment)
            {
                environment[key] = value;
            }
        }

        TestHostResult result = await AssetFixture.Host(asset).ExecuteAsync(
            $"{arguments} --report-trx --report-trx-filename run.trx --results-directory \"{run.Path}\"",
            environmentVariables: environment, cancellationToken: TestContext.CancellationToken);
        RecordChildRun(asset, MetadataMode.Reflection, "partition-count", arguments, run,
            result.ExitCode, result.StandardOutput, result.StandardError);
        return result;
    }

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
                + $" --report-trx --report-trx-filename run.trx --results-directory \"{run.Path}\" --ansi off --progress off";
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
        Assert.HasCount(1, reports);
        ReportResult[] first = ReadResults(reports.Single(path => Path.GetFileName(Path.GetDirectoryName(path)) == "1"));
        // The final attempt is moved to the results root so it is published only once.
        string[] finalReports = Directory.GetFiles(run.Path, "*.trx", SearchOption.TopDirectoryOnly);
        Assert.HasCount(1, finalReports);
        ReportResult[] second = ReadResults(finalReports.Single());
        Assert.AreSequenceEqual(
            DependencyTests.Select(name => name + (name == $"{Cart}.Buy" ? "|Failed" : "|Passed")),
            first.Select(test => test.FullyQualifiedName + "|" + test.Outcome),
            SequenceOrder.InAnyOrder);
        AssertResults(second, [$"{Cart}.Buy"]);
        Assert.AreEqual(first.Single(test => test.FullyQualifiedName == $"{Cart}.Buy").Id, second.Single().Id);
        Assert.AreSequenceEqual(
            first.Where(test => test.Outcome == "Failed").Select(test => test.Id + "|" + test.FullyQualifiedName + "|Passed"),
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
            await GenerateAsync(Cancellation, IndependentSource + CancellationProgram, sdkVersion, includeMetadataModes: false, cancellationToken);
        }

        private async Task GenerateAsync(string name, string tests, string sdkVersion, bool includeMetadataModes, CancellationToken cancellationToken)
        {
            string source = (ProjectSource + tests + ProbeSource)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$PlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$RetryVersion$", MicrosoftTestingExtensionsRetryVersion)
                .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$SdkVersion$", sdkVersion);
            if (name == Cancellation)
            {
                source = source.Replace(
                    "<EnableMSTestRunner>true</EnableMSTestRunner>",
                    "<EnableMSTestRunner>true</EnableMSTestRunner><GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>", StringComparison.Ordinal);
            }

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
                public TestContext TestContext { get; set; } = null!;

                [TestMethod, TestCategory("Fast")]
                public void Keep()
                {
                    if (System.Environment.GetEnvironmentVariable("PARTITION_TEST_MULTILINE") is not null)
                    {
                        Assert.AreEqual("first\nsecond", TestContext.Properties["Multiline"]);
                    }

                    if (System.Environment.GetEnvironmentVariable("PARTITION_TEST_HYPHENS") is not null)
                    {
                        Assert.AreEqual("off", TestContext.Properties["---ansi"]);
                    }

                    Probe.Record("Checkout.Keep");
                }
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

        private const string CancellationProgram = """
            #file Program.cs
            #pragma warning disable TPEXP
            using System;
            using System.IO;
            using System.Linq;
            using System.Reflection;
            using System.Security.AccessControl;
            using System.Security.Principal;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Testing.Extensions;
            using Microsoft.Testing.Platform.Builder;
            using Microsoft.Testing.Platform.Extensions.TestHostControllers;
            using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
            using Microsoft.Testing.Platform.TestHostOrchestrator;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public static class Program
            {
                public static async Task<int> Main(string[] args)
                {
                    if (args is ["--sentinel"])
                    {
                        Thread.Sleep(Timeout.Infinite);
                    }

                    string? capture = Environment.GetEnvironmentVariable("PARTITION_TEST_RESPONSE_CAPTURE");
                    if (capture is not null)
                    {
                        string original = Environment.GetEnvironmentVariable("PARTITION_TEST_RESPONSE_ORIGINAL")!;
                        string[] paths = args.Where(arg => arg.StartsWith("@", StringComparison.Ordinal))
                            .Select(arg => arg.Substring(1))
                            .Where(path => !string.Equals(path, original, StringComparison.OrdinalIgnoreCase))
                            .ToArray();
                        if (paths.Length > 0)
                        {
                            foreach (string path in paths)
                            {
                                if (!File.Exists(path))
                                {
                                    throw new InvalidOperationException("The child response file is missing.");
                                }
                                if (!OperatingSystem.IsWindows()
                                    && (File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                                        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
                                {
                                    throw new InvalidOperationException("The child response file is not private.");
                                }
                                if (OperatingSystem.IsWindows())
                                {
                                    FileSecurity security = new FileInfo(path).GetAccessControl();
                                    using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                                    if (!security.AreAccessRulesProtected || security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                                        .Cast<FileSystemAccessRule>()
                                        .Any(rule => rule.AccessControlType == AccessControlType.Allow && !rule.IdentityReference.Equals(identity.User)))
                                    {
                                        throw new InvalidOperationException("The child response file is not private.");
                                    }
                                }
                            }
                            File.WriteAllLines(Path.Combine(capture, Environment.ProcessId + ".txt"), paths);
                            if (Environment.GetEnvironmentVariable("PARTITION_TEST_FAIL_CHILD") is not null)
                            {
                                return 42;
                            }
                        }
                    }

                    if (args.Contains("--partition") && Environment.GetEnvironmentVariable("PARTITION_TEST_CHILD_DIAGNOSTIC") is not null)
                    {
                        Console.Error.WriteLine("partition startup diagnostic marker");
                        return 42;
                    }

                    var builder = await TestApplication.CreateBuilderAsync(args);
                    if (args.Contains("--partition-count") && Environment.GetEnvironmentVariable("PARTITION_TEST_HANG") is not null)
                    {
                        builder.TestHostOrchestrator.AddTestHostExecutionOrchestratorMiddleware(services => new CancelOnMarker(services));
                    }

                    if (Environment.GetEnvironmentVariable("PARTITION_TEST_MIDDLEWARE") is string marker)
                    {
                        builder.TestHostOrchestrator.AddTestHostExecutionOrchestratorMiddleware(_ => new RecordMiddleware(marker));
                    }

                    if (Environment.GetEnvironmentVariable("PARTITION_TEST_CUSTOM_LAUNCHER") is string launcher)
                    {
                        builder.TestHostControllers.AddTestHostLauncher(_ => new CustomLauncher(launcher == "enabled"));
                    }

                    builder.AddMSTest(() => new[] { Assembly.GetExecutingAssembly() });
                    builder.AddTrxReportProvider();
                    builder.AddRetryProvider();
                    using var app = await builder.BuildAsync();
                    return await app.RunAsync();
                }
            }
            internal sealed class CustomLauncher(bool enabled) : ITestHostLauncher
            {
                public string Uid => "CustomLauncher";
                public string Version => "1.0.0";
                public string DisplayName => Uid;
                public string Description => Uid;
                public Task<bool> IsEnabledAsync() => Task.FromResult(enabled);
                public Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken token)
                    => throw new InvalidOperationException("The custom launcher must not be invoked.");
            }
            internal sealed class RecordMiddleware(string marker) : ITestHostExecutionOrchestratorMiddleware
            {
                public string Uid => "RecordMiddleware";
                public string Version => "1.0.0";
                public string DisplayName => Uid;
                public string Description => Uid;
                public Task<bool> IsEnabledAsync() => Task.FromResult(true);
                public Task<int> OrchestrateTestHostExecutionAsync(Func<CancellationToken, Task<int>> next, CancellationToken token)
                {
                    File.AppendAllText(marker, "middleware" + Environment.NewLine);
                    return next(token);
                }
            }
            internal sealed class CancelOnMarker(IServiceProvider services) : ITestHostExecutionOrchestratorMiddleware
            {
                public string Uid => "CancelOnMarker";
                public string Version => "1.0.0";
                public string DisplayName => Uid;
                public string Description => Uid;
                public Task<bool> IsEnabledAsync() => Task.FromResult(true);
                public async Task<int> OrchestrateTestHostExecutionAsync(Func<CancellationToken, Task<int>> next, CancellationToken token)
                {
                    // This test-only trigger requests genuine application cancellation without
                    // broadcasting console signals to unrelated processes on a shared machine.
                    Type type = typeof(TestApplication).Assembly.GetType(
                        "Microsoft.Testing.Platform.Services.ITestApplicationCancellationTokenSource", throwOnError: true)!;
                    object source = services.GetService(type)!;
                    Task<int> execution = next(token);
                    try
                    {
                        string marker = Path.Combine(Environment.GetEnvironmentVariable("PARTITION_TEST_HANG")!, "cancel");
                        while (!File.Exists(marker) && !execution.IsCompleted)
                        {
                            await Task.Delay(25, token);
                        }
                    }
                    finally
                    {
                        type.GetMethod("Cancel")!.Invoke(source, null);
                        await execution;
                    }

                    return await execution;
                }
            }

            """;

        private const string ProbeSource = """
            #file Probe.cs
            using System;
            using System.IO;
            using System.Threading;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            namespace Acme;
            internal static class Probe
            {
                public static void Record(string identity)
                {
                    string? hangDirectory = Environment.GetEnvironmentVariable("PARTITION_TEST_HANG");
                    if (hangDirectory is not null)
                    {
                        string marker = Path.Combine(hangDirectory, identity);
                        var grandchildStart = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, "--sentinel")
                        {
                            UseShellExecute = false,
                        };
                        using var grandchild = System.Diagnostics.Process.Start(grandchildStart)!;
                        File.WriteAllText(marker + ".grandchild", grandchild.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        string temporary = marker + ".tmp";
                        File.WriteAllText(temporary, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        File.Move(temporary, marker);
                        if (Environment.GetEnvironmentVariable("PARTITION_TEST_ORPHAN") is not null)
                        {
                            while (!File.Exists(Path.Combine(hangDirectory, "release-child")))
                            {
                                Thread.Sleep(25);
                            }

                            return;
                        }

                        Thread.Sleep(Timeout.Infinite);
                    }

                    string? retryDirectory = Environment.GetEnvironmentVariable("PARTITION_TEST_FANOUT_RETRY");
                    if (retryDirectory is not null && identity is "Checkout.Keep" or "Cart.Keep")
                    {
                        string recovered = Path.Combine(retryDirectory, "checkout-recovered");
                        if (identity == "Cart.Keep")
                        {
                            Assert.IsTrue(SpinWait.SpinUntil(() => File.Exists(recovered), TimeSpan.FromMinutes(1)),
                                "Checkout must finish its retry before Cart starts attempt one.");
                        }

                        string sentinel = Path.Combine(retryDirectory, identity);
                        if (!File.Exists(sentinel))
                        {
                            File.WriteAllText(sentinel, "first attempt reached");
                            Assert.Fail("Deliberate partition-local first attempt failure");
                        }

                        if (identity == "Checkout.Keep")
                        {
                            File.WriteAllText(recovered, "checkout recovered");
                        }
                    }

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
