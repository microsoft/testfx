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

        Assert.AreSequenceEqual(new[] { TrxReportEngine.TrxArtifactKind }, processor.SupportedKinds);
        Assert.AreSequenceEqual(new[] { ".trx" }, processor.SupportedFileExtensionsFallback);
    }

    [TestMethod]
    public async Task ProcessAsync_WithFewerThanTwoInputs_ReturnsNull()
    {
        TrxArtifactPostProcessor processor = new();
        var input = new InputArtifact("input.trx", TrxReportEngine.TrxArtifactKind, null, null, null, null);

        Assert.IsNull(await processor.ProcessAsync([input], Path.GetTempPath(), CancellationToken.None));
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
                    new InputArtifact(firstPath, TrxReportEngine.TrxArtifactKind, null, null, null, null),
                    new InputArtifact(secondPath, TrxReportEngine.TrxArtifactKind, null, null, null, null),
                ],
                directory,
                CancellationToken.None);

            Assert.IsNotNull(output);
            Assert.AreEqual(TrxReportEngine.TrxArtifactKind, output.Kind);
            Assert.MatchesRegex(new Regex("^merged-[0-9a-f]{32}\\.trx$", RegexOptions.CultureInvariant), Path.GetFileName(output.Path));
            Assert.IsTrue(File.Exists(output.Path));
            Assert.AreEqual("TestRun", XDocument.Load(output.Path).Root!.Name.LocalName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void CreateMergeRunId_IsIndependentOfInputOrder()
    {
        string first = Path.Combine(Path.GetTempPath(), "a.trx");
        string second = Path.Combine(Path.GetTempPath(), "b.trx");

        Assert.AreEqual(
            TrxReportEngine.CreateMergeRunId([first, second]),
            TrxReportEngine.CreateMergeRunId([second, first]));
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
}
