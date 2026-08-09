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
            Assert.AreEqual("execution-1", (string?)tests[0]!["executionId"]);
            Assert.AreEqual("Passed </script><img src=x>", (string?)tests[1]!["displayName"]);

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

    private static JsonObject Test(string uid, string displayName, string outcome, double durationMs)
        => new()
        {
            ["rowKey"] = 0,
            ["uid"] = uid,
            ["displayName"] = displayName,
            ["outcome"] = outcome,
            ["durationMs"] = durationMs,
        };

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
