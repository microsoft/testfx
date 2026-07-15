// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

using Microsoft.Testing.Extensions.CtrfReport;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CtrfReportMergerTests
{
    [TestMethod]
    public void Merge_WithNullReports_ThrowsArgumentNullException()
        => Assert.ThrowsExactly<ArgumentNullException>(() => CtrfReportMerger.Merge(null!));

    [TestMethod]
    public void Merge_WithNoReports_ThrowsArgumentException()
        => Assert.ThrowsExactly<ArgumentException>(() => CtrfReportMerger.Merge([]));

    [TestMethod]
    public void Merge_ConcatenatesTests()
    {
        string a = BuildReport(testEntries: [Test("TestA", "passed"), Test("TestB", "failed")]);
        string b = BuildReport(testEntries: [Test("TestC", "passed")]);

        JsonNode merged = JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!;

        var testArray = (JsonArray)merged["results"]!["tests"]!;
        Assert.HasCount(3, testArray);
        List<string?> names = [.. testArray.Select(t => (string?)t!["name"])];
        Assert.Contains("TestA", names);
        Assert.Contains("TestC", names);
    }

    [TestMethod]
    public void Merge_DerivesSummaryCountersFromTests()
    {
        string a = BuildReport(testEntries: [Test("a", "passed"), Test("b", "passed"), Test("c", "failed")]);
        string b = BuildReport(testEntries: [Test("d", "passed"), Test("e", "skipped"), Test("f", "skipped"), Test("g", "skipped")]);

        JsonNode summary = JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!["results"]!["summary"]!;

        Assert.AreEqual(7, (long)summary["tests"]!);
        Assert.AreEqual(3, (long)summary["passed"]!);
        Assert.AreEqual(1, (long)summary["failed"]!);
        Assert.AreEqual(3, (long)summary["skipped"]!);
    }

    [TestMethod]
    public void Merge_DerivesSummaryFromTests_WhenInputSummaryMissing()
    {
        // An input that carries tests[] but no summary object must still contribute to the merged counts.
        string withSummary = BuildReport(testEntries: [Test("a", "passed")]);
        string withoutSummary = BuildReportWithoutSummary(Test("b", "failed"), Test("c", "skipped"));

        JsonNode summary = JsonNode.Parse(CtrfReportMerger.Merge([withSummary, withoutSummary]))!["results"]!["summary"]!;

        Assert.AreEqual(3, (long)summary["tests"]!);
        Assert.AreEqual(1, (long)summary["passed"]!);
        Assert.AreEqual(1, (long)summary["failed"]!);
        Assert.AreEqual(1, (long)summary["skipped"]!);
    }

    [TestMethod]
    public void Merge_SummaryStartIsEarliestAndStopIsLatest()
    {
        string a = BuildReport(start: 2000, stop: 3000);
        string b = BuildReport(start: 1000, stop: 5000);

        JsonNode summary = JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!["results"]!["summary"]!;

        Assert.AreEqual(1000, (long)summary["start"]!);
        Assert.AreEqual(5000, (long)summary["stop"]!);
        Assert.AreEqual(4000, (long)summary["duration"]!);
    }

    [TestMethod]
    public void Merge_WhenInputsShareToolNameButDifferentVersion_UsesNeutralMergerToolIdentity()
    {
        // Same tool name but different version/metadata is still a distinct identity and must not be
        // stamped onto every merged test.
        string a = BuildReport(toolName: "MSTest", toolVersion: "1.0.0");
        string b = BuildReport(toolName: "MSTest", toolVersion: "2.0.0");

        string? toolName = (string?)JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!["results"]!["tool"]!["name"];

        Assert.Contains("merged", toolName!);
    }

    [TestMethod]
    public void Merge_WhenOneInputMissingTool_UsesNeutralMergerToolIdentity()
    {
        string a = BuildReport(toolName: "MSTest");
        string b = BuildReportWithoutTool();

        string? toolName = (string?)JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!["results"]!["tool"]!["name"];

        Assert.Contains("merged", toolName!);
    }

    [TestMethod]
    public void Merge_WhenAllInputsShareTool_KeepsThatTool()
    {
        string a = BuildReport(toolName: "MSTest");
        string b = BuildReport(toolName: "MSTest");

        JsonNode merged = JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!;

        Assert.AreEqual("CTRF", (string?)merged["reportFormat"]);
        Assert.AreEqual("MSTest", (string?)merged["results"]!["tool"]!["name"]);
    }

    [TestMethod]
    public void Merge_WhenInputsUseDifferentTools_UsesNeutralMergerToolIdentity()
    {
        // Merging modules produced by different frameworks must not misattribute one framework's
        // identity to another's tests, so a neutral merger identity is used instead of the first tool.
        string a = BuildReport(toolName: "MSTest");
        string b = BuildReport(toolName: "OtherFramework");

        string? toolName = (string?)JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!["results"]!["tool"]!["name"];

        Assert.AreNotEqual("MSTest", toolName);
        Assert.AreNotEqual("OtherFramework", toolName);
        Assert.Contains("merged", toolName!);
    }

    [TestMethod]
    public void Merge_DropsModuleSpecificEnvironmentExtraFields()
    {
        // testApplication/exitCode describe a single module and cannot describe all merged modules, so
        // they must not be carried over (misattributing the first module's app/exit code to everyone).
        string a = BuildReport();
        string b = BuildReport();

        JsonNode? environmentExtra = JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!["results"]!["environment"]!["extra"];

        Assert.IsNotNull(environmentExtra);
        Assert.IsNull(environmentExtra["testApplication"]);
        Assert.IsNull(environmentExtra["exitCode"]);
        // Shared, non-module-specific fields are retained.
        Assert.AreEqual("someone", (string?)environmentExtra["user"]);
    }

    [TestMethod]
    public void Merge_DerivesDeterministicReportIdNotReusingInput()
    {
        string a = BuildReport();
        string b = BuildReport();

        string? idA = (string?)JsonNode.Parse(a)!["reportId"];
        string? mergedId = (string?)JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!["reportId"];
        string? mergedIdAgain = (string?)JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!["reportId"];

        Assert.IsNotNull(mergedId);
        // Not one of the inputs' ids...
        Assert.AreNotEqual(idA, mergedId);
        // ...and deterministic: identical inputs reproduce the same id on every merge (RFC 018 idempotency).
        Assert.AreEqual(mergedId, mergedIdAgain);
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenOutputAliasesAnInput_ThrowsArgumentException()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ctrf-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string input = Path.Combine(tempDirectory, "a.json");
            File.WriteAllText(input, BuildReport());

            // Overwriting an input would destroy a read-only source; it must be rejected.
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => CtrfReportMerger.MergeToFileAsync([input], input, CancellationToken.None));

            Assert.IsTrue(File.Exists(input));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_WritesMergedFileToDisk()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ctrf-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string first = Path.Combine(tempDirectory, "a.json");
            string second = Path.Combine(tempDirectory, "b.json");
            string output = Path.Combine(tempDirectory, "nested", "merged.json");
            File.WriteAllText(first, BuildReport(testEntries: [Test("a", "passed"), Test("b", "passed")]));
            File.WriteAllText(second, BuildReport(testEntries: [Test("c", "passed"), Test("d", "passed"), Test("e", "passed")]));

            await CtrfReportMerger.MergeToFileAsync([first, second], output, CancellationToken.None);

            Assert.IsTrue(File.Exists(output));
            JsonNode merged = JsonNode.Parse(File.ReadAllText(output))!;
            Assert.AreEqual(5, (long)merged["results"]!["summary"]!["tests"]!);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

#if NETCOREAPP
    [TestMethod]
    public async Task MergeToFileAsync_WhenOutputAliasesInputViaSymlinkedParent_ThrowsAndPreservesInput()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ctrf-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string realDir = Path.Combine(tempDirectory, "real");
            Directory.CreateDirectory(realDir);
            string input = Path.Combine(realDir, "a.json");
            File.WriteAllText(input, BuildReport());

            string linkDir = Path.Combine(tempDirectory, "link");
            if (!TryCreateDirectorySymlink(linkDir, realDir))
            {
                return;
            }

            // Output goes through the symlinked parent, so it is the SAME physical file as the input.
            string aliasedOutput = Path.Combine(linkDir, "a.json");
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => CtrfReportMerger.MergeToFileAsync([input], aliasedOutput, CancellationToken.None));

            Assert.IsTrue(File.Exists(input));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool TryCreateDirectorySymlink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return Directory.Exists(linkPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }
#endif

    [TestMethod]
    public void Merge_WhenEnvironmentsDiffer_RetainsCommonFieldsAndDropsDiffering()
    {
        // Two inputs from different CI agents disagree on osPlatform but share user/machine. The merged
        // environment must drop the differing osPlatform, keep the common extra fields, and always drop
        // the module-specific testApplication/exitCode.
        string a = BuildReport(osPlatform: "linux");
        string b = BuildReport(osPlatform: "windows");

        JsonNode environment = JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!["results"]!["environment"]!;

        Assert.IsNull(environment["osPlatform"]);
        var extra = (JsonObject)environment["extra"]!;
        Assert.AreEqual("someone", (string?)extra["user"]);
        Assert.AreEqual("box", (string?)extra["machine"]);
        Assert.IsFalse(extra.ContainsKey("testApplication"));
        Assert.IsFalse(extra.ContainsKey("exitCode"));
    }

    [TestMethod]
    public void Merge_WhenEnvironmentsMatch_RetainsSharedFields()
    {
        string a = BuildReport(osPlatform: "linux");
        string b = BuildReport(osPlatform: "linux");

        JsonNode environment = JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!["results"]!["environment"]!;

        Assert.AreEqual("linux", (string?)environment["osPlatform"]);
    }

    private static JsonObject Test(string name, string status)
        => new()
        {
            ["name"] = name,
            ["status"] = status,
            ["duration"] = 1,
        };

    private static string BuildReport(
        long tests = 1,
        long passed = 1,
        long failed = 0,
        long skipped = 0,
        long pending = 0,
        long other = 0,
        long flaky = 0,
        long start = 1000,
        long stop = 2000,
        string toolName = "MSTest",
        string? toolVersion = null,
        string osPlatform = "test",
        IEnumerable<JsonObject>? testEntries = null)
    {
        var testArray = new JsonArray();
        foreach (JsonObject test in testEntries ?? [Test("DefaultTest", "passed")])
        {
            testArray.Add(test);
        }

        var toolObject = new JsonObject { ["name"] = toolName };
        if (toolVersion is not null)
        {
            toolObject["version"] = toolVersion;
        }

        var report = new JsonObject
        {
            ["reportFormat"] = "CTRF",
            ["specVersion"] = "0.0.0",
            ["reportId"] = Guid.NewGuid().ToString("D"),
            ["timestamp"] = DateTimeOffset.FromUnixTimeMilliseconds(stop).ToString("O", CultureInfo.InvariantCulture),
            ["generatedBy"] = "Microsoft.Testing.Extensions.CtrfReport",
            ["results"] = new JsonObject
            {
                ["tool"] = toolObject,
                ["summary"] = new JsonObject
                {
                    ["tests"] = tests,
                    ["passed"] = passed,
                    ["failed"] = failed,
                    ["skipped"] = skipped,
                    ["pending"] = pending,
                    ["other"] = other,
                    ["flaky"] = flaky,
                    ["start"] = start,
                    ["stop"] = stop,
                    ["duration"] = Math.Max(0, stop - start),
                },
                ["environment"] = new JsonObject
                {
                    ["osPlatform"] = osPlatform,
                    ["extra"] = new JsonObject
                    {
                        ["user"] = "someone",
                        ["machine"] = "box",
                        ["testApplication"] = "A.dll",
                        ["exitCode"] = 0,
                    },
                },
                ["tests"] = testArray,
            },
        };

        return report.ToJsonString();
    }

    [TestMethod]
    public void Merge_WhenAnInputHasNoEnvironment_DropsEnvironment()
    {
        // One input supplies no environment at all. Its OS/user/machine are unknown, so no field is shared
        // by every input and the merged report must not attribute the other input's environment to it.
        string withEnvironment = BuildReport(osPlatform: "linux");
        string withoutEnvironment = BuildReportWithoutSummary(Test("t", "passed"));

        JsonNode results = JsonNode.Parse(CtrfReportMerger.Merge([withEnvironment, withoutEnvironment]))!["results"]!;

        Assert.IsNull(results["environment"]);
    }

    private static string BuildReportWithoutSummary(params JsonObject[] testEntries)
    {
        var testArray = new JsonArray();
        foreach (JsonObject test in testEntries)
        {
            testArray.Add(test);
        }

        var report = new JsonObject
        {
            ["reportFormat"] = "CTRF",
            ["specVersion"] = "0.0.0",
            ["reportId"] = Guid.NewGuid().ToString("D"),
            ["results"] = new JsonObject
            {
                ["tool"] = new JsonObject { ["name"] = "MSTest" },
                ["tests"] = testArray,
            },
        };

        return report.ToJsonString();
    }

    [TestMethod]
    public void Merge_UsesTestLevelTimingWhenSummaryMissing()
    {
        // A summary-less input still carries per-test start/stop; those must feed the merged min/max
        // rather than being dropped (which would make the merged window fall back to the epoch).
        string withSummary = BuildReport(start: 5000, stop: 6000);
        string withoutSummary = BuildReportWithoutSummary(TimedTest("t", 1000, 9000));

        JsonNode summary = JsonNode.Parse(CtrfReportMerger.Merge([withSummary, withoutSummary]))!["results"]!["summary"]!;

        Assert.AreEqual(1000, (long)summary["start"]!);
        Assert.AreEqual(9000, (long)summary["stop"]!);
    }

    private static JsonObject TimedTest(string name, long start, long stop)
        => new()
        {
            ["name"] = name,
            ["status"] = "passed",
            ["duration"] = stop - start,
            ["start"] = start,
            ["stop"] = stop,
        };

    private static string BuildReportWithoutTool()
    {
        var report = new JsonObject
        {
            ["reportFormat"] = "CTRF",
            ["specVersion"] = "0.0.0",
            ["reportId"] = Guid.NewGuid().ToString("D"),
            ["results"] = new JsonObject
            {
                ["summary"] = new JsonObject { ["tests"] = 1, ["passed"] = 1, ["start"] = 1000, ["stop"] = 2000 },
                ["tests"] = new JsonArray(Test("t", "passed")),
            },
        };

        return report.ToJsonString();
    }
}
