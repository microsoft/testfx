// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

using Microsoft.Testing.Extensions.HtmlReport;
using Microsoft.Testing.Extensions.HtmlReport.Resources;
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
public sealed class HtmlArtifactPostProcessorTests
{
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Capabilities_DescribeHtmlArtifacts()
    {
        HtmlArtifactPostProcessor processor = new();

        Assert.AreSequenceEqual(new[] { HtmlReportGenerator.HtmlArtifactKind }, processor.SupportedKinds);
        Assert.AreSequenceEqual(
            new[] { ArtifactPostProcessingMode.TestModules, ArtifactPostProcessingMode.RetryAttempts },
            processor.SupportedModes);
        Assert.IsEmpty(processor.SupportedFileExtensionsFallback);
        Assert.IsFalse(processor.SupportsTruncatedRuns);
    }

    [TestMethod]
    public async Task AddHtmlReportProvider_RegistersArtifactPostProcessor()
    {
        var builder = new TestApplicationBuilder(
            new ApplicationLoggingState(LogLevel.None, new CommandLineParseResult(null, [], [])),
            DateTimeOffset.UtcNow,
            new TestApplicationOptions(),
            new Mock<IUnhandledExceptionsHandler>().Object,
            []);

        builder.AddHtmlReportProvider();
        IReadOnlyList<IArtifactPostProcessor> processors =
            await ((ArtifactPostProcessingManager)builder.ArtifactPostProcessing).BuildAsync(new ServiceProvider());

        Assert.HasCount(1, processors);
        Assert.IsInstanceOfType<HtmlArtifactPostProcessor>(processors[0]);
    }

    [TestMethod]
    public void AddHtmlReportProvider_RequiresArtifactPostProcessingBuilder()
    {
        ITestApplicationBuilder builder = new Mock<ITestApplicationBuilder>().Object;

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => builder.AddHtmlReportProvider());

        Assert.AreEqual(ExtensionResources.InvalidTestApplicationBuilderType, exception.Message);
    }

    [TestMethod]
    public async Task ProcessAsync_WithFewerThanTwoInputs_ReturnsNull()
    {
        HtmlArtifactPostProcessor processor = new();
        InputArtifact input = CreateInput("input.html");

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
            string firstPath = Path.Combine(directory, "first.html");
            string secondPath = Path.Combine(directory, "second.html");
            File.WriteAllText(
                firstPath,
                CreateReport(
                    "first.dll",
                    "MSTest",
                    new DateTimeOffset(2026, 8, 9, 10, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 8, 9, 10, 1, 0, TimeSpan.Zero),
                    Test("duplicate", "Failed <script>", "failed", 10)));
            File.WriteAllText(
                secondPath,
                CreateReport(
                    "second.dll",
                    "Other",
                    new DateTimeOffset(2026, 8, 9, 9, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 8, 9, 11, 0, 0, TimeSpan.Zero),
                    Test("duplicate", "Passed </script><img src=x>", "passed", 20)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(firstPath, module: "tests.dll", targetFramework: "net8.0", architecture: "x64", executionId: "execution-1"),
                    CreateInput(secondPath, module: "tests.dll", targetFramework: "net8.0", architecture: "x64", executionId: "execution-2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNotNull(output);
            Assert.AreEqual(HtmlReportGenerator.HtmlArtifactKind, output.Kind);
            Assert.AreEqual(ExtensionResources.HtmlMergedArtifactDisplayName, output.DisplayName);
            Assert.AreEqual(
                string.Format(CultureInfo.CurrentCulture, ExtensionResources.HtmlMergedArtifactDescription, 2),
                output.Description);
            Assert.MatchesRegex(new Regex("^merged-[0-9a-f]{32}\\.html$", RegexOptions.CultureInvariant), Path.GetFileName(output.Path));
            Assert.AreEqual("merged", Path.GetFileName(Path.GetDirectoryName(output.Path)));

            string mergedHtml = File.ReadAllText(output.Path);
            Assert.DoesNotContain("</script><img", mergedHtml);
            JsonObject merged = ParseReport(mergedHtml);
            Assert.AreEqual(ExtensionResources.HtmlMergedReportName, (string?)merged["testApplication"]);
            Assert.AreEqual(string.Empty, (string?)merged["framework"]);
            Assert.AreEqual("machine", (string?)merged["machineName"]);
            Assert.AreEqual("2026-08-09T09:00:00.0000000+00:00", (string?)merged["startTime"]);
            Assert.AreEqual("2026-08-09T11:00:00.0000000+00:00", (string?)merged["endTime"]);

            var tests = (JsonArray)merged["tests"]!;
            Assert.HasCount(2, tests);
            Assert.AreEqual(0, (int?)tests[0]!["rowKey"]);
            Assert.AreEqual(1, (int?)tests[1]!["rowKey"]);
            Assert.AreEqual(1, (int?)tests[0]!["attemptIndex"]);
            Assert.AreEqual(2, (int?)tests[1]!["attemptIndex"]);
            Assert.AreEqual(2, (int?)tests[0]!["attemptOf"]);
            Assert.AreEqual(2, (int?)tests[1]!["attemptOf"]);
            Assert.AreEqual("tests.dll", (string?)tests[0]!["testApplication"]);
            Assert.AreEqual("net8.0", (string?)tests[0]!["targetFramework"]);
            Assert.AreEqual("x64", (string?)tests[0]!["architecture"]);
            Assert.AreEqual("execution-2", (string?)tests[0]!["executionId"]);
            Assert.AreEqual("Passed </script><img src=x>", (string?)tests[0]!["displayName"]);
            Assert.AreEqual("execution-1", (string?)tests[1]!["executionId"]);
            Assert.AreEqual("Failed <script>", (string?)tests[1]!["displayName"]);

            var summary = (JsonObject)merged["summary"]!;
            Assert.AreEqual(2, (int?)summary["total"]);
            Assert.AreEqual(1, (int?)summary["passed"]);
            Assert.AreEqual(1, (int?)summary["failed"]);
            Assert.AreEqual(30d, (double?)summary["totalDurationMs"]);
            Assert.AreSequenceEqual(
                new[] { "first.html", "second.html" },
                Directory.GetFiles(directory, "*.html").Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_OrdersAttemptsByEmbeddedStartTime()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string attempt10Directory = Path.Combine(directory, "10");
            string attempt2Directory = Path.Combine(directory, "2");
            Directory.CreateDirectory(attempt10Directory);
            Directory.CreateDirectory(attempt2Directory);
            string attempt10Path = Path.Combine(attempt10Directory, "report.html");
            string attempt2Path = Path.Combine(attempt2Directory, "report.html");
            File.WriteAllText(
                attempt10Path,
                CreateReport("tests.dll", "MSTest", Epoch.AddMinutes(10), Epoch.AddMinutes(11), Test("same", "attempt 10", "passed", 1)));
            File.WriteAllText(
                attempt2Path,
                CreateReport("tests.dll", "MSTest", Epoch.AddMinutes(2), Epoch.AddMinutes(3), Test("same", "attempt 2", "failed", 1)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [CreateInput(attempt10Path), CreateInput(attempt2Path)],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNotNull(output);
            var tests = (JsonArray)ParseReport(File.ReadAllText(output.Path))["tests"]!;
            Assert.AreEqual("attempt 2", (string?)tests[0]!["displayName"]);
            Assert.AreEqual(1, (int?)tests[0]!["attemptIndex"]);
            Assert.AreEqual("attempt 10", (string?)tests[1]!["displayName"]);
            Assert.AreEqual(2, (int?)tests[1]!["attemptIndex"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WhenInputIsAlreadyMerged_PreservesEmbeddedRowProvenance()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = Path.Combine(directory, "first.html");
            string secondPath = Path.Combine(directory, "second.html");
            File.WriteAllText(firstPath, CreateReport("first.dll", "MSTest", Epoch, Epoch, Test("first", "first", "passed", 1)));
            File.WriteAllText(secondPath, CreateReport("second.dll", "MSTest", Epoch, Epoch, Test("second", "second", "passed", 1)));

            HtmlArtifactPostProcessor processor = new();
            ProcessedArtifact? firstMerge = await processor.ProcessAsync(
                [
                    CreateInput(firstPath, module: "first.dll", targetFramework: "net8.0", architecture: "x64", executionId: "execution-1"),
                    CreateInput(secondPath, module: "second.dll", targetFramework: "net9.0", architecture: "arm64", executionId: "execution-2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);
            Assert.IsNotNull(firstMerge);

            string thirdPath = Path.Combine(directory, "third.html");
            File.WriteAllText(thirdPath, CreateReport("third.dll", "MSTest", Epoch, Epoch, Test("third", "third", "passed", 1)));
            ProcessedArtifact? secondMerge = await processor.ProcessAsync(
                [
                    CreateInput(firstMerge.Path, module: "outer.dll", targetFramework: "net10.0", architecture: "x86", executionId: "outer-execution"),
                    CreateInput(thirdPath, module: "third.dll", targetFramework: "net10.0", architecture: "x64", executionId: "execution-3"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNotNull(secondMerge);
            var tests = (JsonArray)ParseReport(File.ReadAllText(secondMerge.Path))["tests"]!;
            Assert.AreEqual("first.dll", (string?)tests[0]!["testApplication"]);
            Assert.AreEqual("net8.0", (string?)tests[0]!["targetFramework"]);
            Assert.AreEqual("x64", (string?)tests[0]!["architecture"]);
            Assert.AreEqual("execution-1", (string?)tests[0]!["executionId"]);
            Assert.AreEqual("second.dll", (string?)tests[1]!["testApplication"]);
            Assert.AreEqual("net9.0", (string?)tests[1]!["targetFramework"]);
            Assert.AreEqual("arm64", (string?)tests[1]!["architecture"]);
            Assert.AreEqual("execution-2", (string?)tests[1]!["executionId"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WhenInputIsAlreadyMerged_OrdersAttemptsByOriginalSourceStartTime()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = Path.Combine(directory, "first.html");
            string thirdPath = Path.Combine(directory, "third.html");
            File.WriteAllText(firstPath, CreateReport("tests.dll", "MSTest", Epoch.AddMinutes(1), Epoch.AddMinutes(2), Test("same", "attempt 1", "failed", 1)));
            File.WriteAllText(thirdPath, CreateReport("tests.dll", "MSTest", Epoch.AddMinutes(3), Epoch.AddMinutes(4), Test("same", "attempt 3", "passed", 1)));

            HtmlArtifactPostProcessor processor = new();
            ProcessedArtifact? firstMerge = await processor.ProcessAsync(
                [
                    CreateInput(firstPath, module: "tests.dll", targetFramework: "net8.0", architecture: "x64", executionId: "execution-1"),
                    CreateInput(thirdPath, module: "tests.dll", targetFramework: "net8.0", architecture: "x64", executionId: "execution-3"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);
            Assert.IsNotNull(firstMerge);

            string secondPath = Path.Combine(directory, "second.html");
            File.WriteAllText(secondPath, CreateReport("tests.dll", "MSTest", Epoch.AddMinutes(2), Epoch.AddMinutes(3), Test("same", "attempt 2", "failed", 1)));
            ProcessedArtifact? secondMerge = await processor.ProcessAsync(
                [
                    CreateInput(firstMerge.Path),
                    CreateInput(secondPath, module: "tests.dll", targetFramework: "net8.0", architecture: "x64", executionId: "execution-2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNotNull(secondMerge);
            var tests = (JsonArray)ParseReport(File.ReadAllText(secondMerge.Path))["tests"]!;
            Assert.AreEqual("attempt 1", (string?)tests[0]!["displayName"]);
            Assert.AreEqual(1, (int?)tests[0]!["attemptIndex"]);
            Assert.AreEqual("attempt 2", (string?)tests[1]!["displayName"]);
            Assert.AreEqual(2, (int?)tests[1]!["attemptIndex"]);
            Assert.AreEqual("attempt 3", (string?)tests[2]!["displayName"]);
            Assert.AreEqual(3, (int?)tests[2]!["attemptIndex"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WhenModuleMetadataIsMissing_UsesEmbeddedApplicationForProvenanceAndAttemptIdentity()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = Path.Combine(directory, "first.html");
            string secondPath = Path.Combine(directory, "second.html");
            string thirdPath = Path.Combine(directory, "third.html");
            File.WriteAllText(firstPath, CreateReport("same.dll", "MSTest", Epoch, Epoch, Test("same", "first", "failed", 1)));
            File.WriteAllText(secondPath, CreateReport("same.dll", "MSTest", Epoch, Epoch, Test("same", "second", "passed", 2)));
            File.WriteAllText(thirdPath, CreateReport("other.dll", "MSTest", Epoch, Epoch, Test("same", "third", "passed", 3)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [CreateInput(firstPath), CreateInput(secondPath), CreateInput(thirdPath)],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNotNull(output);
            var tests = (JsonArray)ParseReport(File.ReadAllText(output.Path))["tests"]!;
            Assert.AreEqual("same.dll", (string?)tests[0]!["testApplication"]);
            Assert.AreEqual("same.dll", (string?)tests[1]!["testApplication"]);
            Assert.AreEqual("other.dll", (string?)tests[2]!["testApplication"]);
            Assert.AreEqual(2, (int?)tests[0]!["attemptOf"]);
            Assert.AreEqual(2, (int?)tests[1]!["attemptOf"]);
            Assert.IsNull(tests[2]!["attemptIndex"]);
            Assert.IsNull(tests[2]!["attemptOf"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_SameUidFromDifferentTargetFrameworks_DoesNotLabelRowsAsAttempts()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = Path.Combine(directory, "first.html");
            string secondPath = Path.Combine(directory, "second.html");
            File.WriteAllText(firstPath, CreateReport("tests.dll", "MSTest", Epoch, Epoch, Test("same", "net8", "passed", 1)));
            File.WriteAllText(secondPath, CreateReport("tests.dll", "MSTest", Epoch, Epoch, Test("same", "net9", "failed", 2)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(firstPath, module: "tests.dll", targetFramework: "net8.0", architecture: "x64"),
                    CreateInput(secondPath, module: "tests.dll", targetFramework: "net9.0", architecture: "x64"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNotNull(output);
            var tests = (JsonArray)ParseReport(File.ReadAllText(output.Path))["tests"]!;
            Assert.IsNull(tests[0]!["attemptIndex"]);
            Assert.IsNull(tests[0]!["attemptOf"]);
            Assert.IsNull(tests[1]!["attemptIndex"]);
            Assert.IsNull(tests[1]!["attemptOf"]);
            Assert.AreEqual("net8.0", (string?)tests[0]!["targetFramework"]);
            Assert.AreEqual("net9.0", (string?)tests[1]!["targetFramework"]);
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
            string firstPath = Path.Combine(directory, "first.html");
            string secondPath = Path.Combine(directory, "second.html");
            File.WriteAllText(firstPath, CreateReport("same.dll", "MSTest", Epoch, Epoch, Test("a", "A", "passed", 1)));
            File.WriteAllText(secondPath, CreateReport("same.dll", "MSTest", Epoch, Epoch, Test("b", "B", "failed", 2)));
            HtmlArtifactPostProcessor processor = new();
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
    public async Task ProcessAsync_WhenAnInputIsNotAnHtmlReport_Throws()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = Path.Combine(directory, "first.html");
            string secondPath = Path.Combine(directory, "second.html");
            File.WriteAllText(firstPath, CreateReport("same.dll", "MSTest", Epoch, Epoch, Test("a", "A", "passed", 1)));
            File.WriteAllText(secondPath, "<html>not a report</html>");

            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => new HtmlArtifactPostProcessor().ProcessAsync(
                    [CreateInput(firstPath), CreateInput(secondPath)],
                    directory,
                    new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                    CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

#if NETCOREAPP
    [TestMethod]
    public async Task ProcessAsync_WhenMergedDirectoryIsAReparsePoint_DoesNotMerge()
    {
        // 'merged' is a fixed, predictable child name under the orchestrator-supplied output directory.
        // A symlink or junction planted at that name becomes the base the merge writes into, redirecting
        // the merged report to wherever the link points and letting the run write outside the directory it
        // was given, so the processor must refuse rather than write through it.
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
                // Creating a directory symlink needs elevation or Developer Mode on Windows.
                Assert.Inconclusive("Host cannot create directory symbolic links.");
                return;
            }

            string firstPath = Path.Combine(resultsDirectory, "first.html");
            string secondPath = Path.Combine(resultsDirectory, "second.html");
            File.WriteAllText(firstPath, CreateReport("first.dll", "MSTest", Epoch, Epoch, Test("a", "A", "passed", 1)));
            File.WriteAllText(secondPath, CreateReport("second.dll", "MSTest", Epoch, Epoch, Test("b", "B", "passed", 2)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
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

            string firstPath = Path.Combine(resultsDirectory, "first.html");
            string secondPath = Path.Combine(resultsDirectory, "second.html");
            File.WriteAllText(firstPath, CreateReport("first.dll", "MSTest", Epoch, Epoch, Test("a", "A", "passed", 1)));
            File.WriteAllText(secondPath, CreateReport("second.dll", "MSTest", Epoch, Epoch, Test("b", "B", "passed", 2)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
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

    [TestMethod]
    public void CreateMergeId_IncludesExecutionProvenance()
    {
        string path = Path.Combine(Path.GetTempPath(), "report.html");
        InputArtifact first = CreateInput(path, module: "module.dll", targetFramework: "net8.0", architecture: "x64", executionId: "execution-1");
        InputArtifact second = CreateInput(path, module: "module.dll", targetFramework: "net8.0", architecture: "x64", executionId: "execution-2");

        Assert.AreNotEqual(
            HtmlArtifactPostProcessor.CreateMergeId([first]),
            HtmlArtifactPostProcessor.CreateMergeId([second]));
    }

    [TestMethod]
    public async Task ProcessAsync_RetryModeUsesDistinctOutputPath()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = Path.Combine(directory, "first.html");
            string secondPath = Path.Combine(directory, "second.html");
            File.WriteAllText(firstPath, CreateReport("tests.dll", "MSTest", Epoch, Epoch.AddMinutes(1), Test("a", "A", "passed", 1)));
            File.WriteAllText(secondPath, CreateReport("tests.dll", "MSTest", Epoch.AddMinutes(2), Epoch.AddMinutes(3), Test("b", "B", "passed", 1)));
            InputArtifact[] inputs =
            [
                CreateInput(firstPath, module: "tests.dll", executionId: "1"),
                CreateInput(secondPath, module: "tests.dll", executionId: "2"),
            ];
            var processor = new HtmlArtifactPostProcessor();

            ProcessedArtifact? moduleOutput = await processor.ProcessAsync(
                inputs,
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.TestModules),
                CancellationToken.None);
            ProcessedArtifact? retryOutput = await processor.ProcessAsync(
                inputs,
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(moduleOutput);
            Assert.IsNotNull(retryOutput);
            Assert.AreNotEqual(moduleOutput.Path, retryOutput.Path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ForRetryAttempts_KeepsAllInitialAttemptTests()
    {
        // Attempt 1 runs "flaky" and "stable"; attempt 2 (the retry) only re-runs "flaky", which is
        // exactly what RetryArtifactProcessor supplies for a partial re-run. "stable" must still be
        // present in the merged report even though it was never retried.
        string directory = CreateTemporaryDirectory();
        try
        {
            string attempt1Path = Path.Combine(directory, "attempt1.html");
            string attempt2Path = Path.Combine(directory, "attempt2.html");
            File.WriteAllText(
                attempt1Path,
                CreateReport(
                    "tests.dll",
                    "MSTest",
                    Epoch,
                    Epoch.AddMinutes(1),
                    Test("flaky", "Flaky test", "failed", 10),
                    Test("stable", "Stable test", "passed", 5)));
            File.WriteAllText(
                attempt2Path,
                CreateReport("tests.dll", "MSTest", Epoch.AddMinutes(2), Epoch.AddMinutes(3), Test("flaky", "Flaky test", "passed", 12)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(attempt1Path, module: "tests.dll", executionId: "1"),
                    CreateInput(attempt2Path, module: "tests.dll", executionId: "2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);
            var tests = (JsonArray)ParseReport(File.ReadAllText(output.Path))["tests"]!;
            Assert.HasCount(2, tests);
            Assert.Contains(test => (string?)test!["uid"] == "flaky", tests);
            Assert.Contains(test => (string?)test!["uid"] == "stable", tests);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ForRetryAttempts_UsesExecutionOrderForFinalOutcome()
    {
        // Attempt 1 is supplied FIRST but is stamped with a LATER embedded start time than attempt 2:
        // if the merger sorted by embedded start time (as TestModules merges do), attempt 1 would be
        // treated as the "last" occurrence and its "failed" outcome would incorrectly win. Only
        // respecting the array order that RetryArtifactProcessor already supplies (attempt 1, then
        // attempt 2) picks the correct, truly-final "passed" outcome.
        string directory = CreateTemporaryDirectory();
        try
        {
            string attempt1Path = Path.Combine(directory, "attempt1.html");
            string attempt2Path = Path.Combine(directory, "attempt2.html");
            File.WriteAllText(
                attempt1Path,
                CreateReport("tests.dll", "MSTest", Epoch.AddMinutes(10), Epoch.AddMinutes(11), Test("flaky", "Flaky test", "failed", 10)));
            File.WriteAllText(
                attempt2Path,
                CreateReport("tests.dll", "MSTest", Epoch, Epoch.AddMinutes(1), Test("flaky", "Flaky test", "passed", 7)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(attempt1Path, module: "tests.dll", executionId: "1"),
                    CreateInput(attempt2Path, module: "tests.dll", executionId: "2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);
            var test = (JsonObject)((JsonArray)ParseReport(File.ReadAllText(output.Path))["tests"]!)[0]!;
            Assert.AreEqual("passed", (string?)test["outcome"]);
            Assert.AreEqual(7, (double?)test["durationMs"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ForRetryAttempts_LeavesAmbiguousFoldedRowsUncollapsed()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string attempt1Path = Path.Combine(directory, "attempt1.html");
            string attempt2Path = Path.Combine(directory, "attempt2.html");
            JsonObject firstRowAttempt1 = Test("shared-uid", "Shared title", "failed", 10);
            JsonObject secondRowAttempt1 = Test("shared-uid", "Shared title", "passed", 5);
            JsonObject firstRowAttempt2 = Test("shared-uid", "Shared title", "passed", 12);
            JsonObject secondRowAttempt2 = Test("shared-uid", "Shared title", "passed", 6);
            File.WriteAllText(
                attempt1Path,
                CreateReport(
                    "tests.dll",
                    "MSTest",
                    Epoch,
                    Epoch.AddMinutes(1),
                    firstRowAttempt1,
                    secondRowAttempt1));
            File.WriteAllText(
                attempt2Path,
                CreateReport(
                    "tests.dll",
                    "MSTest",
                    Epoch.AddMinutes(2),
                    Epoch.AddMinutes(3),
                    firstRowAttempt2,
                    secondRowAttempt2));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(attempt1Path, module: "tests.dll", executionId: "1"),
                    CreateInput(attempt2Path, module: "tests.dll", executionId: "2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);
            JsonObject merged = ParseReport(File.ReadAllText(output.Path));
            var tests = (JsonArray)merged["tests"]!;
            Assert.HasCount(4, tests);
            Assert.DoesNotContain(test => test!["flaky"] is not null, tests);
            Assert.DoesNotContain(test => test!["retryAttempts"] is not null, tests);
            Assert.AreEqual(4, (int?)merged["summary"]!["total"]);
            Assert.AreEqual(0, (int?)merged["summary"]!["flaky"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ForRetryAttempts_CorrelatesInProcessRetriesAcrossHostAttempts()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string attempt1Path = Path.Combine(directory, "attempt1.html");
            string attempt2Path = Path.Combine(directory, "attempt2.html");
            JsonObject inProcessAttempt1 = Test("retry-uid", "Retried test", "failed", 10);
            inProcessAttempt1["retryAttemptNumber"] = 1;
            inProcessAttempt1["isSupersededRetryAttempt"] = true;
            JsonObject inProcessAttempt2 = Test("retry-uid", "Retried test", "failed", 11);
            inProcessAttempt2["retryAttemptNumber"] = 2;
            inProcessAttempt2["isSupersededRetryAttempt"] = false;
            JsonObject hostAttempt2 = Test("retry-uid", "Retried test", "passed", 12);
            File.WriteAllText(
                attempt1Path,
                CreateReport(
                    "tests.dll",
                    "MSTest",
                    Epoch,
                    Epoch.AddMinutes(1),
                    inProcessAttempt1,
                    inProcessAttempt2));
            File.WriteAllText(
                attempt2Path,
                CreateReport(
                    "tests.dll",
                    "MSTest",
                    Epoch.AddMinutes(2),
                    Epoch.AddMinutes(3),
                    hostAttempt2));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(attempt1Path, module: "tests.dll", executionId: "1"),
                    CreateInput(attempt2Path, module: "tests.dll", executionId: "2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);
            JsonObject merged = ParseReport(File.ReadAllText(output.Path));
            var tests = (JsonArray)merged["tests"]!;
            Assert.HasCount(1, tests);
            var test = (JsonObject)tests[0]!;
            Assert.IsTrue((bool?)test["flaky"]);
            Assert.HasCount(2, (JsonArray)test["retryAttempts"]!);
            Assert.AreEqual(1, (int?)merged["summary"]!["total"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ForRetryAttempts_UsesFinalAttemptExitCode()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string attempt1Path = Path.Combine(directory, "attempt1.html");
            string attempt2Path = Path.Combine(directory, "attempt2.html");
            File.WriteAllText(
                attempt1Path,
                CreateReportWithExitCode(
                    2,
                    "tests.dll",
                    "MSTest",
                    Epoch,
                    Epoch.AddMinutes(1),
                    Test("flaky", "Flaky test", "failed", 10)));
            File.WriteAllText(
                attempt2Path,
                CreateReportWithExitCode(
                    0,
                    "tests.dll",
                    "MSTest",
                    Epoch.AddMinutes(2),
                    Epoch.AddMinutes(3),
                    Test("flaky", "Flaky test", "passed", 12)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(attempt1Path, module: "tests.dll", executionId: "1"),
                    CreateInput(attempt2Path, module: "tests.dll", executionId: "2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);
            Assert.AreEqual(0, (int?)ParseReport(File.ReadAllText(output.Path))["exitCode"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ForRetryAttempts_FlagsFlakyTestAndRetainsAttemptHistory()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string attempt1Path = Path.Combine(directory, "attempt1.html");
            string attempt2Path = Path.Combine(directory, "attempt2.html");
            File.WriteAllText(
                attempt1Path,
                CreateReport(
                    "tests.dll",
                    "MSTest",
                    Epoch,
                    Epoch.AddMinutes(1),
                    Test(
                        "flaky",
                        "Flaky test",
                        "failed",
                        10,
                        errorMessage: "Assert.AreEqual failed",
                        exceptionType: "System.Exception",
                        stackTrace: "at Test.Run()",
                        standardOutput: "attempt 1 output",
                        standardError: "attempt 1 error")));
            File.WriteAllText(
                attempt2Path,
                CreateReport("tests.dll", "MSTest", Epoch.AddMinutes(2), Epoch.AddMinutes(3), Test("flaky", "Flaky test", "passed", 12)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(attempt1Path, module: "tests.dll", executionId: "1"),
                    CreateInput(attempt2Path, module: "tests.dll", executionId: "2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);
            JsonObject merged = ParseReport(File.ReadAllText(output.Path));
            Assert.AreEqual(1, (int?)merged["summary"]!["flaky"]);
            var test = (JsonObject)((JsonArray)merged["tests"]!)[0]!;
            Assert.AreEqual("passed", (string?)test["outcome"]);
            Assert.IsTrue((bool?)test["flaky"]);
            Assert.AreEqual(1, (int?)test["retries"]);

            var history = (JsonArray)test["retryAttempts"]!;
            Assert.HasCount(1, history);
            var priorAttempt = (JsonObject)history[0]!;
            Assert.AreEqual(1, (int?)priorAttempt["attempt"]);
            Assert.AreEqual("failed", (string?)priorAttempt["outcome"]);
            Assert.AreEqual(10, (double?)priorAttempt["durationMs"]);
            Assert.AreEqual("Assert.AreEqual failed", (string?)priorAttempt["errorMessage"]);
            Assert.AreEqual("System.Exception", (string?)priorAttempt["exceptionType"]);
            Assert.AreEqual("at Test.Run()", (string?)priorAttempt["stackTrace"]);
            Assert.AreEqual("attempt 1 output", (string?)priorAttempt["standardOutput"]);
            Assert.AreEqual("attempt 1 error", (string?)priorAttempt["standardError"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ForRetryAttempts_LeavesStableNonRetriedTestUnchanged()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string attempt1Path = Path.Combine(directory, "attempt1.html");
            string attempt2Path = Path.Combine(directory, "attempt2.html");
            File.WriteAllText(
                attempt1Path,
                CreateReport(
                    "tests.dll",
                    "MSTest",
                    Epoch,
                    Epoch.AddMinutes(1),
                    Test("flaky", "Flaky test", "failed", 10),
                    Test("stable", "Stable test", "passed", 5)));
            File.WriteAllText(
                attempt2Path,
                CreateReport("tests.dll", "MSTest", Epoch.AddMinutes(2), Epoch.AddMinutes(3), Test("flaky", "Flaky test", "passed", 12)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(attempt1Path, module: "tests.dll", executionId: "1"),
                    CreateInput(attempt2Path, module: "tests.dll", executionId: "2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);
            var tests = (JsonArray)ParseReport(File.ReadAllText(output.Path))["tests"]!;
            var stable = (JsonObject)tests.Single(test => (string?)test!["uid"] == "stable")!;
            Assert.AreEqual("passed", (string?)stable["outcome"]);
            Assert.AreEqual(5, (double?)stable["durationMs"]);
            Assert.IsNull(stable["retryAttempts"]);
            Assert.IsNull(stable["retries"]);
            Assert.IsNull(stable["flaky"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ForRetryAttempts_DoesNotDoubleCountLogicalTotals()
    {
        // Attempt 1: "retried" (failed) + "onlyInAttempt1" (passed). Attempt 2 (the retry): "retried"
        // (now passed) + "onlyInAttempt2", a test that only exists in the later attempt. The logical
        // run therefore has exactly 3 tests, not the 4 raw rows the two reports contain together.
        string directory = CreateTemporaryDirectory();
        try
        {
            string attempt1Path = Path.Combine(directory, "attempt1.html");
            string attempt2Path = Path.Combine(directory, "attempt2.html");
            File.WriteAllText(
                attempt1Path,
                CreateReport(
                    "tests.dll",
                    "MSTest",
                    Epoch,
                    Epoch.AddMinutes(1),
                    Test("retried", "Retried test", "failed", 10),
                    Test("onlyInAttempt1", "Only in attempt 1", "passed", 5)));
            File.WriteAllText(
                attempt2Path,
                CreateReport(
                    "tests.dll",
                    "MSTest",
                    Epoch.AddMinutes(2),
                    Epoch.AddMinutes(3),
                    Test("retried", "Retried test", "passed", 12),
                    Test("onlyInAttempt2", "Only in attempt 2", "passed", 6)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(attempt1Path, module: "tests.dll", executionId: "1"),
                    CreateInput(attempt2Path, module: "tests.dll", executionId: "2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);
            var summary = (JsonObject)ParseReport(File.ReadAllText(output.Path))["summary"]!;
            Assert.AreEqual(3, (int?)summary["total"]);
            Assert.AreEqual(3, (int?)summary["passed"]);
            Assert.AreEqual(0, (int?)summary["failed"]);
            Assert.AreEqual(1, (int?)summary["flaky"]);
            Assert.AreEqual(33, (double?)summary["totalDurationMs"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ForTestModules_SummaryHasNoFlakyKey()
    {
        // The "flaky" summary key and per-test "retries"/"retryAttempts"/"flaky" fields are exclusive
        // to RetryAttempts collapsing; TestModules merges must keep their existing shape untouched.
        string directory = CreateTemporaryDirectory();
        try
        {
            string firstPath = Path.Combine(directory, "first.html");
            string secondPath = Path.Combine(directory, "second.html");
            File.WriteAllText(firstPath, CreateReport("first.dll", "MSTest", Epoch, Epoch, Test("a", "A", "passed", 1)));
            File.WriteAllText(secondPath, CreateReport("second.dll", "MSTest", Epoch, Epoch, Test("b", "B", "passed", 2)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [CreateInput(firstPath), CreateInput(secondPath)],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.TestModules),
                CancellationToken.None);

            Assert.IsNotNull(output);
            JsonObject merged = ParseReport(File.ReadAllText(output.Path));
            Assert.IsNull(merged["summary"]!["flaky"]);
            var tests = (JsonArray)merged["tests"]!;
            Assert.IsNull(tests[0]!["retryAttempts"]);
            Assert.IsNull(tests[0]!["retries"]);
            Assert.IsNull(tests[0]!["flaky"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ForRetryAttempts_TemplateRendersFlakySummaryCardGuardAndBadgeMarkup()
    {
        // The embedded JSON alone isn't enough: without matching template markup a flaky test would
        // be collapsed silently. Assert the shipped report-template.html (embedded verbatim in the
        // merged artifact) both (a) only surfaces the "Flaky" summary card when summary.flaky is
        // present, and (b) renders a visible badge on rows whose collapsed outcome is "flaky".
        string directory = CreateTemporaryDirectory();
        try
        {
            string attempt1Path = Path.Combine(directory, "attempt1.html");
            string attempt2Path = Path.Combine(directory, "attempt2.html");
            File.WriteAllText(
                attempt1Path,
                CreateReport("tests.dll", "MSTest", Epoch, Epoch.AddMinutes(1), Test("flaky", "Flaky test", "failed", 10)));
            File.WriteAllText(
                attempt2Path,
                CreateReport("tests.dll", "MSTest", Epoch.AddMinutes(2), Epoch.AddMinutes(3), Test("flaky", "Flaky test", "passed", 12)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(attempt1Path, module: "tests.dll", executionId: "1"),
                    CreateInput(attempt2Path, module: "tests.dll", executionId: "2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);
            string html = File.ReadAllText(output.Path);

            // Summary card: only pushed onto the rendered cards when the field is present.
            Assert.Contains("typeof summary.flaky === \"number\"", html);
            Assert.Contains("cards.push({ key: \"flaky\", label: \"Flaky\", cls: \"flaky\" });", html);
            Assert.Contains(".summary-card.flaky .value", html);

            // Row badge: rendered from the per-test "flaky" flag, visually distinct from the plain
            // "#N of M" attempt-count badge already used for TestModules concatenation.
            Assert.Contains("if (t.flaky) {", html);
            Assert.Contains("flakyBadge.className = \"badge flaky\";", html);
            Assert.Contains(".badge.flaky {", html);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ForRetryAttempts_TemplateTreatsRetryHistoryAsDetailEvenWithoutFinalErrorOrOutput()
    {
        // A flaky test's FINAL attempt passed, so it carries no errorMessage/stackTrace/standardOutput/
        // standardError/exceptionType of its own. The row must still be expandable — via retryAttempts
        // alone — so the failing prior attempt(s) stay inspectable instead of silently disappearing.
        string directory = CreateTemporaryDirectory();
        try
        {
            string attempt1Path = Path.Combine(directory, "attempt1.html");
            string attempt2Path = Path.Combine(directory, "attempt2.html");
            File.WriteAllText(
                attempt1Path,
                CreateReport("tests.dll", "MSTest", Epoch, Epoch.AddMinutes(1), Test("flaky", "Flaky test", "failed", 10, errorMessage: "boom")));
            File.WriteAllText(
                attempt2Path,
                CreateReport(
                    "tests.dll",
                    "MSTest",
                    Epoch.AddMinutes(2),
                    Epoch.AddMinutes(3),
                    Test("flaky", "Flaky test", "passed", 12))); // no error/output fields on the final attempt.

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(attempt1Path, module: "tests.dll", executionId: "1"),
                    CreateInput(attempt2Path, module: "tests.dll", executionId: "2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);

            // The final row genuinely has no detail fields of its own (asserts the test's own setup
            // is exercising the "no final error/output" case rather than accidentally not).
            var test = (JsonObject)((JsonArray)ParseReport(File.ReadAllText(output.Path))["tests"]!)[0]!;
            Assert.AreEqual("passed", (string?)test["outcome"]);
            Assert.IsNull(test["errorMessage"]);
            Assert.IsNull(test["standardOutput"]);
            Assert.IsNull(test["standardError"]);
            Assert.IsTrue((bool?)test["flaky"]);
            Assert.IsNotNull(test["retryAttempts"]);

            string html = File.ReadAllText(output.Path);
            Assert.Contains("|| (t.retryAttempts && t.retryAttempts.length));", html);
            Assert.Contains("prior attempt(s)", html);
            Assert.DoesNotContain("non-passing attempt(s)", html);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_ForRetryAttempts_TemplateRendersPriorAttemptOutcomeDurationAndDetailFields()
    {
        // The retry history is more than a number: verify the template actually renders each prior
        // attempt's outcome pill, duration, and whichever error/output detail fields it carried.
        string directory = CreateTemporaryDirectory();
        try
        {
            string attempt1Path = Path.Combine(directory, "attempt1.html");
            string attempt2Path = Path.Combine(directory, "attempt2.html");
            File.WriteAllText(
                attempt1Path,
                CreateReport(
                    "tests.dll",
                    "MSTest",
                    Epoch,
                    Epoch.AddMinutes(1),
                    Test(
                        "flaky",
                        "Flaky test",
                        "failed",
                        10,
                        errorMessage: "Assert.AreEqual failed",
                        exceptionType: "System.Exception",
                        stackTrace: "at Test.Run()",
                        standardOutput: "attempt 1 output",
                        standardError: "attempt 1 error")));
            File.WriteAllText(
                attempt2Path,
                CreateReport("tests.dll", "MSTest", Epoch.AddMinutes(2), Epoch.AddMinutes(3), Test("flaky", "Flaky test", "passed", 12)));

            ProcessedArtifact? output = await new HtmlArtifactPostProcessor().ProcessAsync(
                [
                    CreateInput(attempt1Path, module: "tests.dll", executionId: "1"),
                    CreateInput(attempt2Path, module: "tests.dll", executionId: "2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None, ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);
            string html = File.ReadAllText(output.Path);

            // Section heading + per-attempt outcome pill and duration label.
            Assert.Contains("historyHeading.textContent = \"Retry history\";", html);
            Assert.Contains("attemptPill.className = \"outcome \" + (attempt.outcome || \"\");", html);
            Assert.Contains("\"Attempt \" + attempt.attempt", html);
            Assert.Contains("fmtDuration(attempt.durationMs)", html);

            // Per-attempt error/output detail fields, mirroring what the final row itself would show.
            Assert.Contains("addBlock(attemptEl, \"h5\", \"Exception type\", attempt.exceptionType);", html);
            Assert.Contains("addBlock(attemptEl, \"h5\", \"Error message\", attempt.errorMessage);", html);
            Assert.Contains("addBlock(attemptEl, \"h5\", \"Stack trace\",   attempt.stackTrace);", html);
            Assert.Contains("addBlock(attemptEl, \"h5\", \"Standard output\", attempt.standardOutput);", html);
            Assert.Contains("addBlock(attemptEl, \"h5\", \"Standard error\",  attempt.standardError);", html);

            // And the actual attempt data made it into the embedded JSON that this markup renders.
            var test = (JsonObject)((JsonArray)ParseReport(html)["tests"]!)[0]!;
            var history = (JsonArray)test["retryAttempts"]!;
            var priorAttempt = (JsonObject)history[0]!;
            Assert.AreEqual("failed", (string?)priorAttempt["outcome"]);
            Assert.AreEqual(10, (double?)priorAttempt["durationMs"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static JsonObject Test(
        string uid,
        string displayName,
        string outcome,
        double durationMs,
        string? errorMessage = null,
        string? exceptionType = null,
        string? stackTrace = null,
        string? standardOutput = null,
        string? standardError = null)
    {
        var test = new JsonObject
        {
            ["rowKey"] = 0,
            ["uid"] = uid,
            ["displayName"] = displayName,
            ["outcome"] = outcome,
            ["durationMs"] = durationMs,
        };

        if (errorMessage is not null)
        {
            test["errorMessage"] = errorMessage;
        }

        if (exceptionType is not null)
        {
            test["exceptionType"] = exceptionType;
        }

        if (stackTrace is not null)
        {
            test["stackTrace"] = stackTrace;
        }

        if (standardOutput is not null)
        {
            test["standardOutput"] = standardOutput;
        }

        if (standardError is not null)
        {
            test["standardError"] = standardError;
        }

        return test;
    }

    private static string CreateReport(
        string testApplication,
        string framework,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        params JsonObject[] tests)
    {
        var testArray = new JsonArray();
        foreach (JsonObject test in tests)
        {
            testArray.Add(test);
        }

        var report = new JsonObject
        {
            ["schemaVersion"] = "1",
            ["generator"] = "Microsoft.Testing.Extensions.HtmlReport",
            ["generatorVersion"] = "1.0.0",
            ["testApplication"] = testApplication,
            ["machineName"] = "machine",
            ["userName"] = "user",
            ["framework"] = framework,
            ["frameworkUid"] = framework,
            ["frameworkVersion"] = "1.0.0",
            ["startTime"] = startTime.ToString("O", CultureInfo.InvariantCulture),
            ["endTime"] = endTime.ToString("O", CultureInfo.InvariantCulture),
            ["exitCode"] = 0,
            ["tests"] = testArray,
            ["summary"] = new JsonObject(),
        };

        return $"<!DOCTYPE html><script id=\"mtp-data\" type=\"application/json\">{report.ToJsonString()}</script>";
    }

    private static string CreateReportWithExitCode(
        int exitCode,
        string testApplication,
        string framework,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        params JsonObject[] tests)
    {
        JsonObject report = ParseReport(CreateReport(testApplication, framework, startTime, endTime, tests));
        report["exitCode"] = exitCode;
        return $"<!DOCTYPE html><script id=\"mtp-data\" type=\"application/json\">{report.ToJsonString()}</script>";
    }

    private static JsonObject ParseReport(string html)
        => (JsonObject)JsonNode.Parse(HtmlReportEngine.ExtractReportJson(html))!;

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"html-post-processor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static InputArtifact CreateInput(
        string path,
        string? module = null,
        string? targetFramework = null,
        string? architecture = null,
        string? executionId = null)
        => new(path, HtmlReportGenerator.HtmlArtifactKind, module, targetFramework, architecture, executionId);
}
