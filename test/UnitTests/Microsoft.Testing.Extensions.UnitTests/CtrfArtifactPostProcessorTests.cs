// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Extensions.CtrfReport.Resources;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.UnitTests;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

[TestClass]
public sealed class CtrfArtifactPostProcessorTests
{
    [TestMethod]
    public void Capabilities_DescribeCtrfArtifactsConservatively()
    {
        CtrfArtifactPostProcessor processor = new();

        Assert.AreSequenceEqual(new[] { CtrfReportGenerator.CtrfArtifactKind }, processor.SupportedKinds);
        Assert.AreSequenceEqual(
            new[] { ArtifactPostProcessingMode.TestModules, ArtifactPostProcessingMode.RetryAttempts },
            processor.SupportedModes);
        Assert.IsEmpty(processor.SupportedFileExtensionsFallback);
        Assert.IsFalse(processor.SupportsTruncatedRuns);
    }

    [TestMethod]
    public async Task AddCtrfReportProvider_RegistersArtifactPostProcessor()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);

        builder.AddCtrfReportProvider();
        IArtifactPostProcessingManager postProcessing =
            ((IArtifactPostProcessingApplicationBuilder)builder).ArtifactPostProcessing;
        var manager = (ArtifactPostProcessingManager)postProcessing;
        IReadOnlyList<IArtifactPostProcessor> processors = await manager.BuildAsync(new ServiceProvider());

        Assert.HasCount(1, processors);
        Assert.IsInstanceOfType<CtrfArtifactPostProcessor>(processors[0]);
    }

    [TestMethod]
    public async Task ProcessAsync_WithFewerThanTwoInputs_ReturnsNull()
    {
        CtrfArtifactPostProcessor processor = new();
        var input = new InputArtifact("input.ctrf.json", CtrfReportGenerator.CtrfArtifactKind, null, null, null, null);

        Assert.IsNull(await processor.ProcessAsync(
            [input],
            Path.GetTempPath(),
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            CancellationToken.None));
    }

    [TestMethod]
    public async Task ProcessAsync_WithTwoInputs_WritesMergedReportAndMetadata()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = WriteReport(directory, "first.ctrf.json", "first");
            string secondPath = WriteReport(directory, "second.ctrf.json", "second");
            CtrfArtifactPostProcessor processor = new();

            ProcessedArtifact? output = await processor.ProcessAsync(
                [
                    CreateInput(firstPath, "execution-1"),
                    CreateInput(secondPath, "execution-2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNotNull(output);
            Assert.AreEqual(CtrfReportGenerator.CtrfArtifactKind, output.Kind);
            Assert.AreEqual(ExtensionResources.CtrfMergedArtifactDisplayName, output.DisplayName);
            Assert.AreEqual(
                string.Format(CultureInfo.CurrentCulture, ExtensionResources.CtrfMergedArtifactDescription, 2),
                output.Description);
            Assert.MatchesRegex(
                new Regex("^merged-[0-9a-f]{32}\\.ctrf\\.json$", RegexOptions.CultureInvariant),
                Path.GetFileName(output.Path));
            Assert.AreEqual("merged", Path.GetFileName(Path.GetDirectoryName(output.Path)));

            JsonNode merged = JsonNode.Parse(File.ReadAllText(output.Path))!;
            Assert.AreEqual("CTRF", (string?)merged["reportFormat"]);
            Assert.AreEqual(2, merged["results"]!["tests"]!.AsArray().Count);
            Assert.IsTrue(Guid.TryParse((string?)merged["reportId"], out _));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ForRetryAttempts_CollapsesToFinalOutcome()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = WriteReport(directory, "first.ctrf.json", "flaky", "failed");
            string secondPath = WriteReport(directory, "second.ctrf.json", "flaky", "passed");

            ProcessedArtifact? output = await new CtrfArtifactPostProcessor().ProcessAsync(
                [CreateInput(firstPath, "1"), CreateInput(secondPath, "2")],
                directory,
                new ArtifactPostProcessingContext(
                    ArtifactPostProcessingTruncationReason.None,
                    ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);
            JsonNode results = JsonNode.Parse(File.ReadAllText(output.Path))!["results"]!;
            Assert.AreEqual(1, results["summary"]!["tests"]!.GetValue<int>());
            Assert.AreEqual(1, results["summary"]!["flaky"]!.GetValue<int>());
            JsonNode test = results["tests"]![0]!;
            Assert.AreEqual("passed", (string?)test["status"]);
            Assert.AreEqual(1, test["retries"]!.GetValue<int>());
            Assert.AreEqual("failed", (string?)test["retryAttempts"]![0]!["status"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WithInvalidCtrfInput_DoesNotPublishPartialMerge()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string validPath = WriteReport(directory, "valid.ctrf.json", "valid");
            string invalidPath = Path.Combine(directory, "invalid.ctrf.json");
            File.WriteAllText(invalidPath, """{"not":"ctrf"}""");
            CtrfArtifactPostProcessor processor = new();

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => processor.ProcessAsync(
                [CreateInput(validPath, "execution-1"), CreateInput(invalidPath, "execution-2")],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None));

            Assert.IsEmpty(Directory.GetFiles(Path.Combine(directory, "merged")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WithReorderedInputs_ProducesIdenticalOutput()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = WriteReport(directory, "first.ctrf.json", "first");
            string secondPath = WriteReport(directory, "second.ctrf.json", "second");
            InputArtifact first = CreateInput(firstPath, "execution-1");
            InputArtifact second = CreateInput(secondPath, "execution-2");
            CtrfArtifactPostProcessor processor = new();

            ProcessedArtifact? output = await processor.ProcessAsync(
                [first, second],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);
            Assert.IsNotNull(output);
            byte[] firstMerge = File.ReadAllBytes(output.Path);

            ProcessedArtifact? retriedOutput = await processor.ProcessAsync(
                [second, first],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNotNull(retriedOutput);
            Assert.AreEqual(output.Path, retriedOutput.Path);
            Assert.AreSequenceEqual(firstMerge, File.ReadAllBytes(retriedOutput.Path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WithDifferentExecutionProvenance_ProducesDifferentArtifactIdentity()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = WriteReport(directory, "first.ctrf.json", "first");
            string secondPath = WriteReport(directory, "second.ctrf.json", "second");
            CtrfArtifactPostProcessor processor = new();
            ArtifactPostProcessingContext context = new(ArtifactPostProcessingTruncationReason.None);

            ProcessedArtifact? firstOutput = await processor.ProcessAsync(
                [CreateInput(firstPath, "execution-1"), CreateInput(secondPath, "execution-2")],
                directory,
                context,
                CancellationToken.None);
            ProcessedArtifact? secondOutput = await processor.ProcessAsync(
                [CreateInput(firstPath, "execution-3"), CreateInput(secondPath, "execution-2")],
                directory,
                context,
                CancellationToken.None);

            Assert.IsNotNull(firstOutput);
            Assert.IsNotNull(secondOutput);
            Assert.AreNotEqual(firstOutput.Path, secondOutput.Path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WhenMergedPathIsAFile_DoesNotMerge()
    {
        string root = CreateTemporaryDirectory();
        string resultsDirectory = Path.Combine(root, "results");
        Directory.CreateDirectory(resultsDirectory);
        File.WriteAllText(Path.Combine(resultsDirectory, "merged"), string.Empty);
        try
        {
            string firstPath = WriteReport(resultsDirectory, "first.ctrf.json", "first");
            string secondPath = WriteReport(resultsDirectory, "second.ctrf.json", "second");

            ProcessedArtifact? output = await new CtrfArtifactPostProcessor().ProcessAsync(
                [CreateInput(firstPath, "execution-1"), CreateInput(secondPath, "execution-2")],
                resultsDirectory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNull(output);
            Assert.IsTrue(File.Exists(firstPath));
            Assert.IsTrue(File.Exists(secondPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

#if NETCOREAPP
    [TestMethod]
    public async Task ProcessAsync_WhenMergedDirectoryIsAReparsePoint_DoesNotMerge()
    {
        string root = CreateTemporaryDirectory();
        string resultsDirectory = Path.Combine(root, "results");
        string outsideDirectory = Path.Combine(root, "outside");
        Directory.CreateDirectory(resultsDirectory);
        Directory.CreateDirectory(outsideDirectory);
        try
        {
            try
            {
                Directory.CreateSymbolicLink(Path.Combine(resultsDirectory, "merged"), outsideDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Inconclusive("Host cannot create directory symbolic links.");
                return;
            }

            string firstPath = WriteReport(resultsDirectory, "first.ctrf.json", "first");
            string secondPath = WriteReport(resultsDirectory, "second.ctrf.json", "second");
            CtrfArtifactPostProcessor processor = new();

            ProcessedArtifact? output = await processor.ProcessAsync(
                [CreateInput(firstPath, "execution-1"), CreateInput(secondPath, "execution-2")],
                resultsDirectory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNull(output);
            Assert.IsEmpty(Directory.GetFileSystemEntries(outsideDirectory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WhenMergedDirectoryIsADanglingReparsePoint_DoesNotMerge()
    {
        string root = CreateTemporaryDirectory();
        string resultsDirectory = Path.Combine(root, "results");
        string missingDirectory = Path.Combine(root, "missing");
        Directory.CreateDirectory(resultsDirectory);
        try
        {
            try
            {
                Directory.CreateSymbolicLink(Path.Combine(resultsDirectory, "merged"), missingDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Inconclusive("Host cannot create directory symbolic links.");
                return;
            }

            string firstPath = WriteReport(resultsDirectory, "first.ctrf.json", "first");
            string secondPath = WriteReport(resultsDirectory, "second.ctrf.json", "second");

            ProcessedArtifact? output = await new CtrfArtifactPostProcessor().ProcessAsync(
                [CreateInput(firstPath, "execution-1"), CreateInput(secondPath, "execution-2")],
                resultsDirectory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNull(output);
            Assert.IsTrue(File.Exists(firstPath));
            Assert.IsTrue(File.Exists(secondPath));
            Assert.IsFalse(Directory.Exists(missingDirectory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
#endif

    private static InputArtifact CreateInput(string path, string executionId)
        => new(path, CtrfReportGenerator.CtrfArtifactKind, null, null, null, executionId);

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ctrf-post-processor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string WriteReport(string directory, string fileName, string testName, string status = "passed")
    {
        string path = Path.Combine(directory, fileName);
        var report = new JsonObject
        {
            ["reportFormat"] = "CTRF",
            ["specVersion"] = "0.0.0",
            ["reportId"] = Guid.NewGuid().ToString("D"),
            ["results"] = new JsonObject
            {
                ["summary"] = new JsonObject
                {
                    ["tests"] = 1,
                    ["passed"] = 1,
                    ["start"] = 1000,
                    ["stop"] = 2000,
                },
                ["tests"] = new JsonArray(
                    new JsonObject
                    {
                        ["name"] = testName,
                        ["status"] = status,
                    }),
            },
        };
        File.WriteAllText(path, report.ToJsonString());
        return path;
    }
}
