// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

/// <summary>
/// Isolated local files for the TRX tool's static filesystem boundary. Every using scope compiles
/// to finally cleanup; no ambient directory, environment, ACL, process or service is changed.
/// </summary>
internal sealed class TrxToolWorkspace : IDisposable
{
    public static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public TrxToolWorkspace() => Directory.CreateDirectory(Root);

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "trxcase-" + Guid.NewGuid().ToString("N"));

    public string FilePath(string relative) => Path.Combine(Root, relative);

    public string Save(string relative, XDocument report)
    {
        string path = FilePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        report.Save(path);
        return path;
    }

    public string Write(string relative, string text)
    {
        string path = FilePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    public static XDocument Report(string id, string testName, string? deploymentRoot = "deploy", bool failed = false)
        => new(new XElement(
            Ns + "TestRun",
            new XAttribute("id", "41fcf6f7-124a-48a5-b76f-706c945318c5"),
            new XAttribute("name", "input run"),
            new XElement(Ns + "Times", new XAttribute("start", "2026-10-30T10:00:00+00:00"), new XAttribute("finish", "2026-10-30T10:00:03+00:00")),
            deploymentRoot is null ? null : new XElement(
                Ns + "TestSettings",
                new XElement(Ns + "Deployment", new XAttribute("runDeploymentRoot", deploymentRoot))),
            new XElement(
                Ns + "Results",
                new XElement(Ns + "UnitTestResult", new XAttribute("testId", id),
                    new XAttribute("executionId", "execution-" + id), new XAttribute("testName", testName),
                    new XAttribute("outcome", failed ? "Failed" : "Passed"))),
            new XElement(
                Ns + "TestDefinitions",
                new XElement(Ns + "UnitTest", new XAttribute("id", id), new XAttribute("name", testName),
                    new XAttribute("storage", "suite.dll"), new XElement(Ns + "TestMethod", new XAttribute("className", "Suite"), new XAttribute("name", testName)))),
            new XElement(Ns + "TestEntries", new XElement(Ns + "TestEntry", new XAttribute("testId", id), new XAttribute("executionId", "execution-" + id))),
            new XElement(Ns + "TestLists", new XElement(Ns + "TestList", new XAttribute("id", "shared-list"), new XAttribute("name", "All tests"))),
            new XElement(Ns + "ResultSummary", new XAttribute("outcome", failed ? "Failed" : "Completed"),
                new XElement(Ns + "Counters", new XAttribute("total", 1), new XAttribute("executed", 1),
                    new XAttribute("passed", failed ? 0 : 1), new XAttribute("failed", failed ? 1 : 0)))));

    public static XElement Collector(params XElement[] references)
        => new(Ns + "CollectorDataEntries", new XElement(
            Ns + "Collector",
            new XAttribute("uri", "datacollector://fixture/1.0"),
            new XElement(Ns + "UriAttachments", references.Select(reference => new XElement(Ns + "UriAttachment", reference)))));

    public static XElement Result(XDocument document) => document.Descendants(Ns + "UnitTestResult").Single();

    public static XElement Summary(XDocument document) => document.Descendants(Ns + "ResultSummary").Single();

    public static string Deployment(XDocument document)
        => document.Descendants(Ns + "Deployment").Single().Attribute("runDeploymentRoot")!.Value;
}
