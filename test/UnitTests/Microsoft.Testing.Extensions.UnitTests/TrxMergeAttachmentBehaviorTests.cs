// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.UnitTests.Helpers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxMergeAttachmentBehaviorTests
{
    private static readonly XNamespace Ns = TrxToolWorkspace.Ns;
    private static readonly Guid RunId = new("a01f7e38-0d97-418e-aeb6-cc7005972156");
    private const string RunName = "merged";
    private const string Deployment = "merged.a01f7e380d97418eaeb6cc7005972156";

    [TestMethod]
    [DataRow(false, DisplayName = "TrxMerge_MissingOwningDirectory")]
    [DataRow(true, DisplayName = "TrxMerge_EmptyOwningDirectory")]
    public async Task Merge_PerTestFilesWithoutOwningDirectoryResolveAtInRoot(bool emptyDirectory)
    {
        using var files = new TrxToolWorkspace();
        XDocument report = TrxToolWorkspace.Report("test", "Without directory");
        XElement result = TrxToolWorkspace.Result(report);
        if (emptyDirectory)
        {
            result.SetAttributeValue("relativeResultsDirectory", string.Empty);
        }

        result.Add(new XElement(Ns + "ResultFiles", ResultFile(@"logs\trace.txt"), ResultFile("missing.txt")));
        string source = files.Write(Path.Combine("input", "deploy", "In", "logs", "trace.txt"), "per-test bytes");
        string input = files.Save(Path.Combine("input", "input.trx"), report);
        byte[] original = File.ReadAllBytes(input);
        string output = files.FilePath(Path.Combine("output", "merged.trx"));

        await MergeAsync([input], output);

        var merged = XDocument.Load(output);
        XElement mergedResult = TrxToolWorkspace.Result(merged);
        Assert.AreEqual(emptyDirectory ? string.Empty : null, (string?)mergedResult.Attribute("relativeResultsDirectory"));
        Assert.AreEqual("0/logs/trace.txt", (string?)Assert.ContainsSingle(merged.Descendants(Ns + "ResultFile")).Attribute("path"));
        Assert.AreEqual("per-test bytes", File.ReadAllText(files.FilePath(Path.Combine("output", Deployment, "In", "0", "logs", "trace.txt"))));
        Assert.AreEqual("per-test bytes", File.ReadAllText(source));
        Assert.AreSequenceEqual(original, File.ReadAllBytes(input));
    }

    [TestMethod]
    public async Task Merge_PerTestMixedReferencesPrefixDirectoryExactlyOnce()
    {
        using var files = new TrxToolWorkspace();
        string absolute = files.Write(Path.Combine("outside", "absolute.log"), "already resolvable");
        XDocument report = TrxToolWorkspace.Report("test", "Mixed references");
        XElement result = TrxToolWorkspace.Result(report);
        result.SetAttributeValue("relativeResultsDirectory", @"execution\agent");
        result.Add(new XElement(
            Ns + "ResultFiles",
            ResultFile(@"logs\first.txt"), ResultFile("second.txt"), ResultFile(absolute),
            ResultFile("missing.txt"), ResultFile("../../../../escape.txt"), new XElement(Ns + "ResultFile"), ResultFile(string.Empty)));
        files.Write(Path.Combine("input", "deploy", "In", "execution", "agent", "logs", "first.txt"), "first");
        files.Write(Path.Combine("input", "deploy", "In", "execution", "agent", "second.txt"), "second");
        string input = files.Save(Path.Combine("input", "input.trx"), report);
        byte[] original = File.ReadAllBytes(input);
        string output = files.FilePath(Path.Combine("output", "merged.trx"));

        await MergeAsync([input], output);

        var merged = XDocument.Load(output);
        Assert.AreEqual("0/execution/agent", (string?)TrxToolWorkspace.Result(merged).Attribute("relativeResultsDirectory"));
        Assert.AreSequenceEqual(
            ["logs/first.txt", "second.txt", absolute],
            merged.Descendants(Ns + "ResultFile").Select(value => (string?)value.Attribute("path")));
        Assert.AreEqual("first", File.ReadAllText(files.FilePath(Path.Combine("output", Deployment, "In", "0", "execution", "agent", "logs", "first.txt"))));
        Assert.AreEqual("second", File.ReadAllText(files.FilePath(Path.Combine("output", Deployment, "In", "0", "execution", "agent", "second.txt"))));
        Assert.AreEqual("already resolvable", File.ReadAllText(absolute));
        Assert.AreSequenceEqual(original, File.ReadAllBytes(input));
        Assert.HasCount(2, Directory.GetFiles(files.FilePath(Path.Combine("output", Deployment)), "*.txt", SearchOption.AllDirectories));
    }

    [TestMethod]
    [DataRow("no-settings")]
    [DataRow("empty-deployment")]
    [DataRow("missing-directory")]
    [DataRow("traversal")]
    [DataRow("rooted-deployment")]
    public async Task Merge_MissingSourceDropsOnlyRelativeReferencesAndTheirRequiredOwners(string sourceShape)
    {
        using var files = new TrxToolWorkspace();
        string absolute = files.Write(Path.Combine("external", "absolute.log"), "absolute evidence");
        string absoluteDirectory = files.FilePath("external");
        files.Write(Path.Combine("external", "owned.log"), "rooted directory evidence");
        string? deployment = sourceShape switch
        {
            "no-settings" => null,
            "empty-deployment" => string.Empty,
            "traversal" => "../external",
            "rooted-deployment" => absoluteDirectory,
            _ => "absent",
        };
        XDocument report = TrxToolWorkspace.Report("test", "Missing deployment", deployment);
        TrxToolWorkspace.Summary(report).Add(TrxToolWorkspace.Collector(
            new XElement(Ns + "A", new XAttribute("href", "relative.log")),
            new XElement(Ns + "A", new XAttribute("href", absolute))));
        TrxToolWorkspace.Summary(report).Add(new XElement(Ns + "ResultFiles", ResultFile("run-relative.log"), ResultFile(absolute)));
        TrxToolWorkspace.Result(report).SetAttributeValue("relativeResultsDirectory", "execution");
        TrxToolWorkspace.Result(report).Add(new XElement(Ns + "ResultFiles", ResultFile("test-relative.log"), ResultFile(absolute)));
        report.Root!.Element(Ns + "Results")!.Add(new XElement(
            Ns + "UnitTestResult",
            new XAttribute("testId", "rooted-owner"), new XAttribute("relativeResultsDirectory", absoluteDirectory),
            new XElement(Ns + "ResultFiles", ResultFile("owned.log"))));
        string input = files.Save(Path.Combine("input", "input.trx"), report);
        byte[] original = File.ReadAllBytes(input);
        string output = files.FilePath(Path.Combine("output", "merged.trx"));

        await MergeAsync([input], output);

        var merged = XDocument.Load(output);
        Assert.AreEqual(absolute, (string?)Assert.ContainsSingle(merged.Descendants(Ns + "A")).Attribute("href"));
        Assert.HasCount(1, merged.Descendants(Ns + "UriAttachment"), "A removed collector reference must take its schema-required UriAttachment owner with it.");
        Assert.AreSequenceEqual(
            [absolute, "owned.log", absolute],
            merged.Descendants(Ns + "ResultFile").Select(value => (string?)value.Attribute("path")));
        Assert.AreEqual(absoluteDirectory, (string?)merged.Descendants(Ns + "UnitTestResult").Last().Attribute("relativeResultsDirectory"));
        Assert.AreSequenceEqual(original, File.ReadAllBytes(input));
        Assert.IsEmpty(Directory.GetFiles(files.FilePath("output"), "*.log", SearchOption.AllDirectories));
        Assert.AreEqual("absolute evidence", File.ReadAllText(absolute));
    }

    [TestMethod]
    public async Task Merge_EmptyReferenceAttributesArePreservedWithoutInventedFiles()
    {
        using var files = new TrxToolWorkspace();
        XDocument report = TrxToolWorkspace.Report("test", "Empty references");
        TrxToolWorkspace.Summary(report).Add(TrxToolWorkspace.Collector(
            new XElement(Ns + "A"), new XElement(Ns + "A", new XAttribute("href", string.Empty)),
            new XElement(Ns + "A", new XAttribute("href", "log.txt"))));
        TrxToolWorkspace.Result(report).Add(new XElement(
            Ns + "ResultFiles",
            new XElement(Ns + "ResultFile"), ResultFile(string.Empty), ResultFile("log.txt")));
        files.Write(Path.Combine("input", "deploy", "In", "log.txt"), "only materialized file");
        string input = files.Save(Path.Combine("input", "input.trx"), report);
        string output = files.FilePath(Path.Combine("output", "merged.trx"));

        await MergeAsync([input], output);

        var merged = XDocument.Load(output);

        Assert.AreSequenceEqual(
            [null, string.Empty, "0/log.txt"],
            merged.Descendants(Ns + "A").Select(value => (string?)value.Attribute("href")));

        Assert.AreSequenceEqual(
            [null, string.Empty, "0/log.txt"],
            merged.Descendants(Ns + "ResultFile").Select(value => (string?)value.Attribute("path")));
        string copied = Assert.ContainsSingle(Directory.GetFiles(files.FilePath(Path.Combine("output", Deployment)), "*", SearchOption.AllDirectories));
        Assert.AreEqual(files.FilePath(Path.Combine("output", Deployment, "In", "0", "log.txt")), copied);
        Assert.AreEqual("only materialized file", File.ReadAllText(copied));
    }

    [TestMethod]
    public async Task Merge_ExistingDestinationIsReplacedWithoutMutatingSource()
    {
        using var files = new TrxToolWorkspace();
        string source = files.Write(Path.Combine("input", "deploy", "In", "log.txt"), "new evidence");
        string destination = files.Write(Path.Combine("output", Deployment, "In", "0", "log.txt"), "old evidence");
        XDocument report = ReportWithCollector("log.txt");
        string input = files.Save(Path.Combine("input", "input.trx"), report);
        byte[] original = File.ReadAllBytes(input);
        string output = files.FilePath(Path.Combine("output", "merged.trx"));

        await MergeAsync([input], output);

        Assert.AreEqual("new evidence", File.ReadAllText(destination));
        Assert.AreEqual("new evidence", File.ReadAllText(source));
        Assert.AreEqual("0/log.txt", (string?)Assert.ContainsSingle(XDocument.Load(output).Descendants(Ns + "A")).Attribute("href"));
        Assert.AreSequenceEqual(original, File.ReadAllBytes(input));
        Assert.HasCount(1, Directory.GetFiles(Path.GetDirectoryName(destination)!));
    }

    [TestMethod]
    [DataRow("collector")]
    [DataRow("per-test")]
    public async Task Merge_EqualRootsValidateReferencesWithoutPrefixOrCopy(string shape)
    {
        using var files = new TrxToolWorkspace();
        XDocument report = TrxToolWorkspace.Report("test", "Equal deployment", Deployment);
        string relative = shape == "collector" ? "log.txt" : "execution/log.txt";
        string source = files.Write(Path.Combine(Deployment, "In", relative.Replace('/', Path.DirectorySeparatorChar)), "already in merged root");
        if (shape == "collector")
        {
            TrxToolWorkspace.Summary(report).Add(TrxToolWorkspace.Collector(
                new XElement(Ns + "A", new XAttribute("href", "log.txt")),
                new XElement(Ns + "A", new XAttribute("href", "absent.txt"))));
        }
        else
        {
            TrxToolWorkspace.Result(report).SetAttributeValue("relativeResultsDirectory", "execution");
            TrxToolWorkspace.Result(report).Add(new XElement(Ns + "ResultFiles", ResultFile("log.txt"), ResultFile("absent.txt")));
        }

        string input = files.Save("input.trx", report);
        byte[] original = File.ReadAllBytes(input);
        string output = files.FilePath("merged.trx");

        await MergeAsync([input], output);

        var merged = XDocument.Load(output);
        Assert.AreEqual(Deployment, TrxToolWorkspace.Deployment(merged));
        if (shape == "collector")
        {
            Assert.AreEqual("log.txt", (string?)Assert.ContainsSingle(merged.Descendants(Ns + "A")).Attribute("href"));
        }
        else
        {
            Assert.AreEqual("execution", (string?)TrxToolWorkspace.Result(merged).Attribute("relativeResultsDirectory"));
            Assert.AreEqual("log.txt", (string?)Assert.ContainsSingle(merged.Descendants(Ns + "ResultFile")).Attribute("path"));
        }

        Assert.AreEqual(source, Assert.ContainsSingle(Directory.GetFiles(files.FilePath(Deployment), "*", SearchOption.AllDirectories)));
        Assert.AreEqual("already in merged root", File.ReadAllText(source));
        Assert.AreSequenceEqual(original, File.ReadAllBytes(input));
        Assert.IsFalse(Directory.Exists(files.FilePath(Path.Combine(Deployment, "In", "0"))));
    }

    [TestMethod]
    public async Task Merge_StandaloneResultFilesUseInRootAndDropOnlyUnmaterializedReferences()
    {
        using var files = new TrxToolWorkspace();
        XDocument report = TrxToolWorkspace.Report("test", "Foreign run-level files");
        TrxToolWorkspace.Summary(report).Add(new XElement(Ns + "ResultFiles", ResultFile(@"agent\run.log"), ResultFile("absent.log")));
        // A foreign producer can omit UriAttachment. Dropping its A removes only the leaf, not
        // the surrounding UriAttachments collection.
        TrxToolWorkspace.Summary(report).Add(new XElement(
            Ns + "CollectorDataEntries",
            new XElement(Ns + "Collector", new XElement(
                Ns + "UriAttachments",
                new XElement(Ns + "A", new XAttribute("href", "absent-collector.log"))))));
        files.Write(Path.Combine("input", "deploy", "In", "agent", "run.log"), "run-level evidence");
        string input = files.Save(Path.Combine("input", "input.trx"), report);
        string output = files.FilePath(Path.Combine("output", "merged.trx"));

        await MergeAsync([input], output);

        var merged = XDocument.Load(output);
        Assert.AreEqual("0/agent/run.log", (string?)Assert.ContainsSingle(merged.Descendants(Ns + "ResultFile")).Attribute("path"));
        Assert.AreEqual("run-level evidence", File.ReadAllText(files.FilePath(Path.Combine("output", Deployment, "In", "0", "agent", "run.log"))));
        Assert.IsEmpty(merged.Descendants(Ns + "A"));
        Assert.IsFalse(Assert.ContainsSingle(merged.Descendants(Ns + "UriAttachments")).HasElements);
    }

    [TestMethod]
    [DataRow("../escape.log", DisplayName = "TrxMerge_ForwardSlashTraversal")]
    [DataRow(@"..\escape.log", DisplayName = "TrxMerge_BackslashTraversal")]
    [DataRow("nested/../../escape.log", DisplayName = "TrxMerge_NestedTraversal")]
    [DataRow("absent.log", DisplayName = "TrxMerge_MissingReferencedFile")]
    public async Task Merge_TraversalAndMissingReferencesCannotEscapeOrDangle(string rejected)
    {
        using var files = new TrxToolWorkspace();
        string external = files.Write(Path.Combine("input", "deploy", "escape.log"), "must remain untouched");
        files.Write(Path.Combine("input", "deploy", "In", "log.txt"), "confined");
        XDocument report = ReportWithCollector(rejected, "nested/../log.txt");
        string input = files.Save(Path.Combine("input", "input.trx"), report);
        string output = files.FilePath(Path.Combine("output", "merged.trx"));

        await MergeAsync([input], output);

        var merged = XDocument.Load(output);
        Assert.AreEqual("0/nested/../log.txt", (string?)Assert.ContainsSingle(merged.Descendants(Ns + "A")).Attribute("href"));
        Assert.HasCount(1, merged.Descendants(Ns + "UriAttachment"));
        Assert.AreEqual("confined", File.ReadAllText(files.FilePath(Path.Combine("output", Deployment, "In", "0", "log.txt"))));
        Assert.AreEqual("must remain untouched", File.ReadAllText(external));
        Assert.IsEmpty(Directory.GetFiles(files.FilePath("output"), "escape.log", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task Merge_CancellationNeverCommitsPartialOutput()
    {
        using var files = new TrxToolWorkspace();
        string input = files.Save(Path.Combine("input", "input.trx"), ReportWithCollector("log.txt"));
        string source = files.Write(Path.Combine("input", "deploy", "In", "log.txt"), "source evidence");
        byte[] original = File.ReadAllBytes(input);
        string output = files.FilePath(Path.Combine("not-created", "merged.trx"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => TrxReportEngine.MergeToFileAsync([input], output, RunId, RunName, cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsFalse(Directory.Exists(files.FilePath("not-created")));
        Assert.AreSequenceEqual(original, File.ReadAllBytes(input));
        Assert.AreEqual("source evidence", File.ReadAllText(source));
    }

    [TestMethod]
    public async Task Merge_AttachmentCopyFailureDropsAllRelativeReferencesForOnlyThatInput()
    {
        using var files = new TrxToolWorkspace();
        string absolute = files.Write("absolute.log", "independent absolute evidence");
        XDocument firstReport = ReportWithCollector("good.log", "blocked/log.log", absolute);
        TrxToolWorkspace.Result(firstReport).Add(new XElement(Ns + "ResultFiles", ResultFile("not-yet-visited.log")));
        string first = files.Save(Path.Combine("first", "input.trx"), firstReport);
        files.Write(Path.Combine("first", "deploy", "In", "good.log"), "copied before failure");
        files.Write(Path.Combine("first", "deploy", "In", "blocked", "log.log"), "blocked source");
        files.Write(Path.Combine("first", "deploy", "In", "not-yet-visited.log"), "must not dangle");
        XDocument secondReport = ReportWithCollector("good.log");
        string second = files.Save(Path.Combine("second", "input.trx"), secondReport);
        files.Write(Path.Combine("second", "deploy", "In", "good.log"), "healthy second input");
        string obstacle = files.Write(Path.Combine("output", Deployment, "In", "0", "blocked"), "a regular file, not a directory");
        byte[] firstBytes = File.ReadAllBytes(first);
        byte[] secondBytes = File.ReadAllBytes(second);
        string output = files.FilePath(Path.Combine("output", "merged.trx"));

        await MergeAsync([first, second], output);

        var merged = XDocument.Load(output);
        Assert.AreSequenceEqual([absolute, "1/good.log"], merged.Descendants(Ns + "A").Select(value => (string?)value.Attribute("href")));
        Assert.HasCount(2, merged.Descendants(Ns + "UriAttachment"));
        Assert.IsEmpty(merged.Descendants(Ns + "ResultFile"));
        Assert.AreEqual("2", (string?)merged.Descendants(Ns + "Counters").Single().Attribute("total"));
        Assert.AreEqual("healthy second input", File.ReadAllText(files.FilePath(Path.Combine("output", Deployment, "In", "1", "good.log"))));
        Assert.AreEqual("a regular file, not a directory", File.ReadAllText(obstacle));
        Assert.AreEqual("independent absolute evidence", File.ReadAllText(absolute));
        Assert.AreSequenceEqual(firstBytes, File.ReadAllBytes(first));
        Assert.AreSequenceEqual(secondBytes, File.ReadAllBytes(second));
    }

    [TestMethod]
    public async Task Merge_RootedOwningDirectoryKeepsResultFilesAbsoluteAndUncopied()
    {
        using var files = new TrxToolWorkspace();
        string first = files.Write(Path.Combine("external", "first.log"), "rooted owner evidence");
        string second = files.Write("second.log", "absolute leaf evidence");
        XDocument report = TrxToolWorkspace.Report("test", "Rooted owner");
        TrxToolWorkspace.Result(report).SetAttributeValue("relativeResultsDirectory", Path.GetDirectoryName(first)!);
        TrxToolWorkspace.Result(report).Add(new XElement(Ns + "ResultFiles", ResultFile("first.log"), ResultFile(second)));
        Directory.CreateDirectory(files.FilePath(Path.Combine("input", "deploy", "In")));
        string input = files.Save(Path.Combine("input", "input.trx"), report);
        byte[] original = File.ReadAllBytes(input);
        string output = files.FilePath(Path.Combine("output", "merged.trx"));

        await MergeAsync([input], output);

        var merged = XDocument.Load(output);
        Assert.AreEqual(Path.GetDirectoryName(first), (string?)TrxToolWorkspace.Result(merged).Attribute("relativeResultsDirectory"));
        Assert.AreSequenceEqual(["first.log", second], merged.Descendants(Ns + "ResultFile").Select(value => (string?)value.Attribute("path")));
        Assert.AreEqual("rooted owner evidence", File.ReadAllText(first));
        Assert.AreEqual("absolute leaf evidence", File.ReadAllText(second));
        Assert.IsFalse(Directory.Exists(files.FilePath(Path.Combine("output", Deployment, "In"))));
        Assert.AreSequenceEqual(original, File.ReadAllBytes(input));
    }

#if NETCOREAPP
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    [DataRow("in-root")]
    [DataRow("file")]
    [DataRow("parent")]
    public async Task Merge_LinuxSourceLinksAreDroppedWithoutReadingExternalTargets(string shape)
    {
        using var files = new TrxToolWorkspace();
        string external = files.Write(Path.Combine("external", "log.txt"), "outside deployment");
        string inRoot = files.FilePath(Path.Combine("input", "deploy", "In"));
        Directory.CreateDirectory(Path.GetDirectoryName(inRoot)!);
        string reference = shape == "parent" ? "linked/log.txt" : "log.txt";
        if (shape == "in-root")
        {
            Directory.CreateSymbolicLink(inRoot, files.FilePath("external"));
        }
        else
        {
            Directory.CreateDirectory(inRoot);
            if (shape == "file")
            {
                File.CreateSymbolicLink(Path.Combine(inRoot, "log.txt"), external);
            }
            else
            {
                Directory.CreateSymbolicLink(Path.Combine(inRoot, "linked"), files.FilePath("external"));
            }
        }

        string input = files.Save(Path.Combine("input", "input.trx"), ReportWithCollector(reference));
        byte[] original = File.ReadAllBytes(input);
        string output = files.FilePath(Path.Combine("output", "merged.trx"));

        await MergeAsync([input], output);

        var merged = XDocument.Load(output);
        Assert.IsEmpty(merged.Descendants(Ns + "A"));
        Assert.IsEmpty(merged.Descendants(Ns + "UriAttachment"));
        Assert.AreEqual("outside deployment", File.ReadAllText(external));
        Assert.IsEmpty(Directory.GetFiles(files.FilePath("output"), "*.txt", SearchOption.AllDirectories));
        Assert.AreSequenceEqual(original, File.ReadAllBytes(input));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task Merge_LinuxUnsafeMergedRootDropsRelativeReferences()
    {
        using var files = new TrxToolWorkspace();
        string external = files.Write(Path.Combine("external", "sentinel.txt"), "external tree");
        string source = files.Write(Path.Combine("input", "deploy", "In", "log.txt"), "input tree");
        string outputDirectory = files.FilePath("output");
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateSymbolicLink(Path.Combine(outputDirectory, Deployment), files.FilePath("external"));
        string input = files.Save(Path.Combine("input", "input.trx"), ReportWithCollector("log.txt", external));
        string output = Path.Combine(outputDirectory, "merged.trx");

        await MergeAsync([input], output);

        Assert.AreEqual(external, (string?)Assert.ContainsSingle(XDocument.Load(output).Descendants(Ns + "A")).Attribute("href"));
        Assert.AreEqual("input tree", File.ReadAllText(source));
        Assert.AreEqual("external tree", File.ReadAllText(external));
        Assert.AreEqual(external, Assert.ContainsSingle(Directory.GetFiles(files.FilePath("external"), "*", SearchOption.AllDirectories)));
        Assert.IsNotNull(new DirectoryInfo(Path.Combine(outputDirectory, Deployment)).LinkTarget);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task Merge_LinuxInRootLinkIsRemovedBeforeCopy()
    {
        using var files = new TrxToolWorkspace();
        string external = files.Write(Path.Combine("external", "sentinel.txt"), "external tree");
        files.Write(Path.Combine("input", "deploy", "In", "log.txt"), "safe source");
        string mergedRoot = files.FilePath(Path.Combine("output", Deployment));
        Directory.CreateDirectory(mergedRoot);
        string inRoot = Path.Combine(mergedRoot, "In");
        Directory.CreateSymbolicLink(inRoot, files.FilePath("external"));
        string input = files.Save(Path.Combine("input", "input.trx"), ReportWithCollector("log.txt"));
        string output = files.FilePath(Path.Combine("output", "merged.trx"));

        await MergeAsync([input], output);

        Assert.IsNull(new DirectoryInfo(inRoot).LinkTarget);
        Assert.AreEqual("safe source", File.ReadAllText(Path.Combine(inRoot, "0", "log.txt")));
        Assert.AreEqual("0/log.txt", (string?)Assert.ContainsSingle(XDocument.Load(output).Descendants(Ns + "A")).Attribute("href"));
        Assert.AreEqual("external tree", File.ReadAllText(external));
        Assert.AreEqual(external, Assert.ContainsSingle(Directory.GetFiles(files.FilePath("external"), "*", SearchOption.AllDirectories)));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task Merge_LinuxDestinationLinkIsNeverTraversed()
    {
        using var files = new TrxToolWorkspace();
        string external = files.Write(Path.Combine("external", "log.txt"), "must not overwrite");
        files.Write(Path.Combine("input", "deploy", "In", "nested", "log.txt"), "new source");
        string destinationParent = files.FilePath(Path.Combine("output", Deployment, "In", "0"));
        Directory.CreateDirectory(destinationParent);
        Directory.CreateSymbolicLink(Path.Combine(destinationParent, "nested"), files.FilePath("external"));
        string input = files.Save(Path.Combine("input", "input.trx"), ReportWithCollector("nested/log.txt"));
        string output = files.FilePath(Path.Combine("output", "merged.trx"));

        await MergeAsync([input], output);

        Assert.IsEmpty(XDocument.Load(output).Descendants(Ns + "A"));
        Assert.AreEqual("must not overwrite", File.ReadAllText(external));
        Assert.IsNotNull(new DirectoryInfo(Path.Combine(destinationParent, "nested")).LinkTarget);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    [DataRow(false, DisplayName = "TrxMerge_ExistingDestinationLink")]
    [DataRow(true, DisplayName = "TrxMerge_DanglingDestinationLink")]
    public async Task Merge_LinuxDestinationFileLinkIsReplacedWithoutFollowingIt(bool dangling)
    {
        using var files = new TrxToolWorkspace();
        string external = dangling ? files.FilePath("not-created.txt") : files.Write("external.txt", "must not overwrite");
        files.Write(Path.Combine("input", "deploy", "In", "log.txt"), "fresh source");
        string destination = files.FilePath(Path.Combine("output", Deployment, "In", "0", "log.txt"));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.CreateSymbolicLink(destination, external);
        string input = files.Save(Path.Combine("input", "input.trx"), ReportWithCollector("log.txt"));
        string output = files.FilePath(Path.Combine("output", "merged.trx"));

        await MergeAsync([input], output);

        Assert.IsNull(new FileInfo(destination).LinkTarget);
        Assert.AreEqual("fresh source", File.ReadAllText(destination));
        Assert.AreEqual("0/log.txt", (string?)Assert.ContainsSingle(XDocument.Load(output).Descendants(Ns + "A")).Attribute("href"));
        if (dangling)
        {
            Assert.IsFalse(File.Exists(external));
        }
        else
        {
            Assert.AreEqual("must not overwrite", File.ReadAllText(external));
        }
    }
#endif

    private static Task MergeAsync(string[] inputs, string output)
        => TrxReportEngine.MergeToFileAsync(inputs, output, RunId, RunName, CancellationToken.None);

    private static XElement ResultFile(string path) => new(Ns + "ResultFile", new XAttribute("path", path));

    private static XDocument ReportWithCollector(params string[] references)
    {
        XDocument report = TrxToolWorkspace.Report("test", "With collector");
        TrxToolWorkspace.Summary(report).Add(TrxToolWorkspace.Collector(
            [.. references.Select(value => new XElement(Ns + "A", new XAttribute("href", value)))]));
        return report;
    }
}
