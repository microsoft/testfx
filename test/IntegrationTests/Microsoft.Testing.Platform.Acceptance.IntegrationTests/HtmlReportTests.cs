// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
[DoNotParallelize]
public class HtmlReportTests : AcceptanceTestBase<HtmlReportTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Html_WhenReportHtmlIsNotSpecified_HtmlReportIsNotGenerated(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // The HTML report is published as an in-process artifact; check the correct block.
        string outputPattern = """
  In process file artifacts produced:
    - .+?\.html
""";
        testHostResult.AssertOutputDoesNotMatchRegex(outputPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Html_WhenReportHtmlIsSpecified_HtmlReportIsGeneratedInDefaultLocation(string tfm)
    {
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string htmlPathPattern = Regex.Escape(testResultsPath + Path.DirectorySeparatorChar) + @".+?\.html";

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-html", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string outputPattern = $"""
  In process file artifacts produced:
    - {htmlPathPattern}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);

        Match match = Regex.Match(testHostResult.StandardOutput, htmlPathPattern);
        Assert.IsTrue(match.Success, $"HTML report path not found in output:\n{testHostResult.StandardOutput}");

        string htmlContent = File.ReadAllText(match.Value);
        Assert.Contains("<!DOCTYPE html>", htmlContent, "Generated file does not appear to be a valid HTML report.");
        Assert.Contains("id=\"mtp-data\"", htmlContent, "Generated HTML report does not contain embedded JSON data.");
    }

    [TestMethod]
    public async Task Html_WhenTestHostCrashes_RecoversCompletedResultsAndMarksReportIncomplete()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, TargetFrameworks.NetCurrent);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-html --report-html-filename crash-{{pid}}.html --results-directory \"{resultDirectory}\"",
            new() { ["CRASHPROCESS"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        string[] files = Directory.GetFiles(resultDirectory, "crash-*.html");
        Assert.HasCount(1, files);
        testHostResult.AssertOutputContains("Out of process file artifacts produced:");
        testHostResult.AssertOutputContains(files[0]);

        Match pidMatch = Regex.Match(testHostResult.StandardOutput, @"CRASHED_CHILD_PID=(\d+)");
        Assert.IsTrue(pidMatch.Success, testHostResult.StandardOutput);
        Assert.AreEqual($"crash-{pidMatch.Groups[1].Value}.html", Path.GetFileName(files[0]));

        string content = File.ReadAllText(files[0]);
        Assert.Contains("<!DOCTYPE html>", content);
        Assert.Contains("\"incomplete\":true", content);
        Assert.Contains("\"runStatus\":\"aborted\"", content);
        Assert.Contains("\"displayName\":\"PassingTest\"", content);
        Assert.DoesNotContain("\"displayName\":\"NeverReachedTest\"", content);
        Assert.Contains("Tests absent from this report did not necessarily pass.", content);
    }

    [TestMethod]
    public async Task Html_WhenTestsRunInParallel_DurationSummaryUsesWallClockTime()
    {
        string resultDirectory = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        string reportPath = Path.Combine(resultDirectory, "parallel.html");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, TargetFrameworks.NetCurrent);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-html --report-html-filename parallel.html --results-directory \"{resultDirectory}\"",
            new() { ["PARALLELTESTS"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        Assert.IsTrue(File.Exists(reportPath), $"Expected HTML report was not found at '{reportPath}'.");

        string htmlReport = File.ReadAllText(reportPath);
        const string htmlDataStart = "<script id=\"mtp-data\" type=\"application/json\">";
        const string htmlDataEnd = "</script>";
        int htmlDataStartIndex = htmlReport.IndexOf(htmlDataStart, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, htmlDataStartIndex, htmlReport);
        htmlDataStartIndex += htmlDataStart.Length;
        int htmlDataEndIndex = htmlReport.IndexOf(htmlDataEnd, htmlDataStartIndex, StringComparison.Ordinal);
        Assert.IsGreaterThan(htmlDataStartIndex, htmlDataEndIndex, htmlReport);

        using var htmlDocument = System.Text.Json.JsonDocument.Parse(
            htmlReport.Substring(htmlDataStartIndex, htmlDataEndIndex - htmlDataStartIndex));
        System.Text.Json.JsonElement root = htmlDocument.RootElement;
        System.Text.Json.JsonElement[] tests =
        [
            .. root.GetProperty("tests").EnumerateArray()
        ];
        DateTimeOffset[] testStartTimes = [.. tests.Select(test => test.GetProperty("startTime").GetDateTimeOffset())];
        DateTimeOffset[] testEndTimes = [.. tests.Select(test => test.GetProperty("endTime").GetDateTimeOffset())];
        double[] testDurations = [.. tests.Select(test => test.GetProperty("durationMs").GetDouble())];
        DateTimeOffset reportStartTime = root.GetProperty("startTime").GetDateTimeOffset();
        DateTimeOffset reportEndTime = root.GetProperty("endTime").GetDateTimeOffset();
        double summaryDuration = root.GetProperty("summary").GetProperty("totalDurationMs").GetDouble();
        double expectedSummaryDuration = (reportEndTime - reportStartTime).TotalMilliseconds;

        Assert.HasCount(2, testDurations);
        Assert.IsLessThan(testEndTimes.Min(), testStartTimes.Max(), "Expected the two test execution intervals to overlap.");
        Assert.IsLessThanOrEqualTo(
            1d,
            Math.Abs(expectedSummaryDuration - summaryDuration),
            $"Expected summary duration to match the report's wall-clock interval. Summary: {summaryDuration} ms; interval: {expectedSummaryDuration} ms; sum of tests: {testDurations.Sum()} ms.");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Html_WhenReportHtmlFilenameIsSpecified_HtmlReportIsGeneratedWithThatName(string tfm)
    {
        const string customFileName = "my-custom-report.html";
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string customFilePath = Path.Combine(testResultsPath, customFileName);
        string expectedFilePath = Regex.Escape(customFilePath);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-html --report-html-filename {customFileName}",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string outputPattern = $"""
  In process file artifacts produced:
    - {expectedFilePath}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);

        Assert.IsTrue(
            File.Exists(customFilePath),
            $"Expected custom HTML report file '{customFileName}' was not found in '{testResultsPath}'.");

        string htmlContent = File.ReadAllText(customFilePath);
        Assert.Contains("<!DOCTYPE html>", htmlContent, "Generated file does not appear to be a valid HTML report.");
        Assert.Contains("id=\"mtp-data\"", htmlContent, "Generated HTML report does not contain embedded JSON data.");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Html_WhenReportHtmlFilenameContainsPath_HtmlReportIsGeneratedInThatPath(string tfm)
    {
        string customFileName = Path.Combine("subdir", "report.html");
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string customFilePath = Path.Combine(testResultsPath, customFileName);
        string expectedFilePath = Regex.Escape(customFilePath);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-html --report-html-filename {customFileName}",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string outputPattern = $"""
  In process file artifacts produced:
    - {expectedFilePath}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);

        Assert.IsTrue(
            File.Exists(customFilePath),
            $"Expected custom HTML report file '{customFileName}' was not found in '{testResultsPath}'.");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Html_WhenReportHtmlFilenameIsSpecifiedWithoutReportHtml_ErrorIsDisplayed(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--report-html-filename report.html",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--report-html-filename' requires '--report-html' to be enabled");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string AssetName = "HtmlReportTest";

        private const string TestCode = """
#file HtmlReportTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Extensions.HtmlReport" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_, __) => new DummyTestFramework());
#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        builder.AddHtmlReportProvider();
#pragma warning restore TPEXP
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    private readonly TaskCompletionSource<bool> _parallelTestsReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _parallelTestsReadyCount;

    public string Uid => nameof(DummyTestFramework);
    public string Version => "2.0.0";
    public string DisplayName => nameof(DummyTestFramework);
    public string Description => nameof(DummyTestFramework);
    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        if (Environment.GetEnvironmentVariable("PARALLELTESTS") == "1")
        {
            await Task.WhenAll(
                RunParallelTestAsync(context, "parallel-test-1", "ParallelTest1"),
                RunParallelTestAsync(context, "parallel-test-2", "ParallelTest2"));
            context.Complete();
            return;
        }

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
            context.Request.Session.SessionUid,
            new TestNode()
            {
                Uid = "test-1",
                DisplayName = "PassingTest",
                Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
            }));
        if (Environment.GetEnvironmentVariable("CRASHPROCESS") == "1")
        {
            string journalPath = Environment.GetEnvironmentVariable("TESTINGPLATFORM_HTMLREPORT_JOURNAL")
                ?? throw new InvalidOperationException("HTML report recovery journal was not configured.");
            bool journalReady = false;
            for (int i = 0; i < 100; i++)
            {
                using FileStream stream = File.Open(journalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                if (reader.ReadLine() is not null && reader.ReadLine() is not null)
                {
                    journalReady = true;
                    break;
                }

                await Task.Delay(50);
            }

            if (!journalReady)
            {
                throw new TimeoutException("HTML report recovery journal did not contain the expected result before the crash deadline.");
            }

            Console.WriteLine($"CRASHED_CHILD_PID={System.Diagnostics.Process.GetCurrentProcess().Id}");
            await Console.Out.FlushAsync();
            Environment.FailFast("CRASHPROCESS");
        }

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
            context.Request.Session.SessionUid,
            new TestNode()
            {
                Uid = "test-2",
                DisplayName = "NeverReachedTest",
                Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
            }));

        context.Complete();
    }

    private async Task RunParallelTestAsync(ExecuteRequestContext context, string uid, string displayName)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        if (Interlocked.Increment(ref _parallelTestsReadyCount) == 2)
        {
            _parallelTestsReady.SetResult(true);
        }

        Task completedTask = await Task.WhenAny(_parallelTestsReady.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        if (completedTask != _parallelTestsReady.Task)
        {
            throw new TimeoutException("Parallel tests did not reach the rendezvous.");
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
        DateTimeOffset end = DateTimeOffset.UtcNow;

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
            context.Request.Session.SessionUid,
            new TestNode()
            {
                Uid = uid,
                DisplayName = displayName,
                Properties = new PropertyBag(
                    PassedTestNodeStateProperty.CachedInstance,
                    new TimingProperty(new TimingInfo(start, end, end - start))),
            }));
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
