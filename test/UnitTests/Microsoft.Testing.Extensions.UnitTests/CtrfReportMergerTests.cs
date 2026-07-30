// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
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
    public async Task MergeToFileAsync_WithNoInputs_ThrowsWithoutCreatingOutputDirectory()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ctrf-merge-{Guid.NewGuid():N}");
        try
        {
            string output = Path.Combine(tempDirectory, "out", "merged.json");
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => CtrfReportMerger.MergeToFileAsync([], output, CancellationToken.None));

            Assert.IsFalse(Directory.Exists(tempDirectory));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

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
    public void Merge_ReportIdIsUnaffectedByIgnoredNonCtrfInput()
    {
        string a = BuildReport(testEntries: [Test("a", "passed")]);
        string b = BuildReport(testEntries: [Test("b", "failed")]);

        // A non-CTRF input (missing the reportFormat discriminator) is skipped by the merge, so it must not
        // participate in the deterministic reportId — the id must match the CTRF-only merge exactly.
        string nonCtrf = "{\"results\":{\"summary\":{},\"tests\":[]}}";

        string? ctrfOnlyId = (string?)JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!["reportId"];
        string? withNoiseId = (string?)JsonNode.Parse(CtrfReportMerger.Merge([a, nonCtrf, b]))!["reportId"];

        Assert.IsNotNull(ctrfOnlyId);
        Assert.AreEqual(ctrfOnlyId, withNoiseId);
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
    public void BuildCaseFoldedProbePath_LowerCasesOnlyFileName_PreservingCaseSensitiveDirectory()
    {
        // Regression seam for the case-sensitivity probe. The probe compares the created file against a
        // case-folded candidate to decide whether the directory is case-sensitive. The bug lower-cased the
        // WHOLE combined path, corrupting the directory portion: a case-insensitive child directory beneath a
        // case-sensitive, differently-cased ancestor was then probed at a non-existent lowercased ancestor, so
        // File.Exists returned false and the directory was misreported as case-sensitive. That in turn made
        // EnsureOutputDoesNotAliasInput compare paths ordinally and miss that 'a.trx' and 'A.trx' are the same
        // file, risking overwrite of a read-only input. Assert directly that only the generated file name is
        // case-folded while the (potentially case-sensitive, uppercased) directory path is preserved verbatim.
        // Reverting the production fix to lower-case the whole path fails this test on every platform.
        // The helper is an internal type linked into several extension assemblies, so it is reached via the
        // unambiguous CtrfReport assembly (a simple-name reference would be ambiguous across those copies).
        MethodInfo buildProbe = typeof(CtrfReportMerger).Assembly
            .GetType("Microsoft.Testing.Extensions.MergeOutputFileHelper", throwOnError: true)!
            .GetMethod("BuildCaseFoldedProbePath", BindingFlags.Static | BindingFlags.NonPublic)!;

        string directory = Path.Combine("SomeCaseSensitive", "PARENT", "Child");
        const string probeFileName = "CASESENSITIVEPROBEabc123";

        string candidate = (string)buildProbe.Invoke(null, [directory, probeFileName])!;

        Assert.AreEqual(Path.Combine(directory, "casesensitiveprobeabc123"), candidate);
        Assert.AreEqual(directory, Path.GetDirectoryName(candidate));
        Assert.AreEqual("casesensitiveprobeabc123", Path.GetFileName(candidate));
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenOutputAliasesInputByCaseOnly_IsRejectedOnCaseInsensitiveFilesystem()
    {
        // End-to-end sibling of the seam test above, scoped to a single scenario: on a case-insensitive
        // directory (Windows/macOS temp dirs are), an output that differs from an input only by CASE aliases
        // that input and must be rejected so a read-only source is never overwritten. Skipped on a genuinely
        // case-sensitive host, where the two names are distinct (that scenario is covered by the sibling test).
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ctrf-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            if (!IsDirectoryCaseInsensitive(tempDirectory))
            {
                Assert.Inconclusive("Host temp filesystem is case-sensitive; covered by the case-sensitive sibling test.");
            }

            string input = Path.Combine(tempDirectory, "report.json");
            File.WriteAllText(input, BuildReport());

            string casedOutput = Path.Combine(tempDirectory, "REPORT.json");
            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => CtrfReportMerger.MergeToFileAsync([input], casedOutput, CancellationToken.None));
            Assert.IsTrue(File.Exists(input));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeToFileAsync_WhenOutputDiffersByCaseOnly_IsAllowedOnCaseSensitiveFilesystem()
    {
        // Complementary scenario to the case-insensitive test above: on a genuinely case-sensitive directory,
        // an output that differs from an input only by CASE is a distinct file, so the merge is allowed and the
        // input is preserved. Skipped on a case-insensitive host (that scenario is the sibling test).
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ctrf-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            if (IsDirectoryCaseInsensitive(tempDirectory))
            {
                Assert.Inconclusive("Host temp filesystem is case-insensitive; covered by the case-insensitive sibling test.");
            }

            string input = Path.Combine(tempDirectory, "report.json");
            File.WriteAllText(input, BuildReport());

            string casedOutput = Path.Combine(tempDirectory, "REPORT.json");
            await CtrfReportMerger.MergeToFileAsync([input], casedOutput, CancellationToken.None);
            Assert.IsTrue(File.Exists(input));
            Assert.IsTrue(File.Exists(casedOutput));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static bool IsDirectoryCaseInsensitive(string directory)
    {
        string probe = Path.Combine(directory, "CaseProbe" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(probe, string.Empty);
        try
        {
            // Only the file name is lower-cased so the (possibly case-sensitive) directory path stays intact.
            return File.Exists(Path.Combine(directory, Path.GetFileName(probe).ToLowerInvariant()));
        }
        finally
        {
            File.Delete(probe);
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

    [TestMethod]
    public void Merge_StampsMergerIdentityInGeneratedBy()
    {
        // The merged document is produced by this merger, so 'generatedBy' must be the merger's identity,
        // not the (possibly different-versioned) first input's value.
        var report = new JsonObject
        {
            ["reportFormat"] = "CTRF",
            ["specVersion"] = "0.0.0",
            ["generatedBy"] = "SomeOtherTool v9",
            ["results"] = new JsonObject
            {
                ["tool"] = new JsonObject { ["name"] = "MSTest" },
                ["tests"] = new JsonArray { Test("t", "passed") },
            },
        };

        string generatedBy = (string)JsonNode.Parse(CtrfReportMerger.Merge([report.ToJsonString()]))!["generatedBy"]!;

        Assert.AreEqual("Microsoft.Testing.Extensions.CtrfReport", generatedBy);
    }

    [TestMethod]
    public void Merge_IgnoresNonCtrfInputs()
    {
        // A JSON object that is not a CTRF document must not be accepted (become 'first') and have
        // CTRF-shaped data emitted under its label; its tests are excluded from the merge. This covers both
        // a non-CTRF reportFormat and a missing reportFormat (the required format discriminator).
        string ctrf = BuildReport(testEntries: [Test("a", "passed")]);
        var wrongFormat = new JsonObject
        {
            ["reportFormat"] = "JUnit",
            ["results"] = new JsonObject { ["tests"] = new JsonArray { Test("x", "passed") } },
        };
        var noFormat = new JsonObject
        {
            ["results"] = new JsonObject { ["tests"] = new JsonArray { Test("y", "passed") } },
        };

        JsonNode merged = JsonNode.Parse(CtrfReportMerger.Merge([ctrf, wrongFormat.ToJsonString(), noFormat.ToJsonString()]))!;

        Assert.AreEqual("CTRF", (string?)merged["reportFormat"]);
        Assert.AreEqual(1, (long)merged["results"]!["summary"]!["tests"]!);
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
        string? runId = null,
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

        if (runId is not null)
        {
            report["runId"] = runId;
        }

        return report.ToJsonString();
    }

    [TestMethod]
    public void Merge_CarriesRunId_WhenEveryInputReportsTheSameOne()
    {
        // Per-attempt (and per-shard) documents of one logical run share a runId; the merged document
        // describes that same logical run, so it keeps the id while getting its own reportId.
        string a = BuildReport(runId: "run-42", testEntries: [Test("a", "passed")]);
        string b = BuildReport(runId: "run-42", testEntries: [Test("b", "passed")]);

        JsonNode merged = JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!;

        Assert.AreEqual("run-42", (string?)merged["runId"]);
        Assert.AreNotEqual("run-42", (string?)merged["reportId"]);
        Assert.AreNotEqual((string?)JsonNode.Parse(a)!["reportId"], (string?)merged["reportId"]);
    }

    [TestMethod]
    public void Merge_OmitsRunId_WhenInputsBelongToDifferentRuns()
    {
        string a = BuildReport(runId: "run-1");
        string b = BuildReport(runId: "run-2");

        JsonNode merged = JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!;

        Assert.IsNull(merged["runId"]);
    }

    [TestMethod]
    public void Merge_OmitsRunId_WhenAnInputHasNone()
    {
        // An input with no runId may or may not belong to the same run, so claiming the known one would
        // assert a correlation the inputs do not support.
        string a = BuildReport(runId: "run-1");
        string b = BuildReport();

        JsonNode merged = JsonNode.Parse(CtrfReportMerger.Merge([a, b]))!;

        Assert.IsNull(merged["runId"]);
    }

    [TestMethod]
    public void Merge_DefaultMode_DoesNotCollapseRepeatedTests()
    {
        // Cross-module merges must keep every row: MTP UIDs are only unique within an assembly, so two
        // same-named tests can legitimately come from different modules.
        string attempt1 = BuildReport(testEntries: [Attempt("t", "failed", uid: "u1")]);
        string attempt2 = BuildReport(testEntries: [Attempt("t", "passed", uid: "u1")]);

        JsonNode results = JsonNode.Parse(CtrfReportMerger.Merge([attempt1, attempt2]))!["results"]!;

        Assert.HasCount(2, (JsonArray)results["tests"]!);
        Assert.AreEqual(2, (long)results["summary"]!["tests"]!);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_FoldsEarlierAttemptsIntoRetryHistory()
    {
        // Three per-attempt documents of one orchestrated retry run: attempts 1 and 2 failed, attempt 3 passed.
        string attempt1 = BuildReport(testEntries: [Attempt("flaky test", "failed", uid: "u1", duration: 120, message: "boom 1")]);
        string attempt2 = BuildReport(testEntries: [Attempt("flaky test", "failed", uid: "u1", duration: 130, message: "boom 2")]);
        string attempt3 = BuildReport(testEntries: [Attempt("flaky test", "passed", uid: "u1", duration: 140)]);

        JsonNode results = JsonNode.Parse(CtrfReportMerger.Merge([attempt1, attempt2, attempt3], CtrfMergeMode.CollapseRetryAttempts))!["results"]!;

        var tests = (JsonArray)results["tests"]!;
        Assert.HasCount(1, tests);

        JsonNode test = tests[0]!;
        Assert.AreEqual("passed", (string?)test["status"]);
        Assert.IsTrue((bool)test["flaky"]!);

        // ctrf-io/ctrf#58: retryAttempts holds attempts 1..N-1, so retries == retryAttempts.length and the
        // final attempt is retries + 1.
        var retryAttempts = (JsonArray)test["retryAttempts"]!;
        Assert.HasCount(2, retryAttempts);
        Assert.AreEqual(2, (long)test["retries"]!);
        Assert.AreEqual(1, (long)retryAttempts[0]!["attempt"]!);
        Assert.AreEqual(2, (long)retryAttempts[1]!["attempt"]!);
        Assert.AreEqual("boom 1", (string?)retryAttempts[0]!["message"]);
        Assert.AreEqual("boom 2", (string?)retryAttempts[1]!["message"]);

        // The test object carries the FINAL attempt's duration, not the sum across attempts.
        Assert.AreEqual(140, (long)test["duration"]!);
        Assert.AreEqual(120, (long)retryAttempts[0]!["duration"]!);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_CountsLogicalTestsOnce()
    {
        // The scenario from ctrf-io/ctrf#58: a 4-test suite where each attempt re-runs only what failed.
        // The merged document describes the logical run, so it reports 4 tests, not the 8 executions.
        string attempt1 = BuildReport(testEntries:
        [
            Attempt("ok1", "passed", uid: "u1"),
            Attempt("ok2", "passed", uid: "u2"),
            Attempt("recovers", "failed", uid: "u3"),
            Attempt("always fails", "failed", uid: "u4"),
        ]);
        string attempt2 = BuildReport(testEntries:
        [
            Attempt("recovers", "passed", uid: "u3"),
            Attempt("always fails", "failed", uid: "u4"),
        ]);
        string attempt3 = BuildReport(testEntries: [Attempt("always fails", "failed", uid: "u4")]);
        string attempt4 = BuildReport(testEntries: [Attempt("always fails", "failed", uid: "u4")]);

        JsonNode results = JsonNode.Parse(
            CtrfReportMerger.Merge([attempt1, attempt2, attempt3, attempt4], CtrfMergeMode.CollapseRetryAttempts))!["results"]!;

        JsonNode summary = results["summary"]!;
        Assert.AreEqual(4, (long)summary["tests"]!);
        Assert.AreEqual(3, (long)summary["passed"]!);
        Assert.AreEqual(1, (long)summary["failed"]!);
        Assert.AreEqual(1, (long)summary["flaky"]!);
        Assert.HasCount(4, (JsonArray)results["tests"]!);

        JsonNode alwaysFails = ((JsonArray)results["tests"]!).Single(t => (string?)t!["name"] == "always fails")!;
        Assert.AreEqual("failed", (string?)alwaysFails["status"]);
        Assert.AreEqual(3, (long)alwaysFails["retries"]!);
        Assert.IsNull(alwaysFails["flaky"]);

        JsonNode neverRetried = ((JsonArray)results["tests"]!).Single(t => (string?)t!["name"] == "ok1")!;
        Assert.IsNull(neverRetried["retries"]);
        Assert.IsNull(neverRetried["retryAttempts"]);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_FlattensInProcessRetriesOfEachAttempt()
    {
        // An attempt process can itself have retried the test in-process; those executions are already in its
        // retryAttempts[] and must keep their place in the merged history instead of being dropped.
        JsonObject firstAttempt = Attempt("t", "failed", uid: "u1", message: "second execution");
        firstAttempt["retryAttempts"] = new JsonArray(new JsonObject
        {
            ["attempt"] = 1,
            ["status"] = "failed",
            ["message"] = "first execution",
        });

        string attempt1 = BuildReport(testEntries: [firstAttempt]);
        string attempt2 = BuildReport(testEntries: [Attempt("t", "passed", uid: "u1")]);

        JsonNode test = ((JsonArray)JsonNode.Parse(
            CtrfReportMerger.Merge([attempt1, attempt2], CtrfMergeMode.CollapseRetryAttempts))!["results"]!["tests"]!)[0]!;

        var retryAttempts = (JsonArray)test["retryAttempts"]!;
        Assert.HasCount(2, retryAttempts);
        Assert.AreEqual(3, (long)test["retries"]! + 1, "The final attempt is retries + 1.");
        Assert.AreEqual("first execution", (string?)retryAttempts[0]!["message"]);
        Assert.AreEqual("second execution", (string?)retryAttempts[1]!["message"]);
        Assert.AreEqual(1, (long)retryAttempts[0]!["attempt"]!);
        Assert.AreEqual(2, (long)retryAttempts[1]!["attempt"]!);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_ProjectsAttemptsOntoRetryAttemptShape()
    {
        // CTRF section 11 forbids unknown fields on a retry attempt, so test-only fields must not leak into it;
        // rawStatus has no attempt-level slot and moves under 'extra', the only permitted extension point.
        JsonObject failing = Attempt("t", "failed", uid: "u1", message: "boom");
        failing["rawStatus"] = "timedOut";
        failing["suite"] = new JsonArray("NS", "C");
        failing["tags"] = new JsonArray("slow");
        failing["trace"] = "at X()";

        string attempt1 = BuildReport(testEntries: [failing]);
        string attempt2 = BuildReport(testEntries: [Attempt("t", "passed", uid: "u1")]);

        JsonNode test = ((JsonArray)JsonNode.Parse(
            CtrfReportMerger.Merge([attempt1, attempt2], CtrfMergeMode.CollapseRetryAttempts))!["results"]!["tests"]!)[0]!;

        JsonNode retryAttempt = ((JsonArray)test["retryAttempts"]!)[0]!;
        Assert.AreEqual("failed", (string?)retryAttempt["status"]);
        Assert.AreEqual("boom", (string?)retryAttempt["message"]);
        Assert.AreEqual("at X()", (string?)retryAttempt["trace"]);
        Assert.AreEqual("timedOut", (string?)retryAttempt["extra"]!["rawStatus"]);
        Assert.AreEqual("u1", (string?)retryAttempt["extra"]!["uid"]);
        Assert.IsNull(retryAttempt["name"]);
        Assert.IsNull(retryAttempt["suite"]);
        Assert.IsNull(retryAttempt["tags"]);
        Assert.IsNull(retryAttempt["rawStatus"]);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_DropsStaleFlakyFlag_WhenFinalAttemptFails()
    {
        // An input's own flaky flag only describes the attempt that produced it; a later failure means the
        // logical test is not flaky (CTRF 9.22 requires the FINAL status to be passed).
        JsonObject recovered = Attempt("t", "passed", uid: "u1");
        recovered["flaky"] = true;

        string attempt1 = BuildReport(testEntries: [recovered]);
        string attempt2 = BuildReport(testEntries: [Attempt("t", "failed", uid: "u1")]);

        JsonNode results = JsonNode.Parse(
            CtrfReportMerger.Merge([attempt1, attempt2], CtrfMergeMode.CollapseRetryAttempts))!["results"]!;

        JsonNode test = ((JsonArray)results["tests"]!)[0]!;
        Assert.AreEqual("failed", (string?)test["status"]);
        Assert.IsNull(test["flaky"]);
        Assert.AreEqual(0, (long)results["summary"]!["flaky"]!);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_UsesNameAndSuite_WhenNoIdentifierIsAvailable()
    {
        // Without testId or extra.uid, the suite path plus name is the only identity available, and two tests
        // that only share a name must not be fused.
        static JsonObject Named(string name, string status, string suite)
        {
            JsonObject test = Test(name, status);
            test["suite"] = new JsonArray(suite);
            return test;
        }

        string attempt1 = BuildReport(testEntries: [Named("t", "failed", "A"), Named("t", "failed", "B")]);
        string attempt2 = BuildReport(testEntries: [Named("t", "passed", "A"), Named("t", "failed", "B")]);

        var tests = (JsonArray)JsonNode.Parse(
            CtrfReportMerger.Merge([attempt1, attempt2], CtrfMergeMode.CollapseRetryAttempts))!["results"]!["tests"]!;

        Assert.HasCount(2, tests);
        Assert.AreEqual("passed", (string?)tests[0]!["status"]);
        Assert.AreEqual("failed", (string?)tests[1]!["status"]);
        Assert.AreEqual(1, (long)tests[0]!["retries"]!);
        Assert.AreEqual(1, (long)tests[1]!["retries"]!);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Merge_RejectsMalformedTestRows_SoTheMergedDocumentStaysValid(bool collapseRetryAttempts)
    {
        // The mode is passed as a bool because CtrfMergeMode is internal and a test method must be public.
        CtrfMergeMode mode = collapseRetryAttempts ? CtrfMergeMode.CollapseRetryAttempts : CtrfMergeMode.Concatenate;
        // tests[] comes from an untrusted file. The CTRF schema types its items as Test objects, so carrying a
        // bare string or a JSON null through would turn a defect localized to one input into an invalid MERGED
        // document. Such rows are dropped — the same policy the merger already applies to a whole non-CTRF input
        // — and both modes must agree on that, since the merged tests[] is the same array either way.
        // `summary` is deliberately a string here too: `results.summary` is equally untrusted, and reading a
        // property off a non-object node throws just as `tests[]` did.
        string malformed = """
            {"reportFormat":"CTRF","specVersion":"0.0.0","results":{"summary":"broken","tests":["oops",null,42]}}
            """;
        string wellFormed = BuildReport(testEntries: [Attempt("t", "passed", uid: "u1")]);

        JsonNode results = JsonNode.Parse(CtrfReportMerger.Merge([malformed, wellFormed], mode))!["results"]!;

        var tests = (JsonArray)results["tests"]!;
        Assert.HasCount(1, tests, "Only the real test survives.");
        Assert.AreEqual("t", (string?)tests[0]!["name"]);

        // CTRF 8.1: summary.tests equals the tests[] length, and the status buckets must add back up to it —
        // a dropped row must not leave a phantom entry in either the array or the counters.
        JsonNode summary = results["summary"]!;
        Assert.AreEqual(1, (long)summary["tests"]!);
        Assert.AreEqual(1, (long)summary["passed"]!);
        Assert.AreEqual(0, (long)summary["other"]!);
        long bucketSum = (long)summary["passed"]! + (long)summary["failed"]! + (long)summary["skipped"]!
            + (long)summary["pending"]! + (long)summary["other"]!;
        Assert.AreEqual((long)summary["tests"]!, bucketSum, "Every counted test must land in exactly one status bucket.");
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_ToleratesNonObjectExtra()
    {
        // `extra` is free-form, so a foreign producer may put a string or an array there. Identity resolution
        // must fall back to the suite/name key instead of throwing while indexing it.
        static JsonObject WithExtra(string status, JsonNode extra)
        {
            JsonObject test = Test("t", status);
            test["extra"] = extra;
            return test;
        }

        string attempt1 = BuildReport(testEntries: [WithExtra("failed", "ci-run-14")]);
        string attempt2 = BuildReport(testEntries: [WithExtra("passed", new JsonArray("a"))]);

        var tests = (JsonArray)JsonNode.Parse(
            CtrfReportMerger.Merge([attempt1, attempt2], CtrfMergeMode.CollapseRetryAttempts))!["results"]!["tests"]!;

        // Both rows share the name identity, so they collapse even though neither carries a usable extra.uid.
        Assert.HasCount(1, tests);
        Assert.AreEqual("passed", (string?)tests[0]!["status"]);
        Assert.AreEqual(1, (long)tests[0]!["retries"]!);
        Assert.IsTrue((bool)tests[0]!["flaky"]!);
    }

    private static JsonObject Attempt(string name, string status, string uid, long duration = 1, string? message = null)
    {
        var test = new JsonObject
        {
            ["name"] = name,
            ["status"] = status,
            ["duration"] = duration,
            ["extra"] = new JsonObject { ["uid"] = uid },
        };

        if (message is not null)
        {
            test["message"] = message;
        }

        return test;
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
