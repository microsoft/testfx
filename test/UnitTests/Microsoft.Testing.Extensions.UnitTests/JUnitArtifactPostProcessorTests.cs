// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport;
using Microsoft.Testing.Extensions.JUnitReport.Resources;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

[TestClass]
public sealed class JUnitArtifactPostProcessorTests
{
    [TestMethod]
    public void Capabilities_DescribeJUnitArtifacts()
    {
        JUnitArtifactPostProcessor processor = new();

        Assert.AreSequenceEqual(new[] { JUnitReportGenerator.JUnitArtifactKind }, processor.SupportedKinds);
        Assert.AreSequenceEqual(
            new[] { ArtifactPostProcessingMode.TestModules, ArtifactPostProcessingMode.RetryAttempts },
            processor.SupportedModes);
        Assert.IsEmpty(processor.SupportedFileExtensionsFallback);
        Assert.IsFalse(processor.SupportsTruncatedRuns);
    }

    [TestMethod]
    public async Task AddJUnitReportProvider_RegistersArtifactPostProcessor()
    {
        var builder = new TestApplicationBuilder(
            new ApplicationLoggingState(LogLevel.None, new CommandLineParseResult(null, [], [])),
            DateTimeOffset.UtcNow,
            new TestApplicationOptions(),
            new Mock<IUnhandledExceptionsHandler>().Object,
            []);

        builder.AddJUnitReportProvider();
        IReadOnlyList<IArtifactPostProcessor> processors =
            await ((ArtifactPostProcessingManager)builder.ArtifactPostProcessing).BuildAsync(new ServiceProvider());

        Assert.HasCount(1, processors);
        Assert.IsInstanceOfType<JUnitArtifactPostProcessor>(processors[0]);
    }

    [TestMethod]
    public void AddJUnitReportProvider_RequiresArtifactPostProcessingBuilder()
    {
        ITestApplicationBuilder builder = new Mock<ITestApplicationBuilder>().Object;

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => builder.AddJUnitReportProvider());

        Assert.AreEqual(
            ExtensionResources.JUnitReportRequiresArtifactPostProcessing,
            exception.Message);
    }

    [TestMethod]
    public void AddJUnitReportProvider_RequiresTestApplicationBuilder()
    {
        Mock<ITestApplicationBuilder> builder = new();
        builder.As<IArtifactPostProcessingApplicationBuilder>();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => builder.Object.AddJUnitReportProvider());

        Assert.AreEqual(ExtensionResources.InvalidTestApplicationBuilderType, exception.Message);
    }

    [TestMethod]
    public async Task ProcessAsync_WithFewerThanTwoInputs_ReturnsNull()
    {
        JUnitArtifactPostProcessor processor = new();
        InputArtifact input = CreateInput("input.xml");

        Assert.IsNull(await processor.ProcessAsync(
            [input],
            Path.GetTempPath(),
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            CancellationToken.None));
    }

    [TestMethod]
    public async Task ProcessAsync_WithTwoInputs_WritesMergedReport()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = Path.Combine(directory, "first.xml");
            string secondPath = Path.Combine(directory, "second.xml");
            WriteReport(firstPath, "first", tests: 2);
            WriteReport(secondPath, "second", tests: 3);

            ProcessedArtifact? output = await new JUnitArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(firstPath, executionId: "execution-1"),
                    CreateInput(secondPath, executionId: "execution-2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNotNull(output);
            Assert.AreEqual(JUnitReportGenerator.JUnitArtifactKind, output.Kind);
            Assert.AreEqual(ExtensionResources.JUnitMergedArtifactDisplayName, output.DisplayName);
            Assert.AreEqual(
                string.Format(CultureInfo.CurrentCulture, ExtensionResources.JUnitMergedArtifactDescription, 2),
                output.Description);
            Assert.MatchesRegex(new Regex("^merged-[0-9a-f]{32}\\.xml$", RegexOptions.CultureInvariant), Path.GetFileName(output.Path));
            Assert.AreEqual("merged", Path.GetFileName(Path.GetDirectoryName(output.Path)));
            Assert.AreEqual("5", XDocument.Load(output.Path).Root!.Attribute("tests")!.Value);
            Assert.AreSequenceEqual(
                new[] { "first.xml", "second.xml" },
                Directory.GetFiles(directory, "*.xml").Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
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
            string firstPath = Path.Combine(directory, "first.xml");
            string secondPath = Path.Combine(directory, "second.xml");
            WriteReport(firstPath, "first", tests: 2);
            WriteReport(secondPath, "second", tests: 3);
            JUnitArtifactPostProcessor processor = new();
            InputArtifact first = CreateInput(firstPath, executionId: "execution-1");
            InputArtifact second = CreateInput(secondPath, executionId: "execution-2");

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
    public void CreateMergeId_IncludesExecutionProvenance()
    {
        string path = Path.Combine(Path.GetTempPath(), "report.xml");
        InputArtifact first = CreateInput(path, module: "module.dll", targetFramework: "net8.0", architecture: "x64", executionId: "execution-1");
        InputArtifact second = CreateInput(path, module: "module.dll", targetFramework: "net8.0", architecture: "x64", executionId: "execution-2");

        Assert.AreNotEqual(
            JUnitArtifactPostProcessor.CreateMergeId([first]),
            JUnitArtifactPostProcessor.CreateMergeId([second]));
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
            string firstPath = Path.Combine(resultsDirectory, "first.xml");
            string secondPath = Path.Combine(resultsDirectory, "second.xml");
            WriteReport(firstPath, "first", tests: 1);
            WriteReport(secondPath, "second", tests: 1);

            ProcessedArtifact? output = await new JUnitArtifactPostProcessor().ProcessAsync(
                [CreateInput(firstPath), CreateInput(secondPath)],
                resultsDirectory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNull(output);
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

            string firstPath = Path.Combine(resultsDirectory, "first.xml");
            string secondPath = Path.Combine(resultsDirectory, "second.xml");
            WriteReport(firstPath, "first", tests: 1);
            WriteReport(secondPath, "second", tests: 1);

            ProcessedArtifact? output = await new JUnitArtifactPostProcessor().ProcessAsync(
                [CreateInput(firstPath), CreateInput(secondPath)],
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

            string firstPath = Path.Combine(resultsDirectory, "first.xml");
            string secondPath = Path.Combine(resultsDirectory, "second.xml");
            WriteReport(firstPath, "first", tests: 1);
            WriteReport(secondPath, "second", tests: 1);

            ProcessedArtifact? output = await new JUnitArtifactPostProcessor().ProcessAsync(
                [CreateInput(firstPath), CreateInput(secondPath)],
                resultsDirectory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNull(output);
            Assert.IsFalse(Directory.Exists(missingDirectory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
#endif

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"junit-post-processor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static InputArtifact CreateInput(
        string path,
        string? module = null,
        string? targetFramework = null,
        string? architecture = null,
        string? executionId = null)
        => new(path, JUnitReportGenerator.JUnitArtifactKind, module, targetFramework, architecture, executionId);

    private static void WriteReport(string path, string suiteName, int tests)
        => new XDocument(
            new XElement(
                "testsuites",
                new XElement(
                    "testsuite",
                    new XAttribute("name", suiteName),
                    new XAttribute("tests", tests),
                    new XAttribute("failures", 0),
                    new XAttribute("errors", 0),
                    new XAttribute("skipped", 0),
                    new XAttribute("time", "0.000"))))
            .Save(path);
}
