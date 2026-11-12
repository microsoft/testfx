// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using OpenTelemetry;
using OpenTelemetry.Resources;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TestFxOpenTelemetryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(null, false, DisplayName = "SdkDisabledUnset")]
    [DataRow("", false, DisplayName = "SdkDisabledEmpty")]
    [DataRow("false", false, DisplayName = "SdkDisabledFalse")]
    [DataRow("true", true, DisplayName = "SdkDisabledTrue")]
    [DataRow("TRUE", true, DisplayName = "SdkDisabledUppercase")]
    [DataRow("1", false, DisplayName = "SdkDisabledNumeric")]
    [DataRow(" true ", false, DisplayName = "SdkDisabledWhitespace")]
    public void IsDisabled_OnlyTrueTurnsRegistrationOff(string? value, bool expected)
        => Assert.AreEqual(expected, TestFxOpenTelemetry.IsDisabled(value));

    [TestMethod]
    public void Export_PreservesHierarchyTimingStepsAndFailureDetails()
    {
        string sourceName = $"testfx-export-{Guid.NewGuid():N}";
        using var source = new ActivitySource(sourceName, "1.2.3");
        using var listener = new ActivityListener
        {
            ShouldListenTo = activitySource => activitySource.Name == sourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        var upstream = new ActivityContext(
            ActivityTraceId.CreateFromString("11111111111111111111111111111111".AsSpan()),
            ActivitySpanId.CreateFromString("2222222222222222".AsSpan()),
            ActivityTraceFlags.Recorded,
            "vendor=parent",
            isRemote: true);
        var link = new ActivityContext(
            ActivityTraceId.CreateFromString("33333333333333333333333333333333".AsSpan()),
            ActivitySpanId.CreateFromString("4444444444444444".AsSpan()),
            ActivityTraceFlags.Recorded,
            "vendor=link",
            isRemote: true);
        DateTimeOffset start = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero).AddTicks(123);
        using Activity? parent = source.StartActivity("test-run", ActivityKind.Internal, upstream, startTime: start.AddSeconds(-1));
        Assert.IsNotNull(parent);
        using Activity? activity = source.StartActivity(
            "test-execution",
            ActivityKind.Client,
            parent.Context,
            links: [new ActivityLink(link, new ActivityTagsCollection { ["link.kind"] = "execution" })],
            startTime: start);
        Assert.IsNotNull(activity);
        activity.DisplayName = "row \"quoted\"\nsecond line";
        activity.SetTag("test.case.id", "row-42");
        activity.SetTag("test.case.result.status", "fail");
        activity.SetTag("test.case.duration_ms", 9.5);
        activity.SetTag("test.step.1.duration", 2.25);
        activity.SetTag("test.step.1.description", "initialize");
        activity.SetTag("test.step.2.duration", 7.25);
        activity.SetTag("test.step.2.description", "test body");
        activity.SetTag("test.metadata.values", new[] { "one", "two" });
        activity.SetTag("test.metadata.integer", long.MaxValue);
        activity.SetTag("test.metadata.flag", true);
        activity.AddEvent(new ActivityEvent("exception", start.AddTicks(12), new ActivityTagsCollection
        {
            ["exception.type"] = "OwnedTestException",
            ["exception.message"] = "expected failure",
        }));
        activity.SetStatus(ActivityStatusCode.Error, "expected failure");
        activity.SetEndTime(start.UtcDateTime.AddTicks(1234567));
        activity.Stop();
        parent.SetEndTime(start.UtcDateTime.AddSeconds(1));
        parent.Stop();

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        using (var exporter = new TestFxOpenTelemetry.TraceFileExporter(writer, ResourceBuilder.CreateEmpty().Build(), errors))
        {
            Assert.AreEqual(ExportResult.Success, exporter.Export(new Batch<Activity>([activity, parent], 2)));
        }

        JsonElement[] records = ParseRecords(writer.ToString());
        Assert.HasCount(4, records);
        JsonElement span = records[1];
        Assert.AreEqual("span", span.GetProperty("recordType").GetString());
        Assert.AreEqual(parent.TraceId.ToHexString(), span.GetProperty("traceId").GetString());
        Assert.AreEqual(activity.SpanId.ToHexString(), span.GetProperty("spanId").GetString());
        Assert.AreEqual(records[2].GetProperty("spanId").GetString(), span.GetProperty("parentSpanId").GetString());
        Assert.AreEqual("vendor=parent", span.GetProperty("traceState").GetString());
        Assert.AreEqual(1, span.GetProperty("traceFlags").GetInt32());
        Assert.AreEqual("row \"quoted\"\nsecond line", span.GetProperty("name").GetString());
        Assert.AreEqual("test-execution", span.GetProperty("operationName").GetString());
        Assert.AreEqual(sourceName, span.GetProperty("sourceName").GetString());
        Assert.AreEqual("1.2.3", span.GetProperty("sourceVersion").GetString());
        Assert.AreEqual("Client", span.GetProperty("kind").GetString());
        Assert.AreEqual(start.UtcDateTime, span.GetProperty("startTimeUtc").GetDateTime());
        Assert.AreEqual(start.UtcDateTime.AddTicks(1234567), span.GetProperty("endTimeUtc").GetDateTime());
        Assert.AreEqual(1234567L, span.GetProperty("durationTicks").GetInt64());
        Assert.AreEqual("Error", span.GetProperty("status").GetString());
        Assert.AreEqual("expected failure", span.GetProperty("statusDescription").GetString());
        Dictionary<string, JsonElement> attributes = Attributes(span);
        Assert.AreEqual("row-42", attributes["test.case.id"].GetString());
        Assert.AreEqual("fail", attributes["test.case.result.status"].GetString());
        Assert.AreEqual(9.5, attributes["test.case.duration_ms"].GetDouble());
        Assert.AreEqual(2.25, attributes["test.step.1.duration"].GetDouble());
        Assert.AreEqual("initialize", attributes["test.step.1.description"].GetString());
        Assert.AreEqual(7.25, attributes["test.step.2.duration"].GetDouble());
        Assert.AreEqual("test body", attributes["test.step.2.description"].GetString());
        Assert.AreSequenceEqual(new[] { "one", "two" }, attributes["test.metadata.values"].EnumerateArray().Select(value => value.GetString()));
        Assert.AreEqual(long.MaxValue, attributes["test.metadata.integer"].GetInt64());
        Assert.IsTrue(attributes["test.metadata.flag"].GetBoolean());
        JsonElement exception = Assert.ContainsSingle(span.GetProperty("events").EnumerateArray());
        Assert.AreEqual("exception", exception.GetProperty("name").GetString());
        Assert.AreEqual(start.UtcDateTime.AddTicks(12), exception.GetProperty("timestampUtc").GetDateTime());
        Assert.AreEqual("OwnedTestException", Attributes(exception)["exception.type"].GetString());
        Assert.AreEqual("expected failure", Attributes(exception)["exception.message"].GetString());
        JsonElement exportedLink = Assert.ContainsSingle(span.GetProperty("links").EnumerateArray());
        Assert.AreEqual("33333333333333333333333333333333", exportedLink.GetProperty("traceId").GetString());
        Assert.AreEqual("4444444444444444", exportedLink.GetProperty("spanId").GetString());
        Assert.AreEqual("vendor=link", exportedLink.GetProperty("traceState").GetString());
        Assert.AreEqual(1, exportedLink.GetProperty("traceFlags").GetInt32());
        Assert.IsTrue(exportedLink.GetProperty("isRemote").GetBoolean());
        Assert.AreEqual("execution", Attributes(exportedLink)["link.kind"].GetString());
        Assert.AreEqual("complete", records[3].GetProperty("status").GetString());
        Assert.AreEqual(2L, records[3].GetProperty("spanCount").GetInt64());
        Assert.AreEqual(string.Empty, errors.ToString());
    }

    [TestMethod]
    public void Export_OmitsCapturedOutputWithoutLosingOtherAttributes()
    {
        using Activity activity = new Activity("output-policy").SetIdFormat(ActivityIdFormat.W3C).Start();
        activity.SetTag("test.output.stdout", "sensitive stdout");
        activity.SetTag("test.output.stderr", "sensitive stderr");
        activity.SetTag("test.stdout", "legacy stdout");
        activity.SetTag("test.stderr", "legacy stderr");
        activity.SetTag("test.case.name", "retained name");
        activity.Stop();
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        using (var exporter = new TestFxOpenTelemetry.TraceFileExporter(writer, ResourceBuilder.CreateEmpty().Build(), errors))
        {
            Assert.AreEqual(ExportResult.Success, exporter.Export(new Batch<Activity>([activity], 1)));
        }

        JsonElement[] records = ParseRecords(writer.ToString());
        Assert.HasCount(3, records);
        JsonElement span = records[1];
        Dictionary<string, JsonElement> attributes = Attributes(span);
        Assert.HasCount(1, attributes);
        Assert.AreEqual("retained name", attributes["test.case.name"].GetString());
        Assert.DoesNotContain("sensitive stdout", writer.ToString());
        Assert.DoesNotContain("legacy stderr", writer.ToString());
    }

    [TestMethod]
    public async Task Export_ConcurrentBatchesProduceCompleteNoninterleavedRecords()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        using (var exporter = new TestFxOpenTelemetry.TraceFileExporter(writer, ResourceBuilder.CreateEmpty().Build(), errors))
        {
            Task[] exports = Enumerable.Range(0, 64).Select(index => Task.Run(
                () =>
                {
                    using Activity activity = new Activity($"execution-{index}").SetIdFormat(ActivityIdFormat.W3C).Start();
                    activity.SetTag("test.case.id", index);
                    activity.Stop();
                    Assert.AreEqual(ExportResult.Success, exporter.Export(new Batch<Activity>([activity], 1)));
                },
                TestContext.CancellationToken)).ToArray();
            await Task.WhenAll(exports);
        }

        JsonElement[] records = ParseRecords(writer.ToString());
        JsonElement[] spans = records.Where(record => record.GetProperty("recordType").GetString() == "span").ToArray();
        Assert.HasCount(66, records);
        Assert.HasCount(64, spans);
        Assert.HasCount(64, spans.Select(span => span.GetProperty("spanId").GetString()).Distinct());
        foreach (JsonElement span in spans)
        {
            int index = Attributes(span)["test.case.id"].GetInt32();
            Assert.AreEqual($"execution-{index}", span.GetProperty("name").GetString());
        }

        Assert.AreEqual("complete", records[65].GetProperty("status").GetString());
        Assert.AreEqual(64L, records[65].GetProperty("spanCount").GetInt64());
        Assert.AreEqual(0L, records[65].GetProperty("failedExports").GetInt64());
        Assert.AreEqual(string.Empty, errors.ToString());
    }

    [TestMethod]
    public void FileExporters_SharedDirectoryNeverOverwriteAndReleaseFiles()
    {
        string directory = Path.Combine(TestContext.TestTempDirectory!, "traces");
        Resource resource = ResourceBuilder.CreateEmpty().AddAttributes([new("test.assembly.name", "owned-module")]).Build();
        using Activity activity = new Activity("retained-before-disposal").SetIdFormat(ActivityIdFormat.W3C).Start();
        activity.Stop();
        using (var first = new TestFxOpenTelemetry.TraceFileExporter(directory, resource))
        using (var second = new TestFxOpenTelemetry.TraceFileExporter(directory, resource))
        {
            Assert.AreEqual(ExportResult.Success, first.Export(new Batch<Activity>([activity], 1)));
            Assert.AreEqual(ExportResult.Success, second.Export(new Batch<Activity>([activity], 1)));
            string[] liveFiles = Directory.GetFiles(directory, "*.jsonl");
            Assert.HasCount(2, liveFiles);
            foreach (string file in liveFiles)
            {
                using var liveStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var liveReader = new StreamReader(liveStream);
                JsonElement[] partial = ParseRecords(liveReader.ReadToEnd());
                Assert.HasCount(2, partial);
                Assert.AreEqual("span", partial[1].GetProperty("recordType").GetString());
            }
        }

        string[] files = Directory.GetFiles(directory, "*.jsonl");
        Assert.HasCount(2, files);
        var runIds = new HashSet<string>();
        foreach (string file in files)
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
            using var reader = new StreamReader(stream);
            JsonElement[] records = ParseRecords(reader.ReadToEnd());
            Assert.HasCount(3, records);
            Assert.AreEqual("testfx-otel-jsonl", records[0].GetProperty("format").GetString());
            Assert.AreEqual(1, records[0].GetProperty("schemaVersion").GetInt32());
            Assert.AreEqual("owned-module", Attributes(records[0])["test.assembly.name"].GetString());
            Assert.IsTrue(runIds.Add(records[0].GetProperty("runId").GetString()!));
            Assert.AreEqual("complete", records[2].GetProperty("status").GetString());
            Assert.AreEqual(1L, records[2].GetProperty("spanCount").GetInt64());
        }
    }

    [TestMethod]
    public void Dispose_EmptyExportWritesExactlyOneCompletion()
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        var exporter = new TestFxOpenTelemetry.TraceFileExporter(writer, ResourceBuilder.CreateEmpty().Build(), errors);
        Assert.AreEqual(ExportResult.Success, exporter.Export(new Batch<Activity>([], 0)));
        exporter.Dispose();
        exporter.Dispose();

        JsonElement[] records = ParseRecords(writer.ToString());
        Assert.HasCount(2, records);
        Assert.AreEqual("resource", records[0].GetProperty("recordType").GetString());
        Assert.AreEqual("completion", records[1].GetProperty("recordType").GetString());
        Assert.AreEqual("complete", records[1].GetProperty("status").GetString());
        Assert.AreEqual(0L, records[1].GetProperty("spanCount").GetInt64());
        Assert.AreEqual(0L, records[1].GetProperty("failedExports").GetInt64());
    }

    [TestMethod]
    [DataRow(false, DisplayName = "WriteFailure")]
    [DataRow(true, DisplayName = "FlushFailure")]
    public void Export_WriteFailureIsReportedAndCompletionCannotClaimSuccess(bool failFlush)
    {
        using var writer = new FailingWriter();
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        using Activity activity = new Activity("owned-failed-export").SetIdFormat(ActivityIdFormat.W3C).Start();
        activity.Stop();
        using (var exporter = new TestFxOpenTelemetry.TraceFileExporter(writer, ResourceBuilder.CreateEmpty().Build(), errors))
        {
            writer.FailWrite = !failFlush;
            writer.FailFlush = failFlush;
            Assert.AreEqual(ExportResult.Failure, exporter.Export(new Batch<Activity>([activity], 1)));
            Assert.AreEqual(ExportResult.Failure, exporter.Export(new Batch<Activity>([activity], 1)));
            writer.FailWrite = false;
            writer.FailFlush = false;
        }

        JsonElement completion = ParseRecords(writer.ToString()).Last();
        Assert.AreEqual("failed", completion.GetProperty("status").GetString());
        Assert.AreEqual(2L, completion.GetProperty("failedExports").GetInt64());
        Assert.Contains("TestFx OpenTelemetry trace export failed", errors.ToString());
        Assert.Contains("owned I/O failure", errors.ToString());
    }

    [TestMethod]
    public void Dispose_WriteFailureReportsErrorWithoutClaimingCompletion()
    {
        using var writer = new FailingWriter();
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        var exporter = new TestFxOpenTelemetry.TraceFileExporter(writer, ResourceBuilder.CreateEmpty().Build(), errors);
        writer.FailWrite = true;
        exporter.Dispose();

        Assert.HasCount(1, ParseRecords(writer.ToString()));
        Assert.Contains("owned I/O failure", errors.ToString());
    }

    [TestMethod]
    public void Constructor_InvalidDirectoryFailsExplicitly()
    {
        string path = Path.Combine(TestContext.TestTempDirectory!, "not-a-directory");
        File.WriteAllText(path, "owned file");

        Assert.Throws<IOException>(() => new TestFxOpenTelemetry.TraceFileExporter(path, ResourceBuilder.CreateEmpty().Build()));
    }

    [TestMethod]
    public void GetExecutionAttributes_AllowlistPreservesProvenanceAndDoesNotCaptureSecrets()
    {
        Dictionary<string, string> environment = new()
        {
            ["SYSTEM_JOBNAME"] = "Windows_Release",
            ["SYSTEM_JOBATTEMPT"] = "3",
            ["SYSTEM_STAGEATTEMPT"] = "2",
            ["AGENT_NAME"] = "agent-7",
            ["AGENT_OS"] = "Windows_NT",
            ["IMAGEOS"] = "windows-2026",
            ["IMAGEVERSION"] = "image-42",
            ["BUILD_SOURCEBRANCH"] = "refs/heads/main",
            ["TESTINGPLATFORM_LOGICAL_RUN_ID"] = "logical-run",
            ["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"] = "module-execution",
            ["TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER"] = "4",
            ["SYSTEM_ACCESSTOKEN"] = "must-not-be-read",
        };
        var reads = new List<string>();
        var attributes = TestFxOpenTelemetry.GetExecutionAttributes(variable =>
        {
            reads.Add(variable);
            return environment.TryGetValue(variable, out string? value) ? value : null;
        }).ToDictionary(pair => pair.Key, pair => pair.Value);

        Assert.AreEqual("Windows_Release", attributes["testfx.ci.job.name"]);
        Assert.AreEqual("3", attributes["testfx.ci.job.attempt"]);
        Assert.AreEqual("2", attributes["testfx.ci.stage.attempt"]);
        Assert.AreEqual("agent-7", attributes["testfx.ci.agent.name"]);
        Assert.AreEqual("Windows_NT", attributes["testfx.ci.agent.os"]);
        Assert.AreEqual("windows-2026", attributes["testfx.ci.image.name"]);
        Assert.AreEqual("image-42", attributes["testfx.ci.image.version"]);
        Assert.AreEqual("refs/heads/main", attributes["testfx.vcs.ref"]);
        Assert.AreEqual("logical-run", attributes["testfx.run.logical_id"]);
        Assert.AreEqual("module-execution", attributes["testfx.run.execution_id"]);
        Assert.AreEqual("4", attributes["testfx.run.attempt"]);
        Assert.IsFalse(Assert.IsInstanceOfType<bool>(attributes["testfx.trace.capture_test_output"]));
        Assert.AreEqual("synchronous-jsonl-v1", attributes["testfx.trace.collection"]);
        Assert.DoesNotContain("SYSTEM_ACCESSTOKEN", reads);
        Assert.DoesNotContain("must-not-be-read", attributes.Values);
        Assert.IsNotEmpty(Assert.IsInstanceOfType<string>(attributes["testfx.target.framework"]));
        Assert.IsNotEmpty(Assert.IsInstanceOfType<string>(attributes["testfx.build.configuration"]));
        Assert.IsNotEmpty(Assert.IsInstanceOfType<string>(attributes["testfx.sdk.version"]));
    }

#if NET
    [TestMethod]
    public void RepositoryTestHost_RemovesAffectedTestCollectionEnvironment()
    {
        var startInfo = new ProcessStartInfo();
        string[] variables =
        [
            "DOTNET_CLI_ENABLE_AFFECTED_TESTS",
            "DOTNET_CLI_TEST_AFFECTED_TESTS_MODE",
            "MTP_AFFECTED_TESTS_CHILD_PROCESS",
            "MTP_AFFECTED_TESTS_BATCH_FEEDBACK_ID",
            "MTP_AFFECTED_TESTS_BATCH_FEEDBACK_PATH",
        ];
        foreach (string variable in variables)
        {
            startInfo.Environment[variable] = "inherited-parent-context";
        }

        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment[TestFxOpenTelemetry.TraceDirectoryVariable] = "owned-traces";

        RemoveAffectedTestEnvironment(startInfo);

        foreach (string variable in variables)
        {
            Assert.DoesNotContain(variable, startInfo.Environment.Keys);
        }

        Assert.AreEqual("1", startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"]);
        Assert.AreEqual("owned-traces", startInfo.Environment[TestFxOpenTelemetry.TraceDirectoryVariable]);
    }

    [TestMethod]
    [DataRow("enabled")]
    [DataRow("disabled")]
    [DataRow("no-directory")]
    [DataRow("empty")]
    [DataRow("help")]
    [DataRow("info")]
    [Timeout(90_000, CooperativeCancellation = true)]
    public async Task RepositoryTestHost_PreservesResultsAndPersistsOnlyWhenEnabled(string scenario)
    {
        string directory = Path.Combine(TestContext.TestTempDirectory!, "host-traces");
        string module = typeof(TestFxOpenTelemetryTests).Assembly.Location;
        string dotnet = Path.GetFullPath(Path.Combine(
            RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..",
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet"));
        var startInfo = new ProcessStartInfo
        {
            FileName = dotnet,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(module);
        if (scenario is "help" or "info")
        {
            startInfo.ArgumentList.Add($"--{scenario}");
        }
        else
        {
            startInfo.ArgumentList.Add("--filter");
            startInfo.ArgumentList.Add(scenario == "empty"
                ? "FullyQualifiedName=NoMatchingTraceValidationTest"
                : $"FullyQualifiedName~{nameof(IsDisabled_OnlyTrueTurnsRegistrationOff)}");
        }

        startInfo.ArgumentList.Add("--ansi");
        startInfo.ArgumentList.Add("off");
        startInfo.ArgumentList.Add("--progress");
        startInfo.ArgumentList.Add("off");
        RemoveAffectedTestEnvironment(startInfo);
        foreach (string key in startInfo.Environment.Keys.Where(key => key.StartsWith("TESTINGPLATFORM_", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            startInfo.Environment.Remove(key);
        }

        startInfo.Environment[TestFxOpenTelemetry.TraceDirectoryVariable] = scenario == "no-directory" ? null : directory;
        startInfo.Environment["OTEL_SDK_DISABLED"] = scenario == "disabled" ? "true" : null;
        startInfo.Environment["OTEL_SERVICE_NAME"] = null;
        startInfo.Environment["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["TF_BUILD"] = "true";
        startInfo.Environment["GITHUB_ACTIONS"] = null;
        startInfo.Environment["GITLAB_CI"] = null;
        startInfo.Environment["JENKINS_URL"] = null;
        startInfo.Environment["BUILD_BUILDID"] = "trace-validation-build";
        startInfo.Environment["BUILD_SOURCEVERSION"] = "trace-validation-revision";
        startInfo.Environment["BUILD_SOURCEBRANCH"] = "refs/heads/trace-validation";
        startInfo.Environment["SYSTEM_JOBNAME"] = "TraceValidation";
        startInfo.Environment["SYSTEM_JOBATTEMPT"] = "3";
        startInfo.Environment["TESTINGPLATFORM_LOGICAL_RUN_ID"] = "validation-logical";
        startInfo.Environment["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"] = "validation-module";
        startInfo.Environment["TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER"] = "2";
        using var process = new Process { StartInfo = startInfo };
        Assert.IsTrue(process.Start());
        Task<string> output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        var cleanupLimit = TimeSpan.FromSeconds(30);
        try
        {
            await process.WaitForExitAsync(TestContext.CancellationToken);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(cleanupLimit, CancellationToken.None);
            await Task.WhenAll(output, error).WaitAsync(cleanupLimit, CancellationToken.None);
        }

        int expectedExit = scenario == "empty" ? 8 : 0;
        Assert.AreEqual(expectedExit, process.ExitCode, $"{await output}{await error}");
        if (scenario is "disabled" or "no-directory")
        {
            Assert.IsFalse(Directory.Exists(directory));
            return;
        }

        string file = Assert.ContainsSingle(Directory.GetFiles(directory, "*.jsonl"));
        JsonElement[] records = ParseRecords(File.ReadAllText(file));
        Dictionary<string, JsonElement> resource = Attributes(records[0]);
        Assert.AreEqual("Microsoft.Testing.Extensions.UnitTests", resource["test.assembly.name"].GetString());
        Assert.AreEqual("trace-validation-build", resource["cicd.pipeline.run.id"].GetString());
        Assert.AreEqual("trace-validation-revision", resource["vcs.ref.head.revision"].GetString());
        Assert.AreEqual("refs/heads/trace-validation", resource["testfx.vcs.ref"].GetString());
        Assert.AreEqual("TraceValidation", resource["testfx.ci.job.name"].GetString());
        Assert.AreEqual("3", resource["testfx.ci.job.attempt"].GetString());
        Assert.AreEqual("validation-logical", resource["testfx.run.logical_id"].GetString());
        Assert.AreEqual("validation-module", resource["testfx.run.execution_id"].GetString());
        Assert.AreEqual("2", resource["testfx.run.attempt"].GetString());
        string? sdkVersion = resource["testfx.sdk.version"].GetString();
        Assert.IsNotNull(sdkVersion);
        Assert.IsNotEmpty(sdkVersion);
        Assert.AreEqual("complete", records.Last().GetProperty("status").GetString());
        JsonElement[] spans = records.Where(record => record.GetProperty("recordType").GetString() == "span").ToArray();
        Assert.AreEqual(spans.Length, records.Last().GetProperty("spanCount").GetInt64());
        if (scenario is "help" or "info")
        {
            Assert.DoesNotContain("test.case.id", spans.SelectMany(span => Attributes(span).Keys));
            Assert.DoesNotContain("test.run.exit_code", spans.SelectMany(span => Attributes(span).Keys));
            JsonElement builder = Assert.ContainsSingle(spans.Where(span => span.GetProperty("operationName").GetString() == "TestHostBuilder"));
            Assert.AreEqual("InformativeCommandLineHost", Attributes(builder)["HostType"].GetString());
            return;
        }

        JsonElement run = Assert.ContainsSingle(spans.Where(span => Attributes(span).ContainsKey("test.run.exit_code")));
        Assert.AreEqual(expectedExit, Attributes(run)["test.run.exit_code"].GetInt32());
        Assert.AreEqual(expectedExit == 0 ? "Ok" : "Error", run.GetProperty("status").GetString());
        JsonElement[] tests = spans.Where(span => Attributes(span).ContainsKey("test.case.id")).ToArray();
        if (scenario == "empty")
        {
            Assert.IsEmpty(tests);
        }
        else
        {
            string[] names =
            [
                "SdkDisabledUnset", "SdkDisabledEmpty", "SdkDisabledFalse", "SdkDisabledTrue",
                "SdkDisabledUppercase", "SdkDisabledNumeric", "SdkDisabledWhitespace",
            ];
            Assert.HasCount(7, tests);
            Assert.HasCount(7, tests.Select(test => Attributes(test)["test.case.id"].GetString()).Distinct());
            Assert.AreSequenceEqual(
                names.Order(StringComparer.Ordinal),
                tests.Select(test => Attributes(test)["test.case.name"].GetString()).Order(StringComparer.Ordinal));
            foreach (JsonElement test in tests)
            {
                Assert.AreEqual("pass", Attributes(test)["test.case.result.status"].GetString());
                Assert.AreEqual(run.GetProperty("traceId").GetString(), test.GetProperty("traceId").GetString());
            }
        }
    }

    private static void RemoveAffectedTestEnvironment(ProcessStartInfo startInfo)
    {
        startInfo.Environment.Remove("DOTNET_CLI_ENABLE_AFFECTED_TESTS");
        startInfo.Environment.Remove("DOTNET_CLI_TEST_AFFECTED_TESTS_MODE");
        startInfo.Environment.Remove("MTP_AFFECTED_TESTS_CHILD_PROCESS");
        startInfo.Environment.Remove("MTP_AFFECTED_TESTS_BATCH_FEEDBACK_ID");
        startInfo.Environment.Remove("MTP_AFFECTED_TESTS_BATCH_FEEDBACK_PATH");
    }
#endif

    private static JsonElement[] ParseRecords(string text)
        => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line =>
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.Clone();
        }).ToArray();

    private static Dictionary<string, JsonElement> Attributes(JsonElement record)
        => record.GetProperty("attributes").EnumerateArray().ToDictionary(
            attribute => attribute.GetProperty("key").GetString()!,
            attribute => attribute.GetProperty("value"));

    private sealed class FailingWriter : StringWriter
    {
        public bool FailWrite { get; set; }

        public bool FailFlush { get; set; }

        public override void WriteLine(string? value)
        {
            if (FailWrite)
            {
                throw new IOException("owned I/O failure");
            }

            base.WriteLine(value);
        }

        public override void Flush()
        {
            if (FailFlush)
            {
                throw new IOException("owned I/O failure");
            }

            base.Flush();
        }
    }
}
