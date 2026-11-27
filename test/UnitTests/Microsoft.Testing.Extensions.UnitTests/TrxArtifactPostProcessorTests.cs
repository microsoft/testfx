// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

namespace Microsoft.Testing.Extensions.UnitTests;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

[TestClass]
public sealed class TrxArtifactPostProcessorTests
{
    [TestMethod]
    public void Capabilities_DescribeTrxArtifacts()
    {
        TrxArtifactPostProcessor processor = new();

        Assert.AreSequenceEqual([TrxReportEngine.TrxArtifactKind], processor.SupportedKinds);
        Assert.AreSequenceEqual([ArtifactPostProcessingMode.TestModules, ArtifactPostProcessingMode.RetryAttempts], processor.SupportedModes);
        Assert.AreSequenceEqual([".trx"], processor.SupportedFileExtensionsFallback);
        Assert.IsFalse(processor.SupportsTruncatedRuns);
    }

    [TestMethod]
    public async Task ProcessAsync_WithFewerThanTwoInputs_ReturnsNull()
    {
        TrxArtifactPostProcessor processor = new();
        var input = new InputArtifact("input.trx", TrxReportEngine.TrxArtifactKind, null, null, null, null);

        Assert.IsNull(await processor.ProcessAsync(
            [input],
            Path.GetTempPath(),
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
            CancellationToken.None));
    }

    [TestMethod]
    public async Task ProcessAsync_WithTwoInputs_WritesUniquelyNamedMergedReport()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"trx-post-processor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string firstPath = Path.Combine(directory, "first.trx");
            string secondPath = Path.Combine(directory, "second.trx");
            WriteMinimalReport(firstPath, "first");
            WriteMinimalReport(secondPath, "second");
            TrxArtifactPostProcessor processor = new();

            ProcessedArtifact? output = await processor.ProcessAsync(
                [
                    new InputArtifact(firstPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-1"),
                    new InputArtifact(secondPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNotNull(output);
            Assert.AreEqual(TrxReportEngine.TrxArtifactKind, output.Kind);
            Assert.MatchesRegex(new Regex("^merged-[0-9a-f]{32}\\.trx$", RegexOptions.CultureInvariant), Path.GetFileName(output.Path));
            Assert.IsTrue(File.Exists(output.Path));
            Assert.AreEqual("TestRun", XDocument.Load(output.Path).Root!.Name.LocalName);

            // The merged report must be nested rather than written beside its inputs, so that the
            // non-recursive '*.trx' globs used to publish a results directory cannot pick up both the
            // merged report and the per-module reports it consumed (which would double-count every test).
            Assert.AreEqual("merged", Path.GetFileName(Path.GetDirectoryName(output.Path)));
            Assert.AreEqual(
                Path.GetFullPath(directory),
                Path.GetFullPath(Path.Combine(Path.GetDirectoryName(output.Path)!, "..")));
            Assert.AreSequenceEqual(
                ["first.trx", "second.trx"],
                Directory.GetFiles(directory, "*.trx").Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_RetryAttempts_ReplacesCompleteTestIdGroupsAndRebuildsCounters()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"trx-post-processor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string firstPath = Path.Combine(directory, "attempt-1.trx");
            string secondPath = Path.Combine(directory, "attempt-2.trx");
            WriteRetryReport(
                firstPath,
                "attempt-1",
                [
                    ("fixture-test", "fixture-id", "FixtureTest", "Passed"),
                    ("cleanup-failure", "cleanup-id", "[CleanupTests ClassCleanup]", "Failed"),
                    ("retry-row-1-old", "retry-id", "ParameterizedCase", "Failed"),
                    ("retry-row-2-old", "retry-id", "ParameterizedCase", "Passed"),
                    ("recovered-timeout-old", "recovered-timeout-id", "RecoveredTimeout", "Timeout"),
                ],
                includeRunInfo: true);
            WriteRetryReport(
                secondPath,
                "attempt-2",
                [
                    ("retry-row-1-new", "retry-id", "ParameterizedCase", "Passed"),
                    ("retry-row-2-new", "retry-id", "ParameterizedCase", "Passed"),
                    ("recovered-timeout-new", "recovered-timeout-id", "RecoveredTimeout", "Passed"),
                    ("retained-timeout-new", "retained-timeout-id", "RetainedTimeout", "Timeout"),
                ]);
            TrxArtifactPostProcessor processor = new();

            ProcessedArtifact? output = await processor.ProcessAsync(
                [
                    new InputArtifact(secondPath, TrxReportEngine.TrxArtifactKind, null, null, null, "2"),
                    new InputArtifact(firstPath, TrxReportEngine.TrxArtifactKind, null, null, null, "1"),
                ],
                directory,
                new ArtifactPostProcessingContext(
                    ArtifactPostProcessingTruncationReason.None,
                    ArtifactPostProcessingMode.RetryAttempts),
                CancellationToken.None);

            Assert.IsNotNull(output);
            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            var merged = XDocument.Load(output.Path);
            XElement[] results = [.. merged.Descendants(ns + "UnitTestResult")];
            Assert.HasCount(6, results);

            XElement cleanup = Assert.ContainsSingle(result => result.Attribute("testName")?.Value == "[CleanupTests ClassCleanup]", results);
            Assert.AreEqual("Failed", cleanup.Attribute("outcome")?.Value);
            Assert.Contains("cleanup failure details", string.Join(" ", cleanup.Descendants(ns + "Message").Select(message => message.Value)));
            XElement fixtureTest = Assert.ContainsSingle(result => result.Attribute("testName")?.Value == "FixtureTest", results);
            Assert.AreEqual("Passed", fixtureTest.Attribute("outcome")?.Value);
            XElement retainedTimeout = Assert.ContainsSingle(result => result.Attribute("testName")?.Value == "RetainedTimeout", results);
            Assert.AreEqual("Failed", retainedTimeout.Attribute("outcome")?.Value);

            XElement[] retryRows = [.. results.Where(result => result.Attribute("testId")?.Value == "retry-id")];
            Assert.HasCount(2, retryRows);
            Assert.AreSequenceEqual(
                ["retry-row-1-new", "retry-row-2-new"],
                retryRows.Select(result => result.Attribute("executionId")!.Value).OrderBy(value => value, StringComparer.Ordinal));
            Assert.AreEqual(2, retryRows.Count(result => result.Attribute("outcome")?.Value == "Passed"));
            XElement recoveredTimeout = Assert.ContainsSingle(result => result.Attribute("testName")?.Value == "RecoveredTimeout", results);
            Assert.AreEqual("recovered-timeout-new", recoveredTimeout.Attribute("executionId")?.Value);
            Assert.AreEqual("Passed", recoveredTimeout.Attribute("outcome")?.Value);

            XElement[] entries = [.. merged.Descendants(ns + "TestEntry")];
            Assert.HasCount(6, entries);
            Assert.AreSequenceEqual(
                results.Select(result => result.Attribute("executionId")!.Value).OrderBy(value => value, StringComparer.Ordinal),
                entries.Select(entry => entry.Attribute("executionId")!.Value).OrderBy(value => value, StringComparer.Ordinal));
            XElement[] definitions = [.. merged.Descendants(ns + "UnitTest")];
            Assert.HasCount(5, definitions);
            Assert.AreSequenceEqual(
                results.Select(result => result.Attribute("testId")!.Value).Distinct().OrderBy(value => value, StringComparer.Ordinal),
                definitions.Select(definition => definition.Attribute("id")!.Value).OrderBy(value => value, StringComparer.Ordinal));
            XElement retryDefinition = Assert.ContainsSingle(definition => definition.Attribute("id")?.Value == "retry-id", definitions);
            Assert.AreEqual("retry-row-1-new", retryDefinition.Element(ns + "Execution")?.Attribute("id")?.Value);
            Assert.AreEqual("retry-id", retryRows[0].Attribute("testId")?.Value);
            Assert.AreEqual("retry-id", retryRows[1].Attribute("testId")?.Value);

            XElement summary = Assert.ContainsSingle(merged.Descendants(ns + "ResultSummary"));
            Assert.AreEqual("Failed", summary.Attribute("outcome")?.Value);
            XElement counters = Assert.ContainsSingle(summary.Elements(ns + "Counters"));
            Assert.AreEqual("6", counters.Attribute("total")?.Value);
            Assert.AreEqual("5", counters.Attribute("executed")?.Value);
            Assert.AreEqual("4", counters.Attribute("passed")?.Value);
            Assert.AreEqual("1", counters.Attribute("failed")?.Value);
            Assert.AreEqual("1", counters.Attribute("timeout")?.Value);
            Assert.AreEqual("0", counters.Attribute("notExecuted")?.Value);
            Assert.IsEmpty(summary.Elements(ns + "RunInfos"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WithReorderedInputs_ProducesIdenticalOutput()
    {
        // RFC 018 requires processing to be deterministic and idempotent, because orchestrators may retry
        // transient failures. The run id is derived from the ordered inputs, so reversing them must still
        // land on the same output path with byte-identical content.
        string directory = Path.Combine(Path.GetTempPath(), $"trx-post-processor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string firstPath = Path.Combine(directory, "first.trx");
            string secondPath = Path.Combine(directory, "second.trx");
            WriteMinimalReport(firstPath, "first");
            WriteMinimalReport(secondPath, "second");
            TrxArtifactPostProcessor processor = new();

            ProcessedArtifact? output = await processor.ProcessAsync(
                [
                    new InputArtifact(firstPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-1"),
                    new InputArtifact(secondPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNotNull(output);
            byte[] firstMerge = File.ReadAllBytes(output.Path);

            ProcessedArtifact? retriedOutput = await processor.ProcessAsync(
                [
                    new InputArtifact(secondPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-2"),
                    new InputArtifact(firstPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-1"),
                ],
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
    public async Task ProcessAsync_NestsAttachmentDeploymentRootBesideTheMergedReport()
    {
        // Regression guard for the merged report's attachments: MergeToFileAsync writes the attachment
        // deployment root next to the report it produces, and the merged TRX records that root as a
        // RELATIVE path. Nesting the report therefore has to carry the deployment root along with it, or
        // downloaded merged reports would carry dangling attachment references.
        string directory = Path.Combine(Path.GetTempPath(), $"trx-post-processor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string firstPath = WriteReportWithAttachment(Path.Combine(directory, "inA"), "a.trx", "depA", "AAA");
            string secondPath = WriteReportWithAttachment(Path.Combine(directory, "inB"), "b.trx", "depB", "BBB");
            TrxArtifactPostProcessor processor = new();

            ProcessedArtifact? output = await processor.ProcessAsync(
                [
                    new InputArtifact(firstPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-1"),
                    new InputArtifact(secondPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-2"),
                ],
                directory,
                new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None),
                CancellationToken.None);

            Assert.IsNotNull(output);

            // Resolve the recorded deployment root exactly as a consumer of the merged TRX would: relative
            // to the merged report's own directory.
            string mergedDirectory = Path.GetDirectoryName(output.Path)!;
            string deploymentRoot = XDocument.Load(output.Path)
                .Descendants().First(e => e.Name.LocalName == "Deployment")
                .Attribute("runDeploymentRoot")!.Value;
            string resolvedRoot = Path.Combine(mergedDirectory, deploymentRoot);

            Assert.AreEqual("merged", Path.GetFileName(mergedDirectory));
            Assert.IsTrue(Directory.Exists(resolvedRoot));
            Assert.AreEqual("AAA", File.ReadAllText(Path.Combine(resolvedRoot, "In", "0", "machine", "log.txt")));
            Assert.AreEqual("BBB", File.ReadAllText(Path.Combine(resolvedRoot, "In", "1", "machine", "log.txt")));

            // Nothing of the merged report may be left in the results directory root, where a
            // non-recursive '*.trx' publish glob would pick it up alongside its own inputs.
            Assert.IsEmpty(Directory.GetFiles(directory, "*.trx"));
            Assert.IsFalse(Directory.Exists(Path.Combine(directory, deploymentRoot)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_WhenMergedPathIsAFile_DoesNotMerge()
    {
        string root = Path.Combine(Path.GetTempPath(), $"trx-post-processor-{Guid.NewGuid():N}");
        string resultsDirectory = Path.Combine(root, "results");
        Directory.CreateDirectory(resultsDirectory);
        File.WriteAllText(Path.Combine(resultsDirectory, "merged"), string.Empty);
        try
        {
            string firstPath = Path.Combine(resultsDirectory, "first.trx");
            string secondPath = Path.Combine(resultsDirectory, "second.trx");
            WriteMinimalReport(firstPath, "first");
            WriteMinimalReport(secondPath, "second");

            ProcessedArtifact? output = await new TrxArtifactPostProcessor().ProcessAsync(
                [
                    new InputArtifact(firstPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-1"),
                    new InputArtifact(secondPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-2"),
                ],
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
        // 'merged' is a fixed, predictable child name, and the merge confines its writes to the report's
        // own directory. A symlink/junction planted at that name would therefore become the confinement
        // base and redirect the report plus its attachment tree outside the supplied output directory,
        // so the processor must refuse rather than write through it.
        string root = Path.Combine(Path.GetTempPath(), $"trx-post-processor-{Guid.NewGuid():N}");
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

            string firstPath = Path.Combine(resultsDirectory, "first.trx");
            string secondPath = Path.Combine(resultsDirectory, "second.trx");
            WriteMinimalReport(firstPath, "first");
            WriteMinimalReport(secondPath, "second");
            TrxArtifactPostProcessor processor = new();

            ProcessedArtifact? output = await processor.ProcessAsync(
                [
                    new InputArtifact(firstPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-1"),
                    new InputArtifact(secondPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-2"),
                ],
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
        string root = Path.Combine(Path.GetTempPath(), $"trx-post-processor-{Guid.NewGuid():N}");
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

            string firstPath = Path.Combine(resultsDirectory, "first.trx");
            string secondPath = Path.Combine(resultsDirectory, "second.trx");
            WriteMinimalReport(firstPath, "first");
            WriteMinimalReport(secondPath, "second");

            ProcessedArtifact? output = await new TrxArtifactPostProcessor().ProcessAsync(
                [
                    new InputArtifact(firstPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-1"),
                    new InputArtifact(secondPath, TrxReportEngine.TrxArtifactKind, null, null, null, "execution-2"),
                ],
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

    [TestMethod]
    public void CreateMergeRunId_IsIndependentOfInputOrder()
    {
        string first = Path.Combine(Path.GetTempPath(), "a.trx");
        string second = Path.Combine(Path.GetTempPath(), "b.trx");

        Assert.AreEqual(
            TrxReportEngine.CreateMergeRunId([first, second]),
            TrxReportEngine.CreateMergeRunId([second, first]));
    }

    [TestMethod]
    public void CreateMergeRunId_WithDifferentExecutionId_ProducesDifferentId()
    {
        string path = Path.Combine(Path.GetTempPath(), "a.trx");

        Assert.AreNotEqual(
            TrxReportEngine.CreateMergeRunId([path], ["execution-1"]),
            TrxReportEngine.CreateMergeRunId([path], ["execution-2"]));
    }

    private static void WriteMinimalReport(string path, string name)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        new XDocument(
            new XElement(
                ns + "TestRun",
                new XAttribute("id", Guid.NewGuid()),
                new XAttribute("name", name),
                new XElement(
                    ns + "ResultSummary",
                    new XAttribute("outcome", "Completed"),
                    new XElement(ns + "Counters", new XAttribute("total", 0)))))
            .Save(path);
    }

    private static void WriteRetryReport(
        string path,
        string name,
        (string ExecutionId, string TestId, string TestName, string Outcome)[] testResults,
        bool includeRunInfo = false)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement[] results =
        [
            .. testResults.Select(testResult =>
            {
                var result = new XElement(
                    ns + "UnitTestResult",
                    new XAttribute("executionId", testResult.ExecutionId),
                    new XAttribute("testId", testResult.TestId),
                    new XAttribute("testName", testResult.TestName),
                    new XAttribute("outcome", testResult.Outcome == "Timeout" ? "Failed" : testResult.Outcome));
                if (testResult.TestId == "cleanup-id")
                {
                    result.Add(new XElement(ns + "Output", new XElement(ns + "ErrorInfo", new XElement(ns + "Message", "cleanup failure details"))));
                }

                return result;
            }),
        ];
        int passed = testResults.Count(result => result.Outcome == "Passed");
        int failed = testResults.Count(result => result.Outcome == "Failed");
        int timedOut = testResults.Count(result => result.Outcome == "Timeout");
        int notExecuted = testResults.Count(result => result.Outcome == "NotExecuted");
        XElement[] definitions =
        [
            .. testResults.GroupBy(result => result.TestId, StringComparer.Ordinal)
                .Select(group => new XElement(
                    ns + "UnitTest",
                    new XAttribute("id", group.Key),
                    new XAttribute("name", group.Key),
                    new XElement(ns + "Execution", new XAttribute("id", group.First().ExecutionId)))),
        ];
        XElement[] entries =
        [
            .. testResults.Select(result => new XElement(
                ns + "TestEntry",
                new XAttribute("testId", result.TestId),
                new XAttribute("executionId", result.ExecutionId))),
        ];
        var summary = new XElement(
            ns + "ResultSummary",
            new XAttribute("outcome", failed > 0 || timedOut > 0 ? "Failed" : "Completed"),
            new XElement(
                ns + "Counters",
                new XAttribute("total", testResults.Length),
                new XAttribute("executed", passed + failed),
                new XAttribute("passed", passed),
                new XAttribute("failed", failed),
                new XAttribute("timeout", timedOut),
                new XAttribute("notExecuted", notExecuted)));
        if (includeRunInfo)
        {
            summary.Add(new XElement(ns + "RunInfos", new XElement(ns + "RunInfo", new XAttribute("computerName", "machine"), new XAttribute("outcome", "Error"))));
        }

        new XDocument(
            new XElement(
                ns + "TestRun",
                new XAttribute("id", Guid.NewGuid()),
                new XAttribute("name", name),
                new XElement(ns + "Results", results),
                new XElement(ns + "TestDefinitions", definitions),
                new XElement(ns + "TestEntries", entries),
                new XElement(ns + "TestLists"),
                summary))
            .Save(path);
    }

    private static string WriteReportWithAttachment(string inputDirectory, string fileName, string deploymentRoot, string attachmentContent)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        Directory.CreateDirectory(inputDirectory);

        // Physical attachment under "<deploymentRoot>/In/machine/log.txt", referenced by the
        // machine-relative href "machine/log.txt" that the merge rewrites when it relocates the file.
        string attachmentDirectory = Path.Combine(inputDirectory, deploymentRoot, "In", "machine");
        Directory.CreateDirectory(attachmentDirectory);
        File.WriteAllText(Path.Combine(attachmentDirectory, "log.txt"), attachmentContent);

        string path = Path.Combine(inputDirectory, fileName);
        new XDocument(
            new XElement(
                ns + "TestRun",
                new XAttribute("id", Guid.NewGuid()),
                new XAttribute("name", fileName),
                new XElement(
                    ns + "TestSettings",
                    new XAttribute("name", "default"),
                    new XElement(ns + "Deployment", new XAttribute("runDeploymentRoot", deploymentRoot))),
                new XElement(
                    ns + "ResultSummary",
                    new XAttribute("outcome", "Completed"),
                    new XElement(ns + "Counters", new XAttribute("total", 0)),
                    new XElement(
                        ns + "CollectorDataEntries",
                        new XElement(
                            ns + "Collector",
                            new XAttribute("collectorDisplayName", "Code Coverage"),
                            new XElement(
                                ns + "UriAttachments",
                                new XElement(ns + "UriAttachment", new XElement(ns + "A", new XAttribute("href", "machine/log.txt")))))))))
            .Save(path);
        return path;
    }
}
