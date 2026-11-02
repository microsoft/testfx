// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CtrfRetryProjectionBehaviorTests
{
    [TestMethod]
    public async Task GenerateReportAsync_PreservesCompletePriorAttemptAndFinalProjection()
    {
        var fixture = new ReportBehaviorFixture(new() { ["report-ctrf-filename"] = ["projection.json"] });
        DateTimeOffset epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var prior = new CapturedTestResult
        {
            Uid = "same",
            DisplayName = string.Empty,
            Status = "failed",
            Duration = TimeSpan.FromSeconds(2),
            RetryAttemptNumber = 1,
            IsSupersededRetryAttempt = true,
            StartTime = epoch,
            EndTime = epoch.AddSeconds(2),
            ErrorMessage = "expired",
            StackTrace = "at Prior()",
            Line = 17,
            RawStatus = "timedOut",
            ExceptionType = "TimeoutException",
            StandardOutput = "one\r\n\nlast\r",
            Attachments = [new CapturedAttachment { Name = "evidence", Path = "/owned/evidence.txt", ContentType = "text/plain", Description = "prior evidence" }],
        };
        var final = new CapturedTestResult
        {
            Uid = "same",
            DisplayName = string.Empty,
            Status = "passed",
            Duration = TimeSpan.FromTicks(-1),
            RetryAttemptNumber = 2,
            StartTime = epoch.AddSeconds(3),
            EndTime = epoch.AddSeconds(4),
            Namespace = "OnlyNamespace",
            FilePath = "src/test.cs",
            Line = 23,
            StandardError = "terminal\r",
            MethodName = "FinalMethod",
        };
        var engine = new CtrfReportEngine(new(
            fixture.FileSystem.Object, fixture.Module.Object, fixture.Environment.Object, fixture.Services.GetCommandLineOptions(),
            fixture.Configuration.Object, fixture.Clock.Object, fixture.Framework.Object, ReportBehaviorFixture.Start, 0, CancellationToken.None));

        (string path, string? warning) = await engine.GenerateReportAsync([prior, final]);

        Assert.AreEqual(Path.Combine("results", "projection.json"), path);
        Assert.IsNull(warning);
        JsonObject report = JsonNode.Parse(fixture.WrittenFiles[path].Text)!.AsObject();
        JsonObject test = Assert.IsInstanceOfType<JsonObject>(Assert.ContainsSingle(report["results"]!["tests"]!.AsArray()));
        Assert.AreEqual("same", test["testId"]!.GetValue<string>());
        Assert.IsTrue(Guid.TryParse(test["executionId"]!.GetValue<string>(), out _));
        Assert.IsTrue(Guid.TryParse(test["attemptId"]!.GetValue<string>(), out Guid finalAttempt));
        JsonObject retry = test["retryAttempts"]![0]!.AsObject();
        Assert.IsTrue(Guid.TryParse(retry["attemptId"]!.GetValue<string>(), out Guid priorAttempt));
        Assert.AreNotEqual(finalAttempt, priorAttempt);
        test.Remove("testId");
        test.Remove("executionId");
        test.Remove("attemptId");
        retry.Remove("attemptId");
        const string expected = """
            {
              "name":"same","status":"passed","duration":0,"start":3000,"stop":4000,
              "suite":["OnlyNamespace"],"filePath":"src/test.cs","line":23,"retries":1,
              "retryAttempts":[{
                "attempt":1,"status":"failed","duration":2000,"start":0,"stop":2000,
                "message":"expired","trace":"at Prior()","line":17,"stdout":["one","","last"],
                "attachments":[{"name":"evidence","contentType":"text/plain","path":"/owned/evidence.txt","extra":{"description":"prior evidence"}}],
                "extra":{"rawStatus":"timedOut","exceptionType":"TimeoutException"}
              }],
              "flaky":true,"stderr":["terminal"],"extra":{"uid":"same","method":"FinalMethod"}
            }
            """;
        Assert.AreEqual(JsonNode.Parse(expected)!.ToJsonString(), test.ToJsonString());
    }
}
