// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

using Moq;

using GitHubSummaryPostProcessor = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.GitHubActionsSummaryArtifactPostProcessor;

namespace Microsoft.Testing.Extensions.UnitTests;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

[TestClass]
public sealed class CiRunSummaryAggregationTests
{
    [TestMethod]
    public async Task ReadAndAggregate_UsesAuthoritativeSummaryAndSortsModulesAsync()
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule moduleB = CreateModule("B", passed: 1, failed: 0);
            CiRunSummaryModule moduleA = CreateModule("A", passed: 0, failed: 1);
            string pathB = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                moduleB);
            string pathA = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                moduleA);
            InputArtifact[] inputs =
            [
                CreateInput(pathB, moduleB),
                CreateInput(pathA, moduleA),
            ];
            var runSummary = new ArtifactPostProcessingRunSummary(
                totalTests: 9,
                passedTests: 6,
                failedTests: 2,
                skippedTests: 1,
                duration: TimeSpan.FromSeconds(12),
                exitCode: 2,
                testModuleCount: 2);

            CiRunSummaryAggregate aggregate = CiRunSummaryAggregation.ReadAndAggregate(
                inputs,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, runSummary));

            Assert.IsTrue(aggregate.HasAuthoritativeRunSummary);
            Assert.IsFalse(aggregate.IsPartial);
            Assert.AreEqual(9, aggregate.TotalTests);
            Assert.AreEqual(TimeSpan.FromSeconds(12), aggregate.Duration);
            Assert.AreEqual(2, aggregate.ExitCode);
            Assert.AreEqual("A", aggregate.Modules[0].AssemblyName);
            Assert.AreEqual("B", aggregate.Modules[1].AssemblyName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAndAggregate_WithoutRunSummary_UsesObservedCountsAsync()
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule module = CreateModule("A", passed: 2, failed: 1);
            string path = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                module);

            CiRunSummaryAggregate aggregate = CiRunSummaryAggregation.ReadAndAggregate(
                [CreateInput(path, module)],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None));

            Assert.IsFalse(aggregate.HasAuthoritativeRunSummary);
            Assert.AreEqual(3, aggregate.TotalTests);
            Assert.AreEqual(2, aggregate.PassedTests);
            Assert.AreEqual(1, aggregate.FailedTests);
            Assert.IsNull(aggregate.Duration);
            Assert.IsNull(aggregate.ExitCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReadAndAggregate_MalformedFragment_ThrowsFormatException()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{ "schemaVersion": 1, "provider": "azure-devops" }""");
            var input = new InputArtifact(path, AzureDevOpsSummaryArtifactPostProcessor.FragmentArtifactKind, null, null, null, null);

            Assert.ThrowsExactly<FormatException>(() => CiRunSummaryAggregation.ReadAndAggregate(
                [input],
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ProvidersUseDistinctFragmentKinds()
    {
        string[] kinds =
        [
            AzureDevOpsSummaryArtifactPostProcessor.FragmentArtifactKind,
            GitHubSummaryPostProcessor.FragmentArtifactKind,
        ];

        Assert.AreNotEqual(kinds[0], kinds[1]);
    }

    [TestMethod]
    public async Task AzureDevOpsPostProcessor_WritesAggregateAndUploadsOnceAsync()
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule module = CreateModule("A", passed: 2, failed: 1);
            module.RequestedOutputPath = Path.Combine(directory, "requested-summary.md");
            string fragmentPath = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                module);
            InputArtifact[] inputs = [CreateInput(fragmentPath, module)];
            var runSummary = new ArtifactPostProcessingRunSummary(
                3,
                2,
                1,
                0,
                TimeSpan.FromSeconds(2),
                exitCode: 2,
                testModuleCount: 1);
            var environment = new Mock<IEnvironment>();
            environment.Setup(item => item.GetEnvironmentVariable("TF_BUILD")).Returns("true");
            var output = new List<IOutputDeviceData>();
            var outputDevice = new Mock<IOutputDevice>();
            outputDevice
                .Setup(item => item.DisplayAsync(
                    It.IsAny<IOutputDeviceDataProducer>(),
                    It.IsAny<IOutputDeviceData>(),
                    It.IsAny<CancellationToken>()))
                .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) => output.Add(data))
                .Returns(Task.CompletedTask);
            var processor = new AzureDevOpsSummaryArtifactPostProcessor(
                new TestCommandLineOptions(new()
                {
                    [AzureDevOpsCommandLineOptions.AzureDevOpsSummary] = [],
                }),
                environment.Object,
                outputDevice.Object);

            ProcessedArtifact? first = await processor.ProcessAsync(
                inputs,
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, runSummary),
                CancellationToken.None);
            ProcessedArtifact? second = await processor.ProcessAsync(
                inputs,
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, runSummary),
                CancellationToken.None);

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreEqual(AzureDevOpsSummaryArtifactPostProcessor.SummaryArtifactKind, first.Kind);
            Assert.IsTrue(File.Exists(first.Path));
            Assert.IsTrue(File.Exists(module.RequestedOutputPath));
            Assert.Contains("# Overall test summary", File.ReadAllText(first.Path));
            string[] commands =
            [
                .. output
                    .OfType<AzureDevOpsCommandOutputDeviceData>()
                    .Select(item => item.Text),
            ];
            Assert.HasCount(1, commands);
            Assert.StartsWith("##vso[task.uploadsummary]", commands[0]);
            Assert.Contains(module.RequestedOutputPath, commands[0]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task AzureDevOpsPostProcessor_IsEnabledForDispatcherRelaunchAsync()
    {
        var processor = new AzureDevOpsSummaryArtifactPostProcessor(
            new TestCommandLineOptions(new()
            {
                ["manifest"] = ["manifest.json"],
            }),
            Mock.Of<IEnvironment>(),
            Mock.Of<IOutputDevice>());

        Assert.IsTrue(await processor.IsEnabledAsync());
    }

    [TestMethod]
    public async Task WriteFragmentAsync_BoundsLongFileNameAsync()
    {
        string directory = CreateDirectory();
        try
        {
            CiRunSummaryModule module = CreateModule(new string('a', 300), passed: 1, failed: 0);

            string path = await CiRunSummaryAggregation.WriteFragmentAsync(
                directory,
                AzureDevOpsSummaryArtifactPostProcessor.Provider,
                AzureDevOpsSummaryArtifactPostProcessor.ProviderSlug,
                module);

            Assert.IsTrue(File.Exists(path));
            Assert.IsLessThanOrEqualTo(64, Path.GetFileName(path).Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static CiRunSummaryModule CreateModule(string assemblyName, int passed, int failed)
    {
        var records = new List<TestRecord>();
        for (int i = 0; i < passed; i++)
        {
            records.Add(new TestRecord($"pass-{i}", $"{assemblyName}.Pass{i}", TerminalKind.Passed, TimeSpan.FromMilliseconds(i + 1)));
        }

        for (int i = 0; i < failed; i++)
        {
            records.Add(new TestRecord($"fail-{i}", $"{assemblyName}.Fail{i}", TerminalKind.Failed, TimeSpan.FromMilliseconds(i + 1)));
        }

        return CiRunSummaryAggregation.CreateModule(
            records,
            assemblyName,
            Path.Combine(Path.GetTempPath(), assemblyName + ".dll"),
            "net9.0",
            "x64",
            "execution-" + assemblyName,
            "session-" + assemblyName,
            attemptNumber: 1,
            exitCode: failed > 0 ? 2 : 0);
    }

    private static InputArtifact CreateInput(string path, CiRunSummaryModule module)
        => new(
            path,
            AzureDevOpsSummaryArtifactPostProcessor.FragmentArtifactKind,
            module.ModulePath,
            module.TargetFramework,
            module.Architecture,
            module.ExecutionId);

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ci-summary-aggregation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
