// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ParallelOutputIsolationTests : AcceptanceTestBase<ParallelOutputIsolationTests.TestAssetFixture>
{
    private const string AssetName = "ParallelOutputIsolation";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task SynchronousParallelOutputIsIsolatedPerTestResult(string tfm)
        => await ValidateOutputIsolationAsync(tfm, "SynchronousOutputCases", "SyncMethod");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task AsynchronousParallelOutputIsIsolatedPerTestResult(string tfm)
        => await ValidateOutputIsolationAsync(tfm, "AsynchronousOutputCases", "AsyncMethod");

    private async Task ValidateOutputIsolationAsync(string tfm, string className, string methodPrefix)
    {
        string fileName = $"{className}-{Guid.NewGuid():N}.trx";
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult hostResult = await testHost.ExecuteAsync(
            $"--filter ClassName=ParallelOutputIsolation.{className} --report-trx --report-trx-filename {fileName}",
            cancellationToken: TestContext.CancellationToken);

        hostResult.AssertExitCodeIs(ExitCode.Success);
        hostResult.AssertOutputContainsSummary(failed: 0, passed: 3, skipped: 0);

        string trxFile = Directory.GetFiles(testHost.DirectoryName, fileName, SearchOption.AllDirectories).Single();
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement[] results = [.. XDocument.Load(trxFile).Descendants(ns + "UnitTestResult")];
        Assert.HasCount(3, results);
        Assert.IsTrue(results.All(result => (string?)result.Attribute("outcome") == "Passed"));
        Assert.IsTrue(
            results.All(result => TimeSpan.Parse((string)result.Attribute("duration")!, CultureInfo.InvariantCulture) > TimeSpan.Zero),
            "Every result should report a concrete positive duration.");

        DateTimeOffset firstEnd = results.Min(result => DateTimeOffset.Parse((string)result.Attribute("endTime")!, CultureInfo.InvariantCulture));
        Assert.IsGreaterThan(
            1,
            results.Count(result => DateTimeOffset.Parse((string)result.Attribute("startTime")!, CultureInfo.InvariantCulture) < firstEnd),
            "The synchronization gate guarantees that multiple tests overlap before the first test ends.");

        for (int index = 1; index <= 3; index++)
        {
            string ownMethod = $"{methodPrefix}{index}";
            string[] otherMethods = Enumerable.Range(1, 3)
                .Where(otherIndex => otherIndex != index)
                .Select(otherIndex => $"{methodPrefix}{otherIndex}")
                .ToArray();

            XElement result = results.Single(result => (string?)result.Attribute("testName") == ownMethod);
            string standardOutput = result.Descendants(ns + "StdOut").Single().Value;
            string standardError = result.Descendants(ns + "StdErr").Single().Value;

            AssertOutputChannelIsIsolated(standardOutput, ownMethod, otherMethods);
            Assert.Contains("Debug Trace:", standardOutput);
            AssertOutputChannelIsIsolated(standardError, ownMethod, otherMethods);
        }

        string allOutput = string.Join(
            Environment.NewLine,
            results.SelectMany(result => result.Descendants(ns + "StdOut").Concat(result.Descendants(ns + "StdErr"))).Select(element => element.Value));
        Assert.Contains($"{className} ClassInitialize", allOutput);
        Assert.Contains($"{className} ClassCleanup", allOutput);
    }

    private static void AssertOutputChannelIsIsolated(
        string output,
        string ownMethod,
        string[] otherMethods)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(output), $"No matching output was recorded for {ownMethod}.");
        Assert.Contains("TestInitialize", output);
        Assert.Contains(ownMethod, output);
        Assert.Contains("TestCleanup", output);

        foreach (string otherMethod in otherMethods)
        {
            Assert.DoesNotContain(otherMethod, output, $"Output from {otherMethod} leaked into {ownMethod}.");
        }
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [];

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file ParallelOutputIsolation.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file Assembly.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
[assembly: Parallelize(Workers = 3, Scope = ExecutionScope.MethodLevel)]

#file OutputCases.cs
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ParallelOutputIsolation;

[TestClass]
public class SynchronousOutputCases
{
    private static readonly Barrier Gate = new(3);

    [ClassInitialize]
    public static void ClassInitialize(TestContext context) => Write("SynchronousOutputCases ClassInitialize");

    [TestInitialize]
    public void TestInitialize() => Write("TestInitialize");

    [TestCleanup]
    public void TestCleanup() => Write("TestCleanup");

    [ClassCleanup]
    public static void ClassCleanup() => Write("SynchronousOutputCases ClassCleanup");

    [TestMethod] public void SyncMethod1() => Run(nameof(SyncMethod1));
    [TestMethod] public void SyncMethod2() => Run(nameof(SyncMethod2));
    [TestMethod] public void SyncMethod3() => Run(nameof(SyncMethod3));

    private static void Run(string methodName)
    {
        Write($"{methodName} before gate");
        Assert.IsTrue(Gate.SignalAndWait(TimeSpan.FromSeconds(30)), "All synchronous tests should reach the gate.");
        Write($"{methodName} after gate");
    }

    private static void Write(string message)
    {
        Trace.WriteLine(message);
        Console.WriteLine(message);
        Console.Error.WriteLine(message);
    }
}

[TestClass]
public class AsynchronousOutputCases
{
    private static readonly TaskCompletionSource<bool> Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static int s_started;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context) => Write("AsynchronousOutputCases ClassInitialize");

    [TestInitialize]
    public void TestInitialize() => Write("TestInitialize");

    [TestCleanup]
    public void TestCleanup() => Write("TestCleanup");

    [ClassCleanup]
    public static void ClassCleanup() => Write("AsynchronousOutputCases ClassCleanup");

    [TestMethod] public Task AsyncMethod1() => RunAsync(nameof(AsyncMethod1));
    [TestMethod] public Task AsyncMethod2() => RunAsync(nameof(AsyncMethod2));
    [TestMethod] public Task AsyncMethod3() => RunAsync(nameof(AsyncMethod3));

    private static async Task RunAsync(string methodName)
    {
        Write($"{methodName} before gate");
        if (Interlocked.Increment(ref s_started) == 3)
        {
            Gate.TrySetResult(true);
        }

        Task completedTask = await Task.WhenAny(Gate.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.AreSame(Gate.Task, completedTask, "All asynchronous tests should reach the gate.");
        await Gate.Task;
        Write($"{methodName} after gate");
    }

    private static void Write(string message)
    {
        Trace.WriteLine(message);
        Console.WriteLine(message);
        Console.Error.WriteLine(message);
    }
}
""";
    }

    public TestContext TestContext { get; set; } = default!;
}
