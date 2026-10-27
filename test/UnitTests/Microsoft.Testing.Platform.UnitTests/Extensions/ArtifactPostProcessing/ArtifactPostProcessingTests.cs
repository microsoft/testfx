// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.Extensions.ArtifactPostProcessing;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

[TestClass]
public sealed class ArtifactPostProcessingTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ProcessedArtifact_LegacyAndExplicitNullConstructorsLeaveCoverageUnavailable()
    {
        var legacy = new ProcessedArtifact("output.fixture", "fixture.kind", "display", "description");
        var unavailable = new ProcessedArtifact("output.fixture", "fixture.kind", "display", "description", null);

        foreach (ProcessedArtifact artifact in new[] { legacy, unavailable })
        {
            Assert.AreEqual("output.fixture", artifact.Path);
            Assert.AreEqual("fixture.kind", artifact.Kind);
            Assert.AreEqual("display", artifact.DisplayName);
            Assert.AreEqual("description", artifact.Description);
            Assert.IsNull(artifact.CoverageSummary);
        }
    }

    [TestMethod]
    public void ProcessedArtifact_CoverageSummaryIsReadOnlySnapshot()
    {
        List<CoverageMetricResult> source = CreateCoverageMetrics();
        var artifact = new ProcessedArtifact("output.fixture", "fixture.kind", "display", null, source);

        source[0] = new CoverageMetricResult(CoverageMetric.Line, 1, 2, "replacement");
        source.Clear();

        AssertMetricSnapshot(artifact.CoverageSummary);
        IList<CoverageMetricResult> list = Assert.IsInstanceOfType<IList<CoverageMetricResult>>(artifact.CoverageSummary);
        Assert.IsTrue(list.IsReadOnly);
        Assert.ThrowsExactly<NotSupportedException>(() => list[0] = new CoverageMetricResult(CoverageMetric.Line, 1, 2, "replacement"));
        Assert.ThrowsExactly<NotSupportedException>(() => list.Add(new CoverageMetricResult(CoverageMetric.Line, 1, 2, "replacement")));
        AssertMetricSnapshot(artifact.CoverageSummary);
    }

    [TestMethod]
    public void ValidateProcessedArtifact_NormalizesPathWithoutDroppingCoverage()
    {
        using DispatcherFixture fixture = new();
        fixture.WriteCoverageManifest();
        Directory.CreateDirectory(Path.Combine(fixture.OutputDirectory, "nested"));
        string normalized = Path.Combine(fixture.OutputDirectory, "merged.fixture");
        string originalPath = Path.Combine(fixture.OutputDirectory, "nested", "..", "merged.fixture");
        File.WriteAllText(normalized, "complete output");
        var output = new ProcessedArtifact(originalPath, "fixture.kind", "display", "description", CreateCoverageMetrics());

        ProcessedArtifact actual = ArtifactPostProcessingDispatcherTool.ValidateProcessedArtifact(
            output, fixture.OutputDirectory, [new InputArtifact(fixture.FirstInputPath, "coverage.fixture", null, null, null, null)]);

        Assert.AreEqual(normalized, actual.Path);
        Assert.AreEqual("fixture.kind", actual.Kind);
        Assert.AreEqual("display", actual.DisplayName);
        Assert.AreEqual("description", actual.Description);
        AssertMetricSnapshot(actual.CoverageSummary);
        Assert.AreEqual(originalPath, output.Path);
        AssertMetricSnapshot(output.CoverageSummary);
        Assert.AreEqual("complete output", File.ReadAllText(normalized));
        fixture.AssertInputsUnchanged();
    }

    [TestMethod]
    [DataRow(true, null, DisplayName = "CompleteNegotiatedCoverage")]
    [DataRow(false, null, DisplayName = "UnnegotiatedArtifactHasNoCoverage")]
    [DataRow(true, "timeout", DisplayName = "TimeoutArtifactHasNoCoverage")]
    [DataRow(true, "maximumFailedTests", DisplayName = "PartialArtifactHasNoCoverage")]
    public async Task DispatcherRun_CoverageRequiresNegotiatedNonTruncatedOutput(bool negotiated, string? truncation)
    {
        using DispatcherFixture fixture = new();
        fixture.Protocol.SetupGet(value => value.IsCoverageSummarySupported).Returns(negotiated);
        fixture.WriteCoverageManifest(truncation);
        Mock<IArtifactPostProcessor> processor = CreateCoverageProcessor(fixture, TestContext.CancellationToken);

        int exit = await fixture.Create([processor.Object]).RunAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, exit);
        object message = Assert.ContainsSingle(fixture.Published);
        Assert.AreEqual("execution", ReadModel<string>(message, "ExecutionId"));
        Assert.AreEqual("instance", ReadModel<string>(message, "InstanceId"));
        object artifact = Assert.ContainsSingle(ReadModel<Array>(message, "FileArtifacts")!.Cast<object>());
        Assert.AreEqual(Path.Combine(fixture.OutputDirectory, "merged.fixture"), ReadModel<string>(artifact, "FullPath"));
        Assert.AreEqual("coverage.fixture.processed", ReadModel<string>(artifact, "Kind"));
        Assert.AreSequenceEqual([fixture.FirstInputPath, fixture.SecondInputPath], ReadModel<string[]>(artifact, "InputArtifactPaths"));
        if (negotiated && truncation is null)
        {
            AssertWireCoverage(artifact);
        }
        else
        {
            Assert.IsNull(ReadModel<Array>(artifact, "CoverageSummary"));
        }

        Assert.AreEqual("complete output", File.ReadAllText(Path.Combine(fixture.OutputDirectory, "merged.fixture")));
        fixture.AssertInputsUnchanged();
        Assert.IsEmpty(fixture.Output);
    }

    [TestMethod]
    [DataRow("none", DisplayName = "ProcessorReturnsNoOutput")]
    [DataRow("legacy", DisplayName = "LegacyOutputHasUnavailableCoverage")]
    [DataRow("null", DisplayName = "ExplicitUnavailableCoverage")]
    [DataRow("empty", DisplayName = "EmptyListDoesNotInventZeroCounts")]
    public async Task DispatcherRun_UnavailableCoverageOrNoOutputDoesNotInventSummary(string mode)
    {
        using DispatcherFixture fixture = new();
        fixture.Protocol.SetupGet(value => value.IsCoverageSummarySupported).Returns(true);
        fixture.WriteCoverageManifest();
        Mock<IArtifactPostProcessor> processor = CreateProcessor("unavailable", ["coverage.fixture"], []);
        string outputPath = Path.Combine(fixture.OutputDirectory, "merged.fixture");
        processor.Setup(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), fixture.OutputDirectory, It.IsAny<ArtifactPostProcessingContext>(), TestContext.CancellationToken))
            .Returns(() =>
            {
                if (mode == "none")
                {
                    return Task.FromResult<ProcessedArtifact?>(null);
                }

                File.WriteAllText(outputPath, "complete output");
                return Task.FromResult<ProcessedArtifact?>(mode == "legacy"
                    ? new ProcessedArtifact(outputPath, "fixture.kind", "display", null)
                    : new ProcessedArtifact(outputPath, "fixture.kind", "display", null, mode == "empty" ? [] : null));
            });

        int exit = await fixture.Create([processor.Object]).RunAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, exit);
        if (mode == "none")
        {
            Assert.IsEmpty(fixture.Published);
            Assert.IsFalse(File.Exists(outputPath));
        }
        else
        {
            object artifact = Assert.ContainsSingle(ReadModel<Array>(Assert.ContainsSingle(fixture.Published), "FileArtifacts")!.Cast<object>());
            Assert.AreEqual(outputPath, ReadModel<string>(artifact, "FullPath"));
            Assert.AreSequenceEqual([fixture.FirstInputPath, fixture.SecondInputPath], ReadModel<string[]>(artifact, "InputArtifactPaths"));
            if (mode == "empty")
            {
                Assert.IsEmpty(ReadModel<Array>(artifact, "CoverageSummary")!);
            }
            else
            {
                Assert.IsNull(ReadModel<Array>(artifact, "CoverageSummary"));
            }
        }

        fixture.AssertInputsUnchanged();
        Assert.IsEmpty(fixture.Output);
    }

    [TestMethod]
    [DataRow("missing", DisplayName = "MissingOutputCannotPublishCoverage")]
    [DataRow("outside", DisplayName = "OutsideOutputCannotPublishCoverage")]
    [DataRow("input", DisplayName = "ReusedInputCannotPublishCoverage")]
    public async Task DispatcherRun_RejectedOutputDoesNotPublishCoverage(string invalidOutput)
    {
        using DispatcherFixture fixture = new();
        fixture.Protocol.SetupGet(value => value.IsCoverageSummarySupported).Returns(true);
        fixture.WriteCoverageManifest();
        Directory.CreateDirectory(fixture.OutputDirectory);
        string outputPath = invalidOutput switch
        {
            "missing" => Path.Combine(fixture.OutputDirectory, "missing.fixture"),
            "outside" => Path.Combine(fixture.Root, "outside.fixture"),
            _ => Path.Combine(fixture.OutputDirectory, "original-input.fixture"),
        };
        if (invalidOutput != "missing")
        {
            File.WriteAllText(outputPath, "original content");
        }

        if (invalidOutput == "input")
        {
            fixture.WriteManifest($$"""[{ "path": "{{outputPath.Replace("\\", "\\\\")}}", "kind": "coverage.fixture" }]""");
        }

        Mock<IArtifactPostProcessor> processor = CreateProcessor("rejected", ["coverage.fixture"], []);
        processor.Setup(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), fixture.OutputDirectory, It.IsAny<ArtifactPostProcessingContext>(), TestContext.CancellationToken))
            .ReturnsAsync(new ProcessedArtifact(outputPath, "fixture.kind", "rejected", null, CreateCoverageMetrics()));

        int exit = await fixture.Create([processor.Object]).RunAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, exit);
        Assert.IsEmpty(fixture.Published);
        Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(Assert.ContainsSingle(fixture.Output));
        if (invalidOutput != "missing")
        {
            Assert.AreEqual("original content", File.ReadAllText(outputPath));
        }

        fixture.AssertInputsUnchanged();
    }

    [TestMethod]
    [DataRow(false, "exception", DisplayName = "ExceptionAfterValidOutput")]
    [DataRow(false, "missing", DisplayName = "RejectedOutputAfterValidOutput")]
    [DataRow(false, "uncanceled", DisplayName = "UncancelledExceptionAfterValidOutput")]
    [DataRow(true, "exception", DisplayName = "ExceptionBeforeValidOutputs")]
    [DataRow(true, "missing", DisplayName = "RejectedOutputBeforeValidOutputs")]
    [DataRow(true, "uncanceled", DisplayName = "UncancelledExceptionBeforeValidOutputs")]
    public async Task DispatcherRun_AnyProcessorFailureStripsEveryOutputCoverage(bool failureFirst, string failure)
    {
        using DispatcherFixture fixture = new();
        fixture.Protocol.SetupGet(value => value.IsCoverageSummarySupported).Returns(true);
        fixture.WriteCoverageManifest();
        fixture.WriteManifest($$"""
            [
              { "path": "{{fixture.FirstInputPath.Replace("\\", "\\\\")}}", "kind": "first.kind" },
              { "path": "{{fixture.SecondInputPath.Replace("\\", "\\\\")}}", "kind": "second.kind" },
              { "path": "bad.fixture", "kind": "bad.kind" }
            ]
            """);
        Mock<IArtifactPostProcessor> first = CreateCoverageProcessor(fixture, TestContext.CancellationToken, "first.kind", "first.fixture");
        Mock<IArtifactPostProcessor> second = CreateCoverageProcessor(fixture, TestContext.CancellationToken, "second.kind", "second.fixture");
        Mock<IArtifactPostProcessor> bad = CreateProcessor("bad.processor", ["bad.kind"], []);
        bad.Setup(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), fixture.OutputDirectory, It.IsAny<ArtifactPostProcessingContext>(), TestContext.CancellationToken))
            .Returns(() => failure switch
            {
                "exception" => Task.FromException<ProcessedArtifact?>(new IOException("processing sentinel")),
                "uncanceled" => Task.FromException<ProcessedArtifact?>(new OperationCanceledException("processing sentinel")),
                _ => Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(
                    Path.Combine(fixture.OutputDirectory, "missing.fixture"), "bad.kind", "bad", null, CreateCoverageMetrics())),
            });

        int exit = await fixture.Create(failureFirst ? [bad.Object, first.Object, second.Object] : [first.Object, bad.Object, second.Object])
            .RunAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, exit);
        object[] artifacts = [.. ReadModel<Array>(Assert.ContainsSingle(fixture.Published), "FileArtifacts")!.Cast<object>()];
        Assert.HasCount(2, artifacts);
        Assert.AreSequenceEqual(
            [Path.Combine(fixture.OutputDirectory, "first.fixture"), Path.Combine(fixture.OutputDirectory, "second.fixture")],
            artifacts.Select(artifact => ReadModel<string>(artifact, "FullPath")));
        Assert.AreSequenceEqual([fixture.FirstInputPath], ReadModel<string[]>(artifacts[0], "InputArtifactPaths"));
        Assert.AreSequenceEqual([fixture.SecondInputPath], ReadModel<string[]>(artifacts[1], "InputArtifactPaths"));
        foreach (object artifact in artifacts)
        {
            Assert.IsNull(ReadModel<Array>(artifact, "CoverageSummary"));
            Assert.AreEqual("complete output", File.ReadAllText(ReadModel<string>(artifact, "FullPath")!));
        }

        Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(Assert.ContainsSingle(fixture.Output));
        fixture.AssertInputsUnchanged();
    }

    [TestMethod]
    public async Task DispatcherRun_CancellationAfterOutputCreationPreventsPublication()
    {
        using DispatcherFixture fixture = new();
        fixture.Protocol.SetupGet(value => value.IsCoverageSummarySupported).Returns(true);
        fixture.WriteCoverageManifest();
        using CancellationTokenSource cancellation = new();
        string path = Path.Combine(fixture.OutputDirectory, "merged.fixture");
        Mock<IArtifactPostProcessor> processor = CreateProcessor("cancel-after-output", ["coverage.fixture"], []);
        processor.Setup(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), fixture.OutputDirectory, It.IsAny<ArtifactPostProcessingContext>(), cancellation.Token))
            .Returns(() =>
            {
                File.WriteAllText(path, "complete output");
                cancellation.Cancel();
                return Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(path, "fixture.kind", "display", null, CreateCoverageMetrics()));
            });

        OperationCanceledException error = await Assert.ThrowsAsync<OperationCanceledException>(
            () => fixture.Create([processor.Object]).RunAsync(cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.AreEqual("complete output", File.ReadAllText(path));
        Assert.IsEmpty(fixture.Published);
        Assert.IsEmpty(fixture.Output);
        fixture.AssertInputsUnchanged();
    }

    [TestMethod]
    public async Task DispatcherRun_CompleteCoverageDoesNotOverrideFailingRunSummary()
    {
        using DispatcherFixture fixture = new();
        fixture.Protocol.SetupGet(value => value.IsCoverageSummarySupported).Returns(true);
        fixture.WriteCoverageManifest(runSummary: """
            { "totalTests": 1, "passedTests": 0, "failedTests": 1, "skippedTests": 0,
              "durationTicks": 1, "exitCode": 2, "testModuleCount": 2 }
            """);
        Mock<IArtifactPostProcessor> processor = CreateCoverageProcessor(fixture, TestContext.CancellationToken);

        int exit = await fixture.Create([processor.Object]).RunAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, exit, "Successful artifact processing must not reinterpret the test-run verdict.");
        var context = (ArtifactPostProcessingContext)Assert.ContainsSingle(
            processor.Invocations.Where(invocation => invocation.Method.Name == nameof(IArtifactPostProcessor.ProcessAsync))).Arguments[2];
        Assert.AreEqual(2, context.RunSummary?.ExitCode);
        AssertWireCoverage(Assert.ContainsSingle(ReadModel<Array>(Assert.ContainsSingle(fixture.Published), "FileArtifacts")!.Cast<object>()));
        fixture.AssertInputsUnchanged();
    }

    private static List<CoverageMetricResult> CreateCoverageMetrics() =>
    [
        new(CoverageMetric.Line, 3_000_000_000L, long.MaxValue, "merged.producer"),
        new(CoverageMetric.Branch, 2, 7, "merged.producer"),
        new(CoverageMetric.Custom, 0, 0, "custom.producer", "paths"),
        new(CoverageMetric.Custom, 1, 3, "custom.producer", "mcdc"),
    ];

    private static void AssertMetricSnapshot(IReadOnlyList<CoverageMetricResult>? metrics)
    {
        Assert.IsNotNull(metrics);
        Assert.AreSequenceEqual([CoverageMetric.Line, CoverageMetric.Branch, CoverageMetric.Custom, CoverageMetric.Custom], metrics.Select(metric => metric.Metric));
        Assert.AreSequenceEqual(["merged.producer", "merged.producer", "custom.producer", "custom.producer"], metrics.Select(metric => metric.ProducerId));
        Assert.AreSequenceEqual([null, null, "paths", "mcdc"], metrics.Select(metric => metric.CustomMetricName));
        Assert.AreSequenceEqual<long>([3_000_000_000L, 2, 0, 1], metrics.Select(metric => metric.CoveredCount));
        Assert.AreSequenceEqual<long>([long.MaxValue, 7, 0, 3], metrics.Select(metric => metric.CoverableCount));
    }

    private static void AssertWireCoverage(object artifact)
    {
        Assert.IsNull(ReadModel<string>(artifact, "SessionUid"));
        object[] entries = [.. ReadModel<Array>(artifact, "CoverageSummary")!.Cast<object>()];
        Assert.AreSequenceEqual(["merged.producer", "merged.producer", "custom.producer", "custom.producer"], entries.Select(entry => ReadModel<string>(entry, "ProducerId")));
        Assert.AreSequenceEqual<int?>([0, 2, 255, 255], entries.Select(entry => ReadModel<int?>(entry, "Metric")));
        Assert.AreSequenceEqual([null, null, "paths", "mcdc"], entries.Select(entry => ReadModel<string>(entry, "CustomMetricName")));
        Assert.AreSequenceEqual<long?>([3_000_000_000L, 2, 0, 1], entries.Select(entry => ReadModel<long?>(entry, "CoveredCount")));
        Assert.AreSequenceEqual<long?>([long.MaxValue, 7, 0, 3], entries.Select(entry => ReadModel<long?>(entry, "CoverableCount")));
        Assert.IsTrue(entries.All(entry => ReadModel<int?>(entry, "ScopeLevel") == 0));
        Assert.IsTrue(entries.All(entry => ReadModel<string>(entry, "ScopeName") is null && ReadModel<string>(entry, "SessionUid") is null));
    }

    private static Mock<IArtifactPostProcessor> CreateCoverageProcessor(
        DispatcherFixture fixture, CancellationToken token, string kind = "coverage.fixture", string fileName = "merged.fixture")
    {
        Mock<IArtifactPostProcessor> processor = CreateProcessor(kind + ".processor", [kind], [], supportsTruncated: true);
        processor.Setup(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), fixture.OutputDirectory, It.IsAny<ArtifactPostProcessingContext>(), token))
            .Returns(() =>
            {
                string path = Path.Combine(fixture.OutputDirectory, fileName);
                File.WriteAllText(path, "complete output");
                return Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(path, kind + ".processed", "display", "description", CreateCoverageMetrics()));
            });
        return processor;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DispatcherRun_RequiresServerProtocolBeforeReadingManifest(bool protocolPresent)
    {
        using DispatcherFixture fixture = new();
        fixture.Protocol.SetupGet(value => value.IsServerMode).Returns(false);
        int exit = await fixture.Create([], protocolPresent ? fixture.Protocol.Object : null).RunAsync(TestContext.CancellationToken);
        Assert.AreEqual(5, exit);
        ErrorMessageOutputDeviceData error = Assert.IsInstanceOfType<ErrorMessageOutputDeviceData>(Assert.ContainsSingle(fixture.Output));
        Assert.Contains("pipe", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(TestContext.CancellationToken, Assert.ContainsSingle(fixture.OutputTokens));
        Assert.IsEmpty(fixture.Published);
        Assert.IsFalse(Directory.Exists(fixture.OutputDirectory));
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("malformed")]
    [DataRow("output-file")]
    public async Task DispatcherRun_InvalidManifestOrOutputDirectoryProducesInvalidCommandLineWithoutProcessorCalls(string failure)
    {
        using DispatcherFixture fixture = new();
        if (failure == "malformed")
        {
            File.WriteAllText(fixture.ManifestPath, "{broken");
        }
        else if (failure == "output-file")
        {
            File.WriteAllText(fixture.OutputDirectory, "a file, not a directory");
            fixture.WriteManifest("[]");
        }

        Mock<IArtifactPostProcessor> processor = CreateProcessor("unused", ["kind"], []);

        int exit = await fixture.Create([processor.Object]).RunAsync(TestContext.CancellationToken);

        Assert.AreEqual(5, exit);
        ErrorMessageOutputDeviceData error = Assert.IsInstanceOfType<ErrorMessageOutputDeviceData>(Assert.ContainsSingle(fixture.Output));
        Assert.Contains("manifest", error.Message, StringComparison.OrdinalIgnoreCase);
        processor.Verify(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), It.IsAny<string>(), It.IsAny<ArtifactPostProcessingContext>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsEmpty(fixture.Published);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DispatcherRun_FirstProcessorOwnsKindAndLegacyMatchesAndPublishesOnlyValidatedOutput(bool produceOutput)
    {
        using DispatcherFixture fixture = new();
        fixture.WriteManifest("""
            [
              { "path": "tagged.other", "kind": "kind.a", "producingTestModule": "module.dll" },
              { "path": "legacy.TRX", "kind": null },
              { "path": "wrong-kind.trx", "kind": "different" },
              { "path": "case-kind.trx", "kind": "KIND.A" }
            ]
            """);
        Mock<IArtifactPostProcessor> first = CreateProcessor("first", ["kind.a"], [".trx"]);
        Mock<IArtifactPostProcessor> second = CreateProcessor("second", ["kind.a"], [".TRX"]);
        IReadOnlyList<InputArtifact>? observed = null;
        ArtifactPostProcessingContext? context = null;
        string outputPath = Path.Combine(fixture.OutputDirectory, "merged.trx");
        first.Setup(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), fixture.OutputDirectory, It.IsAny<ArtifactPostProcessingContext>(), TestContext.CancellationToken))
            .Callback<IReadOnlyList<InputArtifact>, string, ArtifactPostProcessingContext, CancellationToken>((inputs, _, value, _) =>
            {
                observed = inputs;
                context = value;
            })
            .Returns(() =>
            {
                if (produceOutput)
                {
                    File.WriteAllText(outputPath, "merged contents");
                }

                return Task.FromResult(produceOutput ? new ProcessedArtifact(outputPath, "merged.kind", "merged display", "merged description") : null);
            });

        int exit = await fixture.Create([first.Object, second.Object]).RunAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, exit);
        Assert.AreSequenceEqual(["tagged.other", "legacy.TRX"], observed!.Select(value => value.Path));
        Assert.AreEqual("module.dll", observed![0].ProducingTestModule);
        Assert.AreEqual(ArtifactPostProcessingMode.TestModules, context!.Mode);
        Assert.IsFalse(context.IsTruncated);
        second.Verify(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), It.IsAny<string>(), It.IsAny<ArtifactPostProcessingContext>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.HasCount(2, fixture.Output);
        Assert.Contains("kind.a", ((WarningMessageOutputDeviceData)fixture.Output[0]).Message);
        Assert.Contains("first", ((WarningMessageOutputDeviceData)fixture.Output[0]).Message);
        Assert.Contains(".trx", ((WarningMessageOutputDeviceData)fixture.Output[1]).Message);
        if (produceOutput)
        {
            object message = Assert.ContainsSingle(fixture.Published);
            Assert.AreEqual("Microsoft.Testing.Platform.IPC.Models.FileArtifactMessages", message.GetType().FullName);
            Assert.AreEqual("execution", ReadModel<string>(message, "ExecutionId"));
            Assert.AreEqual("instance", ReadModel<string>(message, "InstanceId"));
            object artifact = Assert.ContainsSingle(ReadModel<Array>(message, "FileArtifacts")!.Cast<object>());
            Assert.AreEqual(outputPath, ReadModel<string>(artifact, "FullPath"));
            Assert.AreEqual("merged.kind", ReadModel<string>(artifact, "Kind"));
            Assert.AreEqual("merged display", ReadModel<string>(artifact, "DisplayName"));
            Assert.AreEqual("merged description", ReadModel<string>(artifact, "Description"));
            Assert.AreSequenceEqual(["tagged.other", "legacy.TRX"], ReadModel<string[]>(artifact, "InputArtifactPaths"));
            Assert.IsNull(ReadModel<string>(artifact, "TestUid"));
            Assert.IsNull(ReadModel<string>(artifact, "SessionUid"));
            Assert.AreEqual("merged contents", File.ReadAllText(outputPath));
        }
        else
        {
            Assert.IsEmpty(fixture.Published);
            Assert.IsFalse(File.Exists(outputPath));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DispatcherRun_EligibilityHonorsModeAndTruncationWithoutConsumingSkippedInputs(bool truncated)
    {
        using DispatcherFixture fixture = new();
        fixture.WriteManifest("""[{ "path": "input.any", "kind": "kind" }]""", truncated ? "timeout" : null);
        Mock<IArtifactPostProcessor> wrongMode = CreateProcessor("retry-only", ["kind"], [], modes: [ArtifactPostProcessingMode.RetryAttempts]);
        Mock<IArtifactPostProcessor> ordinary = CreateProcessor("complete-only", ["kind"], []);
        Mock<IArtifactPostProcessor> recovery = CreateProcessor("recovery", ["kind"], [], supportsTruncated: true);
        IReadOnlyList<InputArtifact>? observed = null;
        ArtifactPostProcessingContext? observedContext = null;
        Mock<IArtifactPostProcessor> selected = truncated ? recovery : ordinary;
        selected.Setup(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), fixture.OutputDirectory, It.IsAny<ArtifactPostProcessingContext>(), TestContext.CancellationToken))
            .Callback<IReadOnlyList<InputArtifact>, string, ArtifactPostProcessingContext, CancellationToken>((inputs, _, context, _) =>
            {
                observed = inputs;
                observedContext = context;
            }).ReturnsAsync((ProcessedArtifact?)null);

        int exit = await fixture.Create([wrongMode.Object, ordinary.Object, recovery.Object]).RunAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, exit);
        Assert.AreEqual("input.any", Assert.ContainsSingle(observed!).Path);
        Assert.AreEqual(truncated, observedContext!.IsTruncated);
        Assert.AreEqual(truncated ? ArtifactPostProcessingTruncationReason.Timeout : ArtifactPostProcessingTruncationReason.None, observedContext.TruncationReason);
        wrongMode.Verify(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), It.IsAny<string>(), It.IsAny<ArtifactPostProcessingContext>(), It.IsAny<CancellationToken>()), Times.Never);
        (truncated ? ordinary : recovery).Verify(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), It.IsAny<string>(), It.IsAny<ArtifactPostProcessingContext>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.IsEmpty(fixture.Published);
    }

    [TestMethod]
    [DataRow("exception")]
    [DataRow("uncanceled")]
    [DataRow("missing")]
    public async Task DispatcherRun_ProcessorFailureKeepsLaterValidOutputButReturnsFailure(string failure)
    {
        using DispatcherFixture fixture = new();
        fixture.WriteManifest("""[{ "path": "bad.any", "kind": "bad" }, { "path": "good.any", "kind": "good" }]""");
        Mock<IArtifactPostProcessor> first = CreateProcessor("failed.processor", ["bad"], []);
        Mock<IArtifactPostProcessor> later = CreateProcessor("later.processor", ["good"], []);
        first.Setup(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), fixture.OutputDirectory, It.IsAny<ArtifactPostProcessingContext>(), TestContext.CancellationToken))
            .Returns(() => failure switch
            {
                "exception" => Task.FromException<ProcessedArtifact?>(new IOException("processing sentinel")),
                "uncanceled" => Task.FromException<ProcessedArtifact?>(new OperationCanceledException("processing sentinel")),
                _ => Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(Path.Combine(fixture.OutputDirectory, "not-created.trx"), "bad", "bad", null)),
            });
        string path = Path.Combine(fixture.OutputDirectory, "good.trx");
        later.Setup(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), fixture.OutputDirectory, It.IsAny<ArtifactPostProcessingContext>(), TestContext.CancellationToken))
            .Returns(() =>
            {
                File.WriteAllText(path, "good output");
                return Task.FromResult<ProcessedArtifact?>(new ProcessedArtifact(path, "good.output", "good display", null));
            });

        int exit = await fixture.Create([first.Object, later.Object]).RunAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, exit);
        WarningMessageOutputDeviceData warning = Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(Assert.ContainsSingle(fixture.Output));
        Assert.Contains("failed.processor", warning.Message);
        Assert.Contains(failure == "missing" ? "output" : "processing sentinel", warning.Message, StringComparison.OrdinalIgnoreCase);
        object published = Assert.ContainsSingle(ReadModel<Array>(Assert.ContainsSingle(fixture.Published), "FileArtifacts")!.Cast<object>());
        Assert.AreEqual(path, ReadModel<string>(published, "FullPath"));
        Assert.AreSequenceEqual(["good.any"], ReadModel<string[]>(published, "InputArtifactPaths"));
        Assert.AreEqual("good output", File.ReadAllText(path));
    }

    [TestMethod]
    public async Task DispatcherRun_CallerCancellationPropagatesWithoutWarningOrPublishing()
    {
        using DispatcherFixture fixture = new();
        fixture.WriteManifest("""[{ "path": "input.any", "kind": "kind" }]""");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Mock<IArtifactPostProcessor> processor = CreateProcessor("canceled", ["kind"], []);
        processor.Setup(value => value.ProcessAsync(It.IsAny<IReadOnlyList<InputArtifact>>(), fixture.OutputDirectory, It.IsAny<ArtifactPostProcessingContext>(), cancellation.Token))
            .Returns(Task.FromCanceled<ProcessedArtifact?>(cancellation.Token));

        OperationCanceledException error = await Assert.ThrowsAsync<OperationCanceledException>(
            () => fixture.Create([processor.Object]).RunAsync(cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.IsEmpty(fixture.Output);
        Assert.IsEmpty(fixture.Published);
    }

    private static Mock<IArtifactPostProcessor> CreateProcessor(
        string uid, IReadOnlyList<string> kinds, IReadOnlyList<string> extensions,
        bool supportsTruncated = false, IReadOnlyList<ArtifactPostProcessingMode>? modes = null)
    {
        var processor = new Mock<IArtifactPostProcessor>();
        processor.SetupGet(value => value.Uid).Returns(uid);
        processor.SetupGet(value => value.SupportedKinds).Returns(kinds);
        processor.SetupGet(value => value.SupportedFileExtensionsFallback).Returns(extensions);
        processor.SetupGet(value => value.SupportsTruncatedRuns).Returns(supportsTruncated);
        processor.SetupGet(value => value.SupportedModes).Returns(modes ?? [ArtifactPostProcessingMode.TestModules]);
        return processor;
    }

    private static T? ReadModel<T>(object value, string name)
        => (T?)value.GetType().GetProperty(name)!.GetValue(value);

    private sealed class DispatcherFixture : IDisposable
    {
        public DispatcherFixture()
        {
            Directory.CreateDirectory(Root);
            Protocol.SetupGet(value => value.IsServerMode).Returns(true);
            Protocol.SetupGet(value => value.InstanceId).Returns("instance");
            Environment.Setup(value => value.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID)).Returns("execution");
            Device.Setup(value => value.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
                .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, value, token) =>
                {
                    Output.Add(value);
                    OutputTokens.Add(token);
                }).Returns(Task.CompletedTask);
        }

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "dispatcher-coverage-" + Guid.NewGuid().ToString("N"));

        public string ManifestPath => Path.Combine(Root, "manifest.json");

        public string OutputDirectory => Path.Combine(Root, "out");

        public string FirstInputPath => Path.Combine(Root, "first.fixture");

        public string SecondInputPath => Path.Combine(Root, "second.fixture");

        public Mock<IPushOnlyProtocol> Protocol { get; } = new();

        public Mock<IEnvironment> Environment { get; } = new();

        public Mock<IOutputDevice> Device { get; } = new();

        // The production protocol's embedded IRequest/models have the same names as the linked
        // test copies but distinct assembly identities. Capture the actual invocation objects.
        public object[] Published => [.. Protocol.Invocations
            .Where(value => value.Method.Name == nameof(IPushOnlyProtocol.SendMessageAsync))
            .Select(value => value.Arguments[0])];

        public List<IOutputDeviceData> Output { get; } = [];

        public List<CancellationToken> OutputTokens { get; } = [];

        public ArtifactPostProcessingDispatcherTool Create(IReadOnlyList<IArtifactPostProcessor> processors)
            => Create(processors, Protocol.Object);

        public ArtifactPostProcessingDispatcherTool Create(IReadOnlyList<IArtifactPostProcessor> processors, IPushOnlyProtocol? protocol)
            => new(new TestCommandLineOptions(new() { ["manifest"] = [ManifestPath] }), processors, protocol, Environment.Object, Device.Object);

        public void WriteCoverageManifest(string? truncation = null, string? runSummary = null)
        {
            File.WriteAllText(FirstInputPath, "first input");
            File.WriteAllText(SecondInputPath, "second input");
            WriteManifest(
                $$"""
                [
                  { "path": "{{FirstInputPath.Replace("\\", "\\\\")}}", "kind": "coverage.fixture" },
                  { "path": "{{SecondInputPath.Replace("\\", "\\\\")}}", "kind": "coverage.fixture" }
                ]
                """,
                truncation,
                runSummary);
        }

        public void AssertInputsUnchanged()
        {
            Assert.AreEqual("first input", File.ReadAllText(FirstInputPath));
            Assert.AreEqual("second input", File.ReadAllText(SecondInputPath));
        }

        public void WriteManifest(string inputs, string? truncation = null, string? runSummary = null)
            => File.WriteAllText(ManifestPath, $$"""
                {
                  "schemaVersion": 1,
                  "outputDirectory": "{{OutputDirectory.Replace("\\", "\\\\")}}",
                  "inputs": {{inputs}}
                  {{(truncation is null ? string.Empty : ", \"truncationReason\": \"" + truncation + "\"")}}
                  {{(runSummary is null ? string.Empty : ", \"runSummary\": " + runSummary)}}
                }
                """);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    [TestMethod]
    public void HandshakeProperties_AreSortedAndDeduplicated()
    {
        IArtifactPostProcessor[] processors =
        [
            new StubProcessor("first", ["z.kind", "a.kind"], [".TRX"]),
            new StubProcessor("second", ["a.kind"], [".trx", ".xml"], supportsTruncatedRuns: true),
            new RequiredStubProcessor("required", ["summary.kind"]),
        ];

        IReadOnlyDictionary<byte, string> properties = ArtifactPostProcessingHandshakeProperties.Create(processors)!;

        Assert.AreEqual("a.kind;summary.kind;z.kind", properties[HandshakeMessagePropertyNames.SupportedPostProcessorKinds]);
        Assert.AreEqual(".trx;.xml", properties[HandshakeMessagePropertyNames.SupportedPostProcessorExtensionsLegacy]);
        Assert.AreEqual("a.kind", properties[HandshakeMessagePropertyNames.SupportedTruncatedRunPostProcessorKinds]);
        Assert.AreEqual(
            ".trx;.xml",
            properties[HandshakeMessagePropertyNames.SupportedTruncatedRunPostProcessorExtensionsLegacy]);
        Assert.AreEqual("summary.kind", properties[HandshakeMessagePropertyNames.RequiredPostProcessorKinds]);
    }

    [TestMethod]
    public void HandshakeProperties_WithNoCapabilities_ReturnsNull()
        => Assert.IsNull(ArtifactPostProcessingHandshakeProperties.Create([new StubProcessor("empty", [], [])]));

    [TestMethod]
    public void HandshakeProperties_ExcludesRetryOnlyRequiredKinds()
    {
        IArtifactPostProcessor[] processors =
        [
            new StubProcessor("module", ["module.kind"], []),
            new RequiredStubProcessor(
                "retry",
                ["retry.kind"],
                [ArtifactPostProcessingMode.RetryAttempts]),
        ];

        IReadOnlyDictionary<byte, string> properties = ArtifactPostProcessingHandshakeProperties.Create(processors)!;

        Assert.AreEqual("module.kind", properties[HandshakeMessagePropertyNames.SupportedPostProcessorKinds]);
        Assert.IsFalse(properties.ContainsKey(HandshakeMessagePropertyNames.RequiredPostProcessorKinds));
    }

    [DataRow(HandshakeMessageHostTypes.TestHost, true)]
    [DataRow(HandshakeMessageHostTypes.ServerTestHost, true)]
    [DataRow(HandshakeMessageHostTypes.TestHostController, true)]
    [DataRow(HandshakeMessageHostTypes.TestHostOrchestrator, false)]
    [TestMethod]
    public void SupportsArtifactPostProcessing_ReturnsExpectedValue(string hostType, bool expected)
        => Assert.AreEqual(expected, CommonHost.SupportsArtifactPostProcessing(hostType));

    [DataRow("")]
    [DataRow(" ")]
    [TestMethod]
    public void ProcessedArtifact_BlankKind_ThrowsArgumentException(string kind)
        => Assert.ThrowsExactly<ArgumentException>(() => new ProcessedArtifact("artifact.trx", kind, "artifact", null));

    [TestMethod]
    public void RunSummary_PassedExceedsTotal_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ArtifactPostProcessingRunSummary(
            totalTests: 0,
            passedTests: 1,
            failedTests: 0,
            skippedTests: 0,
            duration: TimeSpan.Zero,
            exitCode: 0,
            testModuleCount: 1));

        Assert.AreEqual("passedTests", exception.ParamName);
    }

    [DataRow(-1, 0, 0, 0, "totalTests")]
    [DataRow(1, -1, 1, 1, "passedTests")]
    [DataRow(1, 0, -1, 1, "failedTests")]
    [DataRow(1, 0, 0, -1, "skippedTests")]
    [DataRow(1, 0, 0, 0, "skippedTests")]
    [TestMethod]
    public void RunSummary_InvalidCount_ReportsCorrespondingParameter(
        long totalTests,
        long passedTests,
        long failedTests,
        long skippedTests,
        string expectedParameter)
    {
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ArtifactPostProcessingRunSummary(
            totalTests,
            passedTests,
            failedTests,
            skippedTests,
            TimeSpan.Zero,
            exitCode: 0,
            testModuleCount: 1));

        Assert.AreEqual(expectedParameter, exception.ParamName);
    }

    [TestMethod]
    public async Task Manager_BuildsOnlyEnabledProcessors()
    {
        ArtifactPostProcessingManager manager = new();
        manager.AddArtifactPostProcessor(_ => new StubProcessor("enabled", ["kind"], [".ext"]));
        manager.AddArtifactPostProcessor(_ => new StubProcessor("disabled", ["other"], [".other"], isEnabled: false));

        IReadOnlyList<IArtifactPostProcessor> processors = await manager.BuildAsync(new ServiceProvider());

        Assert.HasCount(1, processors);
        Assert.AreEqual("enabled", processors[0].Uid);
    }

    [DataRow("kind;other", ".ext")]
    [DataRow("kind", ".ext;.other")]
    [TestMethod]
    public async Task Manager_CapabilityContainingSeparator_ThrowsInvalidOperationException(string kind, string extension)
    {
        ArtifactPostProcessingManager manager = new();
        manager.AddArtifactPostProcessor(_ => new StubProcessor("processor", [kind], [extension]));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.BuildAsync(new ServiceProvider()));
    }

    [DataRow("", ".ext")]
    [DataRow(" ", ".ext")]
    [DataRow("kind", "")]
    [DataRow("kind", " ")]
    [DataRow("kind", "trx")]
    [DataRow("kind", ".TRX")]
    [TestMethod]
    public async Task Manager_InvalidCapability_ThrowsInvalidOperationException(string kind, string extension)
    {
        ArtifactPostProcessingManager manager = new();
        manager.AddArtifactPostProcessor(_ => new StubProcessor("processor", [kind], [extension]));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.BuildAsync(new ServiceProvider()));
    }

    [TestMethod]
    public void FindProcessorConflicts_DuplicateCapabilitiesWithinOneProcessor_ReturnsEmpty()
    {
        var processor = new StubProcessor("processor", ["kind", "kind"], [".TRX", ".trx"]);

        IReadOnlyDictionary<string, string> conflicts =
            ArtifactPostProcessingDispatcherTool.FindProcessorConflicts([processor]);

        Assert.IsEmpty(conflicts);
    }

    [TestMethod]
    public void FindProcessorConflicts_SameCapabilityAcrossProcessors_ReturnsFirstProcessor()
    {
        IArtifactPostProcessor[] processors =
        [
            new StubProcessor("first", ["kind"], []),
            new StubProcessor("second", ["kind"], []),
        ];

        IReadOnlyDictionary<string, string> conflicts =
            ArtifactPostProcessingDispatcherTool.FindProcessorConflicts(processors);

        Assert.HasCount(1, conflicts);
        Assert.AreEqual("first", conflicts["kind"]);
    }

    [TestMethod]
    public void FindProcessorConflicts_KindMatchingAnotherProcessorExtension_ReturnsEmpty()
    {
        IArtifactPostProcessor[] processors =
        [
            new StubProcessor("kind-processor", [".trx"], []),
            new StubProcessor("extension-processor", [], [".trx"]),
        ];

        IReadOnlyDictionary<string, string> conflicts =
            ArtifactPostProcessingDispatcherTool.FindProcessorConflicts(processors);

        Assert.IsEmpty(conflicts);
    }

    [TestMethod]
    public void ValidateProcessedArtifact_RejectsInvalidPaths()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"processor-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string inputPath = Path.Combine(directory, "input.trx");
            string validOutputPath = Path.Combine(directory, "output.trx");
            string outsidePath = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.trx");
            File.WriteAllText(inputPath, "input");
            File.WriteAllText(validOutputPath, "output");
            File.WriteAllText(outsidePath, "outside");
            var input = new InputArtifact(inputPath, "kind", null, null, null, null);

            ProcessedArtifact validated = ArtifactPostProcessingDispatcherTool.ValidateProcessedArtifact(
                new ProcessedArtifact(validOutputPath, "kind", "output", null),
                directory,
                [input]);

            Assert.AreEqual(Path.GetFullPath(validOutputPath), validated.Path);
            Assert.ThrowsExactly<InvalidOperationException>(() => ArtifactPostProcessingDispatcherTool.ValidateProcessedArtifact(
                new ProcessedArtifact(Path.Combine(directory, "missing.trx"), "kind", "missing", null),
                directory,
                [input]));
            Assert.ThrowsExactly<InvalidOperationException>(() => ArtifactPostProcessingDispatcherTool.ValidateProcessedArtifact(
                new ProcessedArtifact(outsidePath, "kind", "outside", null),
                directory,
                [input]));
            Assert.ThrowsExactly<InvalidOperationException>(() => ArtifactPostProcessingDispatcherTool.ValidateProcessedArtifact(
                new ProcessedArtifact(inputPath, "kind", "input", null),
                directory,
                [input]));

            File.Delete(outsidePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Manifest_LoadsVersionedAttributedInputs()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"artifact-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string manifestPath = Path.Combine(directory, "manifest.json");
        try
        {
            File.WriteAllText(
                manifestPath,
                $$"""
                {
                  "schemaVersion": 1,
                  "outputDirectory": "{{directory.Replace("\\", "\\\\")}}",
                  "inputs": [
                    {
                      "path": "a.trx",
                      "kind": "microsoft.testing.trx",
                      "producingTestModule": "A.dll",
                      "targetFramework": "net10.0",
                      "architecture": "x64",
                      "executionId": "null"
                    },
                    {
                      "path": "legacy.trx",
                      "kind": null
                    }
                  ]
                }
                """);

            var manifest = ArtifactPostProcessingManifest.Load(manifestPath);

            Assert.AreEqual(directory, manifest.OutputDirectory);
            Assert.HasCount(2, manifest.Inputs);
            Assert.AreEqual("microsoft.testing.trx", manifest.Inputs[0].Kind);
            Assert.AreEqual("A.dll", manifest.Inputs[0].ProducingTestModule);
            Assert.AreEqual("null", manifest.Inputs[0].ExecutionId);
            Assert.IsNull(manifest.Inputs[1].Kind);
            Assert.IsFalse(manifest.Context.IsTruncated);
            Assert.AreEqual(ArtifactPostProcessingTruncationReason.None, manifest.Context.TruncationReason);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [DataRow("maximumFailedTests", ArtifactPostProcessingTruncationReason.MaximumFailedTests)]
    [DataRow("timeout", ArtifactPostProcessingTruncationReason.Timeout)]
    [TestMethod]
    public void Manifest_WithTruncationReason_LoadsTruncatedContext(
        string value,
        ArtifactPostProcessingTruncationReason expected)
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                manifestPath,
                $$"""{ "schemaVersion": 1, "outputDirectory": "out", "truncationReason": "{{value}}", "inputs": [] }""");

            var manifest = ArtifactPostProcessingManifest.Load(manifestPath);

            Assert.IsTrue(manifest.Context.IsTruncated);
            Assert.AreEqual(expected, manifest.Context.TruncationReason);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [TestMethod]
    public void Manifest_WithRunSummary_LoadsAuthoritativeContext()
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                manifestPath,
                """
                {
                  "schemaVersion": 1,
                  "outputDirectory": "out",
                  "runSummary": {
                    "totalTests": 10,
                    "passedTests": 7,
                    "failedTests": 2,
                    "skippedTests": 1,
                    "durationTicks": 1234567,
                    "exitCode": 2,
                    "testModuleCount": 3
                  },
                  "inputs": []
                }
                """);

            var manifest = ArtifactPostProcessingManifest.Load(manifestPath);

            ArtifactPostProcessingRunSummary? runSummary = manifest.Context.RunSummary;
            Assert.IsNotNull(runSummary);
            Assert.AreEqual(10, runSummary.TotalTests);
            Assert.AreEqual(7, runSummary.PassedTests);
            Assert.AreEqual(2, runSummary.FailedTests);
            Assert.AreEqual(1, runSummary.SkippedTests);
            Assert.AreEqual(TimeSpan.FromTicks(1234567), runSummary.Duration);
            Assert.AreEqual(2, runSummary.ExitCode);
            Assert.AreEqual(3, runSummary.TestModuleCount);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [TestMethod]
    public void Manifest_WithInvalidRunSummary_ThrowsSpecificFormatException()
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                manifestPath,
                """
                {
                  "schemaVersion": 1,
                  "outputDirectory": "out",
                  "runSummary": {
                    "totalTests": 1
                  },
                  "inputs": []
                }
                """);

            FormatException exception = Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));

            Assert.AreEqual(Platform.Resources.PlatformResources.ArtifactPostProcessingManifestRunSummaryInvalid, exception.Message);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [TestMethod]
    public void Manifest_WithUnsupportedVersion_ThrowsFormatException()
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(manifestPath, """{ "schemaVersion": 2, "outputDirectory": "out", "inputs": [] }""");

            FormatException exception = Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));

            Assert.AreEqual(Platform.Resources.PlatformResources.ArtifactPostProcessingManifestInvalid, exception.Message);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [DataRow("""{ "schemaVersion": "1", "outputDirectory": "out", "inputs": [] }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": 123, "inputs": [] }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "inputs": [{ "path": 123 }] }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "inputs": [{ "path": "a.trx", "kind": 123 }] }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "inputs": [{ "path": "a.trx", "kind": {} }] }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "inputs": [{ "path": "a.trx", "executionId": [] }] }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "truncationReason": null, "inputs": [] }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "truncationReason": 1, "inputs": [] }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "truncationReason": "unknown", "inputs": [] }""")]
    [TestMethod]
    public void Manifest_WithInvalidValueType_ThrowsFormatException(string json)
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(manifestPath, json);

            Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [TestMethod]
    public void Manifest_WithMalformedJson_ThrowsFormatException()
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(manifestPath, """{ "schemaVersion": 1, """);

            Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [TestMethod]
    public void Manifest_WithInputMissingPath_ThrowsFormatException()
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                manifestPath,
                """{ "schemaVersion": 1, "outputDirectory": "out", "inputs": [{ "kind": "microsoft.testing.trx" }] }""");

            Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "inputs": [{}] }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "inputs": [null] }""")]
    [TestMethod]
    public void Manifest_WithEmptyInputEntry_ThrowsFormatException(string json)
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(manifestPath, json);

            Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [TestMethod]
    public void Manifest_WithTopLevelArray_ThrowsFormatException()
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(manifestPath, "[]");

            Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out" }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "inputs": "bad" }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "inputs": "[]" }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "inputs": {} }""")]
    [TestMethod]
    public void Manifest_WithInputsThatIsNotArray_ThrowsFormatException(string json)
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(manifestPath, json);

            Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    private class StubProcessor(
        string uid,
        IReadOnlyList<string> supportedKinds,
        IReadOnlyList<string> supportedExtensions,
        bool supportsTruncatedRuns = false,
        bool isEnabled = true,
        IReadOnlyList<ArtifactPostProcessingMode>? supportedModes = null) : IArtifactPostProcessor
    {
        public string Uid { get; } = uid;

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public IReadOnlyList<ArtifactPostProcessingMode> SupportedModes { get; } =
            supportedModes ?? [ArtifactPostProcessingMode.TestModules];

        public bool SupportsTruncatedRuns { get; } = supportsTruncatedRuns;

        public IReadOnlyList<string> SupportedKinds { get; } = supportedKinds;

        public IReadOnlyList<string> SupportedFileExtensionsFallback { get; } = supportedExtensions;

        public Task<bool> IsEnabledAsync() => Task.FromResult(isEnabled);

        public Task<ProcessedArtifact?> ProcessAsync(
            IReadOnlyList<InputArtifact> inputs,
            string outputDirectory,
            ArtifactPostProcessingContext context,
            CancellationToken cancellationToken)
            => Task.FromResult<ProcessedArtifact?>(null);
    }

    private sealed class RequiredStubProcessor(
        string uid,
        IReadOnlyList<string> supportedKinds,
        IReadOnlyList<ArtifactPostProcessingMode>? supportedModes = null)
        : StubProcessor(uid, supportedKinds, [], supportedModes: supportedModes),
        IArtifactPostProcessorRequiresPostProcessing;
}
