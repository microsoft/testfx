// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

using Microsoft.Testing.Extensions.HtmlReport;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public class HtmlReportMergerTests
{
    [TestMethod]
    public void Merge_CollapseRetryAttempts_FoldsEarlierAttemptsInOrderAndCleansAnnotations()
    {
        JsonObject first = Test("failed", durationMs: 10);
        JsonObject second = Test("failed", durationMs: 20);
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
        Assert.AreEqual(10d, (double)retryAttempts[0]!["durationMs"]!);
        Assert.AreEqual(2, (int)retryAttempts[1]!["attempt"]!);
        Assert.AreEqual(20d, (double)retryAttempts[1]!["durationMs"]!);
        Assert.AreEqual(1, (int)report["summary"]!["flaky"]!);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_DoesNotMarkAllPassingAttemptsFlaky()
    {
        JsonObject report = Merge([Report(Test("passed")), Report(Test("passed"))]);

        var tests = (JsonArray)report["tests"]!;
        Assert.HasCount(1, tests);
        JsonNode test = tests[0]!;
        Assert.IsNull(test["flaky"]);
        Assert.AreEqual(0, (int)report["summary"]!["flaky"]!);
    }

    [TestMethod]
    public void Merge_CollapseRetryAttempts_DoesNotMarkFinalFailureFlaky()
    {
        JsonObject report = Merge([Report(Test("failed")), Report(Test("failed"))]);

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
        Assert.AreEqual("message", (string?)projected["errorMessage"]);
        Assert.AreEqual("Exception", (string?)projected["exceptionType"]);
        Assert.AreEqual("trace", (string?)projected["stackTrace"]);
        Assert.AreEqual("stdout", (string?)projected["standardOutput"]);
        Assert.AreEqual("stderr", (string?)projected["standardError"]);
        Assert.AreEqual(1, (int)projected["retryAttemptNumber"]!);
        Assert.IsTrue((bool)projected["isSupersededRetryAttempt"]!);

        JsonObject sparseProjection = retryAttempts[1]!.AsObject();
        Assert.AreSequenceEqual(
            new[] { "attempt", "outcome", "durationMs", "errorMessage" },
            sparseProjection.Select(property => property.Key).ToArray());
    }

    private static JsonObject Merge(IReadOnlyList<string> reports)
        => JsonNode.Parse(HtmlReportEngine.ExtractReportJson(
            HtmlReportMerger.Merge(reports, HtmlMergeMode.CollapseRetryAttempts)))!.AsObject();

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

    private static JsonObject Test(string outcome, double durationMs = 1)
        => new()
        {
            ["uid"] = "uid",
            ["displayName"] = "test",
            ["outcome"] = outcome,
            ["durationMs"] = durationMs,
        };
}
