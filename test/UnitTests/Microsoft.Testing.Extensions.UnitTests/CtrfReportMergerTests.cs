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
    [DataRow(false)]
    [DataRow(true)]
    public void Merge_WhenAnyInputIsIncomplete_PropagatesRecoveryMetadata(bool collapseRetryAttempts)
    {
        JsonObject incomplete = JsonNode.Parse(BuildReport(testEntries: [Test("incomplete", "failed")]))!.AsObject();
        incomplete["results"]!["environment"]!["extra"]!["incomplete"] = true;
        incomplete["results"]!["environment"]!["extra"]!["runStatus"] = "aborted";

        JsonNode merged = JsonNode.Parse(CtrfReportMerger.Merge(
            [incomplete.ToJsonString(), BuildReport(testEntries: [Test("complete", "passed")])],
            collapseRetryAttempts ? CtrfMergeMode.CollapseRetryAttempts : CtrfMergeMode.Concatenate))!;
        JsonNode extra = merged["results"]!["environment"]!["extra"]!;

        Assert.IsTrue((bool)extra["incomplete"]!);
        Assert.AreEqual("aborted", (string?)extra["runStatus"]);
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

    [TestMethod]
    public async Task MergeAllToFileAsync_WithInvalidInput_ThrowsWithoutWritingOutput()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ctrf-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string valid = Path.Combine(tempDirectory, "valid.json");
            string invalid = Path.Combine(tempDirectory, "invalid.json");
            string output = Path.Combine(tempDirectory, "out", "merged.json");
            File.WriteAllText(valid, BuildReport());
            File.WriteAllText(invalid, """{"not":"ctrf"}""");

            ArgumentException exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => CtrfReportMerger.MergeAllToFileAsync([valid, invalid], output, CancellationToken.None));

            Assert.AreEqual("inputPaths", exception.ParamName);
            Assert.Contains("Every input must be a valid CTRF report.", exception.Message);
            Assert.IsFalse(File.Exists(output));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeAllToFileAsync_WithAllInputsInvalid_ThrowsWithoutWritingOutput()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ctrf-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string first = Path.Combine(tempDirectory, "first.json");
            string second = Path.Combine(tempDirectory, "second.json");
            string output = Path.Combine(tempDirectory, "out", "merged.json");
            File.WriteAllText(first, """{"not":"ctrf"}""");
            File.WriteAllText(second, "[]");

            ArgumentException exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => CtrfReportMerger.MergeAllToFileAsync([first, second], output, CancellationToken.None));

            Assert.AreEqual("inputPaths", exception.ParamName);
            Assert.Contains("Every input must be a valid CTRF report.", exception.Message);
            Assert.IsFalse(File.Exists(output));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeAllToFileAsync_WithUnrepresentableTests_ThrowsWithoutWritingOutput()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ctrf-merge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            string valid = Path.Combine(tempDirectory, "valid.json");
            string invalid = Path.Combine(tempDirectory, "invalid.json");
            File.WriteAllText(valid, BuildReport());
            string[] invalidReports =
            [
                """{"reportFormat":"CTRF","results":{}}""",
                """{"reportFormat":"CTRF","results":{"tests":["not-a-test"]}}""",
            ];

            for (int i = 0; i < invalidReports.Length; i++)
            {
                string output = Path.Combine(tempDirectory, "out", $"merged-{i}.json");
                File.WriteAllText(invalid, invalidReports[i]);

                await Assert.ThrowsExactlyAsync<ArgumentException>(
                    () => CtrfReportMerger.MergeAllToFileAsync([valid, invalid], output, CancellationToken.None));

                Assert.IsFalse(File.Exists(output));
            }
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void CreateDeterministicId_IsStableAndSensitiveToInputValues()
    {
        Guid first = CtrfReportMerger.CreateDeterministicId(["a\0execution-1", "b\0execution-2"]);

        Assert.AreEqual(new Guid("a013bf25-9502-a584-31ca-332457e596d2"), first);
        Assert.AreEqual(first, CtrfReportMerger.CreateDeterministicId(["a\0execution-1", "b\0execution-2"]));
        Assert.AreNotEqual(first, CtrfReportMerger.CreateDeterministicId(["a\0execution-3", "b\0execution-2"]));
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

        // Suppressing the run correlation must not suppress the merged document's own identity.
        Assert.IsNotNull((string?)merged["reportId"]);
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
        Assert.IsNotNull((string?)merged["reportId"]);
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
        // property off a non-object node throws just as `tests[]` did. The well-formed-looking rows carry
        // wrong-typed VALUES — a numeric `status`, a numeric `suite` segment, a numeric `status` inside a nested
        // `retryAttempts[]` entry — because the explicit (string?) conversion on a JsonNode throws for a
        // non-string value instead of yielding null.
        string malformed = """
            {"reportFormat":"CTRF","specVersion":"0.0.0","results":{"summary":"broken","tests":[
              "oops",
              null,
              42,
              {"name":"numeric status","status":7},
              {"name":"numeric suite","status":"failed","suite":["A",7,{"x":1}]},
              {"name":"nested","status":"passed","extra":{"uid":"n1"},"retryAttempts":[{"attempt":1,"status":3}]}
            ]}}
            """;
        string wellFormed = BuildReport(testEntries: [Attempt("t", "passed", uid: "u1")]);

        JsonNode results = JsonNode.Parse(CtrfReportMerger.Merge([malformed, wellFormed], mode))!["results"]!;

        var tests = (JsonArray)results["tests"]!;
        Assert.HasCount(4, tests, "The three non-object elements are dropped; wrong-typed values are tolerated.");
        Assert.AreEqual("numeric status", (string?)tests[0]!["name"]);
        Assert.AreEqual("t", (string?)tests[3]!["name"]);

        // CTRF 8.1: summary.tests equals the tests[] length, and the status buckets must add back up to it —
        // a dropped row must not leave a phantom entry in either the array or the counters. An unreadable
        // status is classified as 'other' rather than crashing the merge.
        JsonNode summary = results["summary"]!;
        Assert.AreEqual(4, (long)summary["tests"]!);
        Assert.AreEqual(2, (long)summary["passed"]!);
        Assert.AreEqual(1, (long)summary["failed"]!);
        Assert.AreEqual(1, (long)summary["other"]!, "A non-string status is unclassifiable.");
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

    [TestMethod]
    public void Merge_ReportId_IsDeterministicPerModeAndDiffersBetweenModes()
    {
        // The two modes turn the same inputs into materially different documents (three rows vs one collapsed
        // row here), so CTRF 5.3 requires each to get its own reportId — one id must not name both artifacts.
        // Determinism per mode (RFC 018 idempotency) must survive that.
        string attempt1 = BuildReport(testEntries: [Attempt("t", "failed", uid: "u1")]);
        string attempt2 = BuildReport(testEntries: [Attempt("t", "passed", uid: "u1")]);
        string[] inputs = [attempt1, attempt2];

        string concatenated = (string)JsonNode.Parse(CtrfReportMerger.Merge(inputs, CtrfMergeMode.Concatenate))!["reportId"]!;
        string collapsed = (string)JsonNode.Parse(CtrfReportMerger.Merge(inputs, CtrfMergeMode.CollapseRetryAttempts))!["reportId"]!;

        Assert.AreNotEqual(concatenated, collapsed, "Two materially different merged documents must not share a reportId.");

        // Re-merging the same inputs the same way reproduces the id.
        Assert.AreEqual(concatenated, (string)JsonNode.Parse(CtrfReportMerger.Merge(inputs, CtrfMergeMode.Concatenate))!["reportId"]!);
        Assert.AreEqual(collapsed, (string)JsonNode.Parse(CtrfReportMerger.Merge(inputs, CtrfMergeMode.CollapseRetryAttempts))!["reportId"]!);

        // The default overload keeps concatenating, so it must agree with the explicit Concatenate mode.
        Assert.AreEqual(concatenated, (string)JsonNode.Parse(CtrfReportMerger.Merge(inputs))!["reportId"]!);

        // CTRF 5.3: reportId MUST be a valid UUID when present.
        Assert.IsTrue(Guid.TryParse(collapsed, out _), $"reportId must be a UUID, got '{collapsed}'.");
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_DoesNotFuseTestsWhoseNameContainsTheIdentitySeparator()
    {
        // A CTRF `name` is an arbitrary non-empty string, so it may contain whatever character the identity key
        // uses as a separator. With plain separation, suite ["A"] + name "B\u001fC" and suite ["A","B"] + name
        // "C" flatten to the same key, which would fuse two unrelated tests and silently drop a result.
        static JsonObject Named(string status, string name, params string[] suite)
        {
            JsonObject test = Test(name, status);
            test["suite"] = new JsonArray([.. suite.Select(s => (JsonNode)JsonValue.Create(s)!)]);
            return test;
        }

        string report = BuildReport(testEntries:
        [
            Named("failed", "B\u001fC", "A"),
            Named("passed", "C", "A", "B"),
        ]);

        var tests = (JsonArray)JsonNode.Parse(
            CtrfReportMerger.Merge([report], CtrfMergeMode.CollapseRetryAttempts))!["results"]!["tests"]!;

        Assert.HasCount(2, tests, "Two distinct tests must not collapse into one.");
        Assert.AreEqual("failed", (string?)tests[0]!["status"]);
        Assert.AreEqual("passed", (string?)tests[1]!["status"]);
        Assert.IsNull(tests[0]!["retries"], "Neither row is a retry of the other.");
        Assert.IsNull(tests[1]!["retries"]);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_UsesLegacyIdWhenTestIdIsAbsent()
    {
        // CTRF 9.1: `id` is a stable test-case identifier that consumers treat as legacy, preferring `testId`
        // only when both are present. An id-only report must therefore collapse on it rather than dropping to
        // the suite/name heuristic, which would fuse these two distinct same-named tests.
        static JsonObject WithId(string id, string status)
        {
            JsonObject test = Test("same name", status);
            test["id"] = id;
            return test;
        }

        string attempt1 = BuildReport(testEntries: [WithId("id-1", "failed"), WithId("id-2", "failed")]);
        string attempt2 = BuildReport(testEntries: [WithId("id-1", "passed"), WithId("id-2", "failed")]);

        var tests = (JsonArray)JsonNode.Parse(
            CtrfReportMerger.Merge([attempt1, attempt2], CtrfMergeMode.CollapseRetryAttempts))!["results"]!["tests"]!;

        Assert.HasCount(2, tests, "Two distinct ids must stay two tests.");
        Assert.AreEqual("passed", (string?)tests[0]!["status"]);
        Assert.IsTrue((bool)tests[0]!["flaky"]!);
        Assert.AreEqual("failed", (string?)tests[1]!["status"]);
        Assert.AreEqual(1, (long)tests[1]!["retries"]!);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_DoesNotFuseRowsThatDifferOnlyByParametersOrFilePath()
    {
        // Suite plus name is not unique on its own: parameterized rows share both while differing in their
        // parameters (CTRF 9.30), and same-named tests in different files differ only by path (9.19). Fusing
        // them would silently drop a result.
        static JsonObject Row(string status, JsonNode? parameters, string? filePath)
        {
            JsonObject test = Test("same name", status);
            if (parameters is not null)
            {
                test["parameters"] = parameters;
            }

            if (filePath is not null)
            {
                test["filePath"] = filePath;
            }

            return test;
        }

        string report = BuildReport(testEntries:
        [
            Row("failed", new JsonObject { ["value"] = 1 }, null),
            Row("passed", new JsonObject { ["value"] = 2 }, null),
            Row("skipped", null, "a.cs"),
            Row("passed", null, "b.cs"),
        ]);

        var tests = (JsonArray)JsonNode.Parse(
            CtrfReportMerger.Merge([report], CtrfMergeMode.CollapseRetryAttempts))!["results"]!["tests"]!;

        Assert.HasCount(4, tests, "Rows differing only by parameters or filePath are distinct tests.");
        Assert.AreSequenceEqual(
            (string?[])["failed", "passed", "skipped", "passed"],
            tests.Select(t => (string?)t!["status"]).ToArray());
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_KeepsRetryHistorySchemaValid()
    {
        // CTRF section 11 constrains a retry attempt: `status` must be one of five values, and no unknown field
        // may appear outside `extra`. Both a promoted test row and an attempt an input already nested must be
        // shaped to that, otherwise one foreign document makes the merged history schema-invalid.
        JsonObject firstAttempt = Test("t", "failed");
        firstAttempt["extra"] = new JsonObject { ["uid"] = "u1" };
        firstAttempt["status"] = 7;
        firstAttempt["retryAttempts"] = new JsonArray(new JsonObject
        {
            ["attempt"] = 1,
            ["status"] = 3,
            ["message"] = "nested",
            ["name"] = "a test-only field section 11 forbids",
        });

        JsonObject finalAttempt = Test("t", "passed");
        finalAttempt["extra"] = new JsonObject { ["uid"] = "u1" };

        string report = BuildReport(testEntries: [firstAttempt, finalAttempt]);

        JsonNode test = ((JsonArray)JsonNode.Parse(
            CtrfReportMerger.Merge([report], CtrfMergeMode.CollapseRetryAttempts))!["results"]!["tests"]!)[0]!;

        var retryAttempts = (JsonArray)test["retryAttempts"]!;
        Assert.HasCount(2, retryAttempts);

        // The nested attempt keeps its diagnostics but loses the wrong-typed status and the test-only field.
        Assert.AreEqual("other", (string?)retryAttempts[0]!["status"], "A non-string status is normalized.");
        Assert.AreEqual("nested", (string?)retryAttempts[0]!["message"]);
        Assert.IsNull(retryAttempts[0]!["name"], "Section 11 forbids unknown fields outside 'extra'.");

        // The promoted row is normalized the same way.
        Assert.AreEqual("other", (string?)retryAttempts[1]!["status"]);

        // Attempt numbers stay a contiguous 1..N-1 sequence after the projection.
        Assert.AreEqual(1, (long)retryAttempts[0]!["attempt"]!);
        Assert.AreEqual(2, (long)retryAttempts[1]!["attempt"]!);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_LeavesAnUnmergedRowExactlyAsWritten()
    {
        // Validity contract: the merger shapes what it synthesizes and relays what it passes through. A test
        // that occurs in only one input (for example one that recovered through in-process retries and was
        // therefore never re-run by the orchestrator) has nothing merged into it, so its row -- including the
        // retryAttempts[] its producer already recorded -- must come out byte-identical. Repairing it here
        // would make merging a single document mutate it.
        JsonObject row = Test("t", "passed");
        row["extra"] = new JsonObject { ["uid"] = "u1" };
        row["retryAttempts"] = new JsonArray(new JsonObject
        {
            ["attempt"] = 7,
            ["status"] = "failed",
            ["message"] = "recorded by the producer",
        });

        string report = BuildReport(testEntries: [row]);
        string original = ((JsonArray)JsonNode.Parse(report)!["results"]!["tests"]!)[0]!.ToJsonString();

        var tests = (JsonArray)JsonNode.Parse(
            CtrfReportMerger.Merge([report], CtrfMergeMode.CollapseRetryAttempts))!["results"]!["tests"]!;

        Assert.HasCount(1, tests);
        Assert.AreEqual(original, tests[0]!.ToJsonString(), "An unmerged row must be relayed verbatim.");

        // Specifically: the producer's own attempt numbering is not rewritten, and retries/flaky are not invented.
        Assert.AreEqual(7, (long)((JsonArray)tests[0]!["retryAttempts"]!)[0]!["attempt"]!);
        Assert.IsNull(tests[0]!["retries"]);
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
