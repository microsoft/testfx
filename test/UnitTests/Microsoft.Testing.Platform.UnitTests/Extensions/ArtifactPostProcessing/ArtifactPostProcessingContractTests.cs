// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.Extensions.ArtifactPostProcessing;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

[TestClass]
public sealed class ArtifactPostProcessingContractTests
{
    public TestContext TestContext { get; set; } = null!;

    // ---- Manifest run summary ----
    [TestMethod]
    [DataRow("-1", "0", "1")]
    [DataRow("0", "2147483648", "1")]
    [DataRow("0", "-2147483649", "1")]
    [DataRow("0", "0", "0")]
    [DataRow("0", "0", "-1")]
    [DataRow("0", "0", "2147483648")]
    public void Manifest_RunSummaryOutOfRange_ThrowsFormatExceptionWithoutInnerException(string durationTicks, string exitCode, string testModuleCount)
    {
        FormatException exception = Assert.ThrowsExactly<FormatException>(() => LoadRunSummary(durationTicks, exitCode, testModuleCount));

        Assert.AreEqual(PlatformResources.ArtifactPostProcessingManifestRunSummaryInvalid, exception.Message);
        Assert.IsNull(exception.InnerException);
    }

    [TestMethod]
    [DataRow("0", "0", "1")]
    [DataRow("5", "-2147483648", "1")]
    [DataRow("5", "2147483647", "2147483647")]
    public void Manifest_RunSummaryAtLimits_LoadsExactValues(string durationTicks, string exitCode, string testModuleCount)
    {
        ArtifactPostProcessingRunSummary summary = LoadRunSummary(durationTicks, exitCode, testModuleCount);

        Assert.AreEqual(TimeSpan.FromTicks(long.Parse(durationTicks, CultureInfo.InvariantCulture)), summary.Duration);
        Assert.AreEqual(int.Parse(exitCode, CultureInfo.InvariantCulture), summary.ExitCode);
        Assert.AreEqual(int.Parse(testModuleCount, CultureInfo.InvariantCulture), summary.TestModuleCount);
    }

    [TestMethod]
    public void Manifest_RunSummaryWithInconsistentCounts_WrapsArgumentExceptionInFormatException()
    {
        string path = WriteManifest("""
            "runSummary": { "totalTests": 1, "passedTests": 2, "failedTests": 0, "skippedTests": 0, "durationTicks": 0, "exitCode": 0, "testModuleCount": 1 },
            """);
        try
        {
            FormatException exception = Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(path));

            Assert.AreEqual(PlatformResources.ArtifactPostProcessingManifestRunSummaryInvalid, exception.Message);
            ArgumentOutOfRangeException inner = Assert.IsInstanceOfType<ArgumentOutOfRangeException>(exception.InnerException);
            Assert.AreEqual("passedTests", inner.ParamName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ArtifactPostProcessingRunSummary LoadRunSummary(string durationTicks, string exitCode, string testModuleCount)
    {
        string path = WriteManifest($$"""
            "runSummary": { "totalTests": 0, "passedTests": 0, "failedTests": 0, "skippedTests": 0, "durationTicks": {{durationTicks}}, "exitCode": {{exitCode}}, "testModuleCount": {{testModuleCount}} },
            """);
        try
        {
            return ArtifactPostProcessingManifest.Load(path).Context.RunSummary!;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteManifest(string extraProperties)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, $$"""
            {
              "schemaVersion": 1,
              "outputDirectory": "out",
              {{extraProperties}}
              "inputs": []
            }
            """);
        return path;
    }

    // ---- Run summary constructor ----
    [TestMethod]
    public void RunSummary_FailedPlusPassedExceedingTotal_ReportsFailedTests()
    {
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ArtifactPostProcessingRunSummary(
            totalTests: 3,
            passedTests: 2,
            failedTests: 2,
            skippedTests: 0,
            duration: TimeSpan.Zero,
            exitCode: 0,
            testModuleCount: 1));

        Assert.AreEqual("failedTests", exception.ParamName);
    }

    [TestMethod]
    public void RunSummary_ZeroDuration_IsAccepted()
    {
        var summary = new ArtifactPostProcessingRunSummary(0, 0, 0, 0, TimeSpan.Zero, 0, 1);

        Assert.AreEqual(TimeSpan.Zero, summary.Duration);
    }

    [TestMethod]
    public void RunSummary_NegativeDuration_ReportsDuration()
    {
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ArtifactPostProcessingRunSummary(0, 0, 0, 0, TimeSpan.FromTicks(-1), 0, 1));

        Assert.AreEqual("duration", exception.ParamName);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void RunSummary_NonPositiveTestModuleCount_ReportsTestModuleCount(int testModuleCount)
    {
        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ArtifactPostProcessingRunSummary(0, 0, 0, 0, TimeSpan.Zero, 0, testModuleCount));

        Assert.AreEqual("testModuleCount", exception.ParamName);
    }

    [TestMethod]
    public void ProcessedArtifact_NullKind_ThrowsArgumentNullException()
        => Assert.ThrowsExactly<ArgumentNullException>(() => new ProcessedArtifact("artifact.trx", null!, "artifact", null));

    // ---- Handshake properties ----
    [TestMethod]
    public void HandshakeProperties_TruncatedRunCapabilities_AreSortedJoinedAndExcludeNonMergingProcessors()
    {
        IArtifactPostProcessor[] processors =
        [
            new FakeProcessor("truncated", ["b.kind", "a.kind"], [".B", ".a"], supportsTruncatedRuns: true),
            new FakeProcessor("not-truncated", ["c.kind"], [".c"]),
            new FakeProcessor("retry-only", ["d.kind"], [".d"], supportsTruncatedRuns: true, modes: [ArtifactPostProcessingMode.RetryAttempts]),
        ];

        IReadOnlyDictionary<byte, string> properties = ArtifactPostProcessingHandshakeProperties.Create(processors)!;

        Assert.AreEqual("a.kind;b.kind;c.kind", properties[HandshakeMessagePropertyNames.SupportedPostProcessorKinds]);
        Assert.AreEqual(".a;.b;.c", properties[HandshakeMessagePropertyNames.SupportedPostProcessorExtensionsLegacy]);
        Assert.AreEqual("a.kind;b.kind", properties[HandshakeMessagePropertyNames.SupportedTruncatedRunPostProcessorKinds]);
        Assert.AreEqual(".a;.b", properties[HandshakeMessagePropertyNames.SupportedTruncatedRunPostProcessorExtensionsLegacy]);
    }

    [TestMethod]
    public void HandshakeProperties_RequiredKinds_AreSortedAndJoined()
    {
        IArtifactPostProcessor[] processors =
        [
            new FakeRequiredProcessor("required-1", ["z.req", "m.req"]),
            new FakeRequiredProcessor("required-2", ["a.req"]),
        ];

        IReadOnlyDictionary<byte, string> properties = ArtifactPostProcessingHandshakeProperties.Create(processors)!;

        Assert.AreEqual("a.req;m.req;z.req", properties[HandshakeMessagePropertyNames.RequiredPostProcessorKinds]);
    }

    [TestMethod]
    public void HandshakeProperties_OnlyKinds_OmitsEveryEmptyProperty()
    {
        IReadOnlyDictionary<byte, string> properties = ArtifactPostProcessingHandshakeProperties.Create([new FakeProcessor("kinds", ["k"], [])])!;

        Assert.AreSequenceEqual([HandshakeMessagePropertyNames.SupportedPostProcessorKinds], properties.Keys);
        Assert.AreEqual("k", properties[HandshakeMessagePropertyNames.SupportedPostProcessorKinds]);
    }

    [TestMethod]
    public void HandshakeProperties_OnlyExtensions_OmitsEveryEmptyProperty()
    {
        IReadOnlyDictionary<byte, string> properties = ArtifactPostProcessingHandshakeProperties.Create([new FakeProcessor("exts", [], [".e"])])!;

        Assert.AreSequenceEqual([HandshakeMessagePropertyNames.SupportedPostProcessorExtensionsLegacy], properties.Keys);
        Assert.AreEqual(".e", properties[HandshakeMessagePropertyNames.SupportedPostProcessorExtensionsLegacy]);
    }

    // ---- Manager ----
    [TestMethod]
    public async Task Manager_EnabledProcessorsWithSameUid_ThrowsInvalidOperationException()
    {
        ArtifactPostProcessingManager manager = new();
        manager.AddArtifactPostProcessor(_ => new FakeProcessor("same", ["k1"], []));
        manager.AddArtifactPostProcessor(_ => new FakeProcessor("same", ["k2"], []));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.BuildAsync(new ServiceProvider()));

        Assert.Contains("same", exception.Message);
    }

    [TestMethod]
    public async Task Manager_OneDefinedAndOneUndefinedMode_ThrowsInvalidOperationException()
    {
        ArtifactPostProcessingManager manager = new();
        manager.AddArtifactPostProcessor(_ => new FakeProcessor("processor", ["k"], [], modes: [ArtifactPostProcessingMode.TestModules, (ArtifactPostProcessingMode)42]));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.BuildAsync(new ServiceProvider()));

        Assert.Contains("SupportedModes", exception.Message);
    }

    [TestMethod]
    [DataRow(";kind", ".ext")]
    [DataRow("kind", ";.ext")]
    public async Task Manager_CapabilityStartingWithSeparator_ThrowsInvalidOperationException(string kind, string extension)
    {
        ArtifactPostProcessingManager manager = new();
        manager.AddArtifactPostProcessor(_ => new FakeProcessor("processor", [kind], [extension]));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.BuildAsync(new ServiceProvider()));

        Assert.Contains(";", exception.Message);
    }

    // ---- Dispatcher tool ----
    [TestMethod]
    public async Task DispatcherTool_IdentityAndEnablement_AreContractual()
    {
        var tool = new ArtifactPostProcessingDispatcherTool(
            new TestCommandLineOptions([]),
            [],
            null,
            new Mock<IEnvironment>().Object,
            new Mock<IOutputDevice>().Object);

        Assert.AreEqual("Microsoft.Testing.Platform.ArtifactPostProcessing.Dispatcher", tool.Uid);
        Assert.IsTrue(await tool.IsEnabledAsync());
    }

    [TestMethod]
    public void ValidateProcessedArtifact_OutputEqualToAnyInput_IsRejected()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"processor-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string firstInput = Path.Combine(directory, "first.trx");
            string secondInput = Path.Combine(directory, "second.trx");
            File.WriteAllText(firstInput, "1");
            File.WriteAllText(secondInput, "2");
            InputArtifact[] inputs =
            [
                new InputArtifact(firstInput, "kind", null, null, null, null),
                new InputArtifact(secondInput, "kind", null, null, null, null),
            ];

            Assert.ThrowsExactly<InvalidOperationException>(() => ArtifactPostProcessingDispatcherTool.ValidateProcessedArtifact(
                new ProcessedArtifact(firstInput, "kind", "first", null),
                directory,
                inputs));
            Assert.ThrowsExactly<InvalidOperationException>(() => ArtifactPostProcessingDispatcherTool.ValidateProcessedArtifact(
                new ProcessedArtifact(secondInput, "kind", "second", null),
                directory,
                inputs));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ValidateProcessedArtifact_OnWindows_PathsAreComparedCaseInsensitively()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"processor-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string inputPath = Path.Combine(directory, "input.trx");
            string outputPath = Path.Combine(directory, "output.trx");
            File.WriteAllText(inputPath, "input");
            File.WriteAllText(outputPath, "output");
            var input = new InputArtifact(inputPath, "kind", null, null, null, null);

            // A different casing of the output directory still resolves inside it.
            ProcessedArtifact validated = ArtifactPostProcessingDispatcherTool.ValidateProcessedArtifact(
                new ProcessedArtifact(outputPath.ToUpperInvariant(), "kind", "output", null),
                directory,
                [input]);
            Assert.IsNotNull(validated);

            // A different casing of an input path still refers to that input.
            Assert.ThrowsExactly<InvalidOperationException>(() => ArtifactPostProcessingDispatcherTool.ValidateProcessedArtifact(
                new ProcessedArtifact(inputPath.ToUpperInvariant(), "kind", "input", null),
                directory,
                [input]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void FindProcessorConflicts_SameCapabilityInThreeProcessors_ReportsFirstProcessorOnce()
    {
        IArtifactPostProcessor[] processors =
        [
            new FakeProcessor("first", ["kind"], []),
            new FakeProcessor("second", ["kind"], []),
            new FakeProcessor("third", ["kind"], []),
        ];

        IReadOnlyDictionary<string, string> conflicts = ArtifactPostProcessingDispatcherTool.FindProcessorConflicts(processors);

        Assert.HasCount(1, conflicts);
        Assert.AreEqual("first", conflicts["kind"]);
    }

    [TestMethod]
    public void FindProcessorConflicts_SameUidRepeated_IsNotAConflict()
    {
        IArtifactPostProcessor[] processors =
        [
            new FakeProcessor("same", ["kind"], []),
            new FakeProcessor("same", ["kind"], []),
        ];

        Assert.IsEmpty(ArtifactPostProcessingDispatcherTool.FindProcessorConflicts(processors));
    }

    [TestMethod]
    public async Task DispatcherRun_CancellationRequestedWhileProcessing_DoesNotRunLaterProcessors()
    {
        string root = Path.Combine(Path.GetTempPath(), "dispatcher-cancel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string manifestPath = Path.Combine(root, "manifest.json");
            File.WriteAllText(manifestPath, $$"""
                {
                  "schemaVersion": 1,
                  "outputDirectory": "{{Path.Combine(root, "out").Replace("\\", "\\\\")}}",
                  "inputs": [
                    { "path": "a.bin", "kind": "kind.a" },
                    { "path": "b.bin", "kind": "kind.b" }
                  ]
                }
                """);
            using CancellationTokenSource cts = new();
            var first = new FakeProcessor("first", ["kind.a"], [], onProcess: () =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });
            var second = new FakeProcessor("second", ["kind.b"], []);
            var protocol = new Mock<IPushOnlyProtocol>();
            protocol.SetupGet(value => value.IsServerMode).Returns(true);
            var outputDevice = new Mock<IOutputDevice>();
            outputDevice.Setup(value => value.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var tool = new ArtifactPostProcessingDispatcherTool(
                new TestCommandLineOptions(new() { ["manifest"] = [manifestPath] }),
                [first, second],
                protocol.Object,
                new Mock<IEnvironment>().Object,
                outputDevice.Object);

            await Assert.ThrowsAsync<OperationCanceledException>(() => tool.RunAsync(cts.Token));

            Assert.AreEqual(1, first.ProcessCalls);
            Assert.AreEqual(0, second.ProcessCalls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private class FakeProcessor(
        string uid,
        IReadOnlyList<string> kinds,
        IReadOnlyList<string> extensions,
        bool supportsTruncatedRuns = false,
        IReadOnlyList<ArtifactPostProcessingMode>? modes = null,
        Action? onProcess = null) : IArtifactPostProcessor
    {
        public int ProcessCalls { get; private set; }

        public string Uid { get; } = uid;

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public IReadOnlyList<ArtifactPostProcessingMode> SupportedModes { get; } = modes ?? [ArtifactPostProcessingMode.TestModules];

        public bool SupportsTruncatedRuns { get; } = supportsTruncatedRuns;

        public IReadOnlyList<string> SupportedKinds { get; } = kinds;

        public IReadOnlyList<string> SupportedFileExtensionsFallback { get; } = extensions;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<ProcessedArtifact?> ProcessAsync(
            IReadOnlyList<InputArtifact> inputs,
            string outputDirectory,
            ArtifactPostProcessingContext context,
            CancellationToken cancellationToken)
        {
            ProcessCalls++;
            onProcess?.Invoke();
            return Task.FromResult<ProcessedArtifact?>(null);
        }
    }

    private sealed class FakeRequiredProcessor(string uid, IReadOnlyList<string> kinds)
        : FakeProcessor(uid, kinds, []),
        IArtifactPostProcessorRequiresPostProcessing;
}
