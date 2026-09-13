// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

using Microsoft.Testing.Extensions.HtmlReport;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HtmlReportMergerTests
{
    [TestMethod]
    public void Merge_CollapseRetryAttempts_FoldsEarlierAttemptsInOrderAndCleansAnnotations()
    {
        JsonObject first = Test("failed", durationMs: 10);
        first["errorMessage"] = "first failure";
        JsonObject second = Test("timedOut", durationMs: 20);
        second["errorMessage"] = "second failure";
        JsonObject final = Test("passed", durationMs: 30);
        final["attemptIndex"] = 3;
        final["attemptOf"] = 3;

        JsonObject report = Merge([Report(first), Report(second), Report(final)]);

        var tests = (JsonArray)report["tests"]!;
        Assert.HasCount(1, tests);
        JsonNode test = tests[0]!;
        Assert.AreEqual("passed", (string?)test["outcome"]);
        Assert.AreEqual(30d, (double)test["durationMs"]!);
        Assert.AreEqual(2, (int)test["retries"]!);
        Assert.IsNull(test["attemptIndex"]);
        Assert.IsNull(test["attemptOf"]);
        Assert.IsTrue((bool)test["flaky"]!);

        var retryAttempts = (JsonArray)test["retryAttempts"]!;
        Assert.HasCount(2, retryAttempts);
        Assert.AreEqual(1, (int)retryAttempts[0]!["attempt"]!);
        Assert.AreEqual("failed", (string?)retryAttempts[0]!["outcome"]);
        Assert.AreEqual(10d, (double)retryAttempts[0]!["durationMs"]!);
        Assert.AreEqual("first failure", (string?)retryAttempts[0]!["errorMessage"]);
        Assert.AreEqual(2, (int)retryAttempts[1]!["attempt"]!);
        Assert.AreEqual("timedOut", (string?)retryAttempts[1]!["outcome"]);
        Assert.AreEqual(20d, (double)retryAttempts[1]!["durationMs"]!);
        Assert.AreEqual("second failure", (string?)retryAttempts[1]!["errorMessage"]);

        JsonNode summary = report["summary"]!;
        Assert.AreEqual(1, (int)summary["total"]!);
        Assert.AreEqual(1, (int)summary["passed"]!);
        Assert.AreEqual(0, (int)summary["failed"]!);
        Assert.AreEqual(0, (int)summary["timedOut"]!);
        Assert.AreEqual(1, (int)summary["flaky"]!);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_DoesNotMarkAllPassingAttemptsFlaky()
    {
        JsonObject final = Test("passed");
        final["flaky"] = true;

        JsonObject report = Merge([Report(Test("passed")), Report(final)]);

        var tests = (JsonArray)report["tests"]!;
        Assert.HasCount(1, tests);
        JsonNode test = tests[0]!;
        Assert.IsNull(test["flaky"]);
        Assert.AreEqual(0, (int)report["summary"]!["flaky"]!);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_RemovesStaleFlakyFlagWithoutRetries()
    {
        JsonObject test = Test("passed");
        test["flaky"] = true;

        JsonObject report = Merge([Report(test)]);

        var tests = (JsonArray)report["tests"]!;
        Assert.HasCount(1, tests);
        Assert.IsNull(tests[0]!["flaky"]);
        Assert.AreEqual(0, (int)report["summary"]!["flaky"]!);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_DoesNotMarkFinalFailureFlaky()
    {
        JsonObject final = Test("failed");
        final["flaky"] = true;

        JsonObject report = Merge([Report(Test("failed")), Report(final)]);

        var tests = (JsonArray)report["tests"]!;
        Assert.HasCount(1, tests);
        JsonNode test = tests[0]!;
        Assert.AreEqual("failed", (string?)test["outcome"]);
        Assert.IsNull(test["flaky"]);
        Assert.AreEqual(0, (int)report["summary"]!["flaky"]!);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_DoesNotFuseAmbiguousRowsFromOneReport()
    {
        JsonObject first = Test("failed");
        first["retryAttemptNumber"] = 1;
        JsonObject second = Test("passed");
        second["retryAttemptNumber"] = 1;

        JsonObject report = Merge([Report(first, second)]);

        var tests = (JsonArray)report["tests"]!;
        Assert.HasCount(2, tests);
        Assert.AreEqual("failed", (string?)tests[0]!["outcome"]);
        Assert.AreEqual("passed", (string?)tests[1]!["outcome"]);
        Assert.IsNull(tests[0]!["retryAttempts"]);
        Assert.IsNull(tests[1]!["retryAttempts"]);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_ProjectsOnlyPresentRetryDetailFields()
    {
        JsonObject first = Test("failed", durationMs: 10);
        first["errorMessage"] = "message";
        first["exceptionType"] = "Exception";
        first["stackTrace"] = "trace";
        first["standardOutput"] = "stdout";
        first["standardError"] = "stderr";
        first["retryAttemptNumber"] = 1;
        first["isSupersededRetryAttempt"] = true;
        first["unexpected"] = "not copied";

        JsonObject second = Test("failed", durationMs: 20);
        second["errorMessage"] = "second message";

        JsonObject report = Merge([Report(first), Report(second), Report(Test("passed"))]);

        var retryAttempts = (JsonArray)((JsonArray)report["tests"]!)[0]!["retryAttempts"]!;
        JsonObject projected = retryAttempts[0]!.AsObject();
        Assert.AreSequenceEqual(
            new[]
            {
                "attempt",
                "outcome",
                "durationMs",
                "errorMessage",
                "exceptionType",
                "stackTrace",
                "standardOutput",
                "standardError",
                "retryAttemptNumber",
                "isSupersededRetryAttempt",
            },
            projected.Select(property => property.Key).ToArray());
        Assert.AreEqual("failed", (string?)projected["outcome"]);
        Assert.AreEqual(10d, (double)projected["durationMs"]!);
        Assert.AreEqual("message", (string?)projected["errorMessage"]);
        Assert.AreEqual("Exception", (string?)projected["exceptionType"]);
        Assert.AreEqual("trace", (string?)projected["stackTrace"]);
        Assert.AreEqual("stdout", (string?)projected["standardOutput"]);
        Assert.AreEqual("stderr", (string?)projected["standardError"]);
        Assert.AreEqual(1, (int)projected["retryAttemptNumber"]!);
        Assert.IsTrue((bool)projected["isSupersededRetryAttempt"]!);
        Assert.IsNull(projected["unexpected"]);

        JsonObject sparseProjection = retryAttempts[1]!.AsObject();
        Assert.AreSequenceEqual(
            new[] { "attempt", "outcome", "durationMs", "errorMessage" },
            sparseProjection.Select(property => property.Key).ToArray());
        Assert.IsNull(sparseProjection["exceptionType"]);
        Assert.IsNull(sparseProjection["standardOutput"]);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_DoesNotFuseTestsWhoseIdentityPartsContainTheSeparator()
    {
        JsonObject first = Test("failed", uid: "uid", displayName: "B\0C");
        first["architecture"] = "A";
        JsonObject second = Test("passed", uid: "uid", displayName: "C");
        second["architecture"] = "A\0B";

        JsonObject report = Merge([Report(first), Report(second)]);

        var tests = (JsonArray)report["tests"]!;
        Assert.HasCount(2, tests, "Two distinct tests must not collapse because identity fields contain the separator.");
        Assert.AreEqual("B\0C", (string?)tests[0]!["displayName"]);
        Assert.AreEqual("C", (string?)tests[1]!["displayName"]);
        Assert.IsNull(tests[0]!["retryAttempts"]);
        Assert.IsNull(tests[1]!["retryAttempts"]);
    }

    [TestMethod]
    public void Merge_Concatenate_DoesNotAnnotateTestsWhoseIdentityPartsContainTheSeparatorAsAttempts()
    {
        JsonObject first = Test("failed", uid: "A");
        first["testApplication"] = "B\0C";
        JsonObject second = Test("passed", uid: "A\0B");
        second["testApplication"] = "C";

        JsonObject report = Merge([Report(first), Report(second)], HtmlMergeMode.Concatenate);

        var tests = (JsonArray)report["tests"]!;
        Assert.HasCount(2, tests);
        Assert.AreEqual("A", (string?)tests[0]!["uid"]);
        Assert.AreEqual("B\0C", (string?)tests[0]!["testApplication"]);
        Assert.IsNull(tests[0]!["attemptIndex"]);
        Assert.IsNull(tests[0]!["attemptOf"]);
        Assert.AreEqual("A\0B", (string?)tests[1]!["uid"]);
        Assert.AreEqual("C", (string?)tests[1]!["testApplication"]);
        Assert.IsNull(tests[1]!["attemptIndex"]);
        Assert.IsNull(tests[1]!["attemptOf"]);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_DoesNotFuseSameNamedRowsWithDifferentUids()
    {
        JsonObject parameterized = Test("failed", uid: "parameter-value-1", displayName: "same name");
        parameterized["parameters"] = new JsonObject { ["value"] = 1 };
        JsonObject differentFile = Test("passed", uid: "file-b", displayName: "same name");
        differentFile["filePath"] = "b.cs";

        JsonObject report = Merge([Report(parameterized), Report(differentFile)]);

        var tests = (JsonArray)report["tests"]!;
        Assert.HasCount(2, tests, "HTML rows use uid to distinguish parameterized cases and same-named tests from different files.");
        Assert.AreEqual("parameter-value-1", (string?)tests[0]!["uid"]);
        Assert.AreEqual(1, (int)tests[0]!["parameters"]!["value"]!);
        Assert.AreEqual("file-b", (string?)tests[1]!["uid"]);
        Assert.AreEqual("b.cs", (string?)tests[1]!["filePath"]);
    }

    private static JsonObject Merge(IReadOnlyList<string> reports, HtmlMergeMode mode = HtmlMergeMode.CollapseRetryAttempts)
        => JsonNode.Parse(HtmlReportEngine.ExtractReportJson(
            HtmlReportMerger.Merge(reports, mode)))!.AsObject();

    private static string Report(params JsonObject[] tests)
    {
        var report = new JsonObject
        {
            ["schemaVersion"] = "1",
            ["generator"] = "Microsoft.Testing.Extensions.HtmlReport",
            ["generatorVersion"] = "1.0.0",
            ["testApplication"] = "app",
            ["machineName"] = "machine",
            ["userName"] = "user",
            ["framework"] = "framework",
            ["frameworkUid"] = "framework",
            ["frameworkVersion"] = "1.0.0",
            ["startTime"] = "2026-09-12T08:00:00.0000000+00:00",
            ["endTime"] = "2026-09-12T08:00:01.0000000+00:00",
            ["tests"] = new JsonArray(tests),
            ["summary"] = new JsonObject(),
        };

        return HtmlReportEngine.RenderReport(report.ToJsonString());
    }

    private static JsonObject Test(string outcome, double durationMs = 1, string uid = "uid", string displayName = "test")
        => new()
        {
            ["uid"] = uid,
            ["displayName"] = displayName,
            ["outcome"] = outcome,
            ["durationMs"] = durationMs,
        };
}
