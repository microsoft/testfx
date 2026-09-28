// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class OpenTelemetryActivityTopologyTests : AcceptanceTestBase<OpenTelemetryActivityTopologyTests.TestAssetFixture>
{
    private const string AssetName = "MSTestOpenTelemetryActivityTopology";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworksToTest))]
    public async Task MSTestSdk_ApplicationOwnedOpenTelemetry_UsesOneCanonicalTestActivity(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult result = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContains("[MSTEST-OTEL-CANONICAL] one-span-per-test");
        result.AssertOutputContains($"[MSTEST-OTEL-TFM] {tfm}");
        result.AssertOutputContains("[MSTEST-OTEL-AMBIENT] canonical-test");
        result.AssertOutputContains("[MSTEST-OTEL-HTTP] child-of-canonical-test");
        result.AssertOutputContains("[MSTEST-OTEL-PARALLEL] isolated");
        result.AssertOutputContains("[MSTEST-OTEL-OCCURRENCES] retries-and-data-rows");
        result.AssertOutputContains("[MSTEST-OTEL-FIXTURE-CONTEXT] no-completed-span-leak");
        result.AssertOutputContains("[MSTEST-OTEL-METRIC] test.run.duration");
        result.AssertOutputContains("[MSTEST-OTEL-RESOURCE] service.name=mstest-otel-characterization");

        foreach (string line in result.StandardOutput.Split(Environment.NewLine)
            .Where(line => line.StartsWith("[MSTEST-OTEL-TOPOLOGY]", StringComparison.Ordinal)))
        {
            Console.WriteLine($"{tfm}: {line}");
        }
    }

    public TestContext TestContext { get; set; }

    public static IEnumerable<object[]> TargetFrameworksToTest { get; } =
    [
        ["net8.0"],
        ["net9.0"],
    ];

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file Directory.Packages.props
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>

#file MSTestOpenTelemetryActivityTopology.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <LangVersion>preview</LangVersion>
    <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
    <EnableMicrosoftTestingExtensionsCodeCoverage>false</EnableMicrosoftTestingExtensionsCodeCoverage>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="$MicrosoftExtensionsHostingVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.OpenTelemetry" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="OpenTelemetry" Version="$OpenTelemetryVersion$" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="$OpenTelemetryVersion$" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.19.0" />
  </ItemGroup>
</Project>

#file Program.cs
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

[assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)]

internal static class Program
{
    private const string ApplicationServiceName = "mstest-otel-characterization";

    public static async Task<int> Main(string[] args)
    {
        var activityExporter = new CapturingActivityExporter();
        var metricExporter = new CapturingMetricExporter();
        HostApplicationBuilder hostBuilder = Host.CreateApplicationBuilder(args);
        hostBuilder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ApplicationServiceName)
                .AddTestingPlatformTestResource()
                .AddTestingPlatformCIResource())
            .WithTracing(tracing => tracing
                .AddSource(TelemetryProbe.SourceName)
                .AddTestingPlatformInstrumentation()
                .AddHttpClientInstrumentation()
                .AddProcessor(new SimpleActivityExportProcessor(activityExporter)))
            .WithMetrics(metrics => metrics
                .AddTestingPlatformInstrumentation()
                .AddReader(new PeriodicExportingMetricReader(metricExporter, exportIntervalMilliseconds: 10)));

        using IHost host = hostBuilder.Build();
        await host.StartAsync();

        using var applicationActivitySource = new ActivitySource(TelemetryProbe.SourceName);
        TelemetryProbe.Initialize(applicationActivitySource);
        using Activity applicationRoot = applicationActivitySource.StartActivity("application-test-run")
            ?? throw new InvalidOperationException("The application-owned provider did not subscribe to its activity source.");

        ITestApplicationBuilder testBuilder = await TestApplication.CreateBuilderAsync(args);
        testBuilder.AddMSTest(() => [Assembly.GetExecutingAssembly()]);
        testBuilder.AddTestingPlatformDiagnostics();

        int exitCode;
        using (ITestApplication testApplication = await testBuilder.BuildAsync())
        {
            exitCode = await testApplication.RunAsync();
        }

        applicationRoot.Stop();
        ServiceProviderServiceExtensions.GetRequiredService<TracerProvider>(host.Services).ForceFlush();
        ServiceProviderServiceExtensions.GetRequiredService<MeterProvider>(host.Services).ForceFlush();
        ActivityTopologyVerifier.Verify(activityExporter.Snapshot());
        ResourceVerifier.Verify(activityExporter.GetCapturedResource(), "trace", ApplicationServiceName);
        ResourceVerifier.Verify(metricExporter.GetCapturedResource(), "metric", ApplicationServiceName);
        if (!metricExporter.Contains("test.run.duration"))
        {
            throw new InvalidOperationException("The application-owned meter provider did not export test.run.duration.");
        }

        Console.WriteLine("[MSTEST-OTEL-CANONICAL] one-span-per-test");
        Console.WriteLine($"[MSTEST-OTEL-TFM] {GetTargetFrameworkMoniker()}");
        Console.WriteLine("[MSTEST-OTEL-AMBIENT] canonical-test");
        Console.WriteLine("[MSTEST-OTEL-HTTP] child-of-canonical-test");
        Console.WriteLine("[MSTEST-OTEL-PARALLEL] isolated");
        Console.WriteLine("[MSTEST-OTEL-OCCURRENCES] retries-and-data-rows");
        Console.WriteLine("[MSTEST-OTEL-FIXTURE-CONTEXT] no-completed-span-leak");
        Console.WriteLine("[MSTEST-OTEL-METRIC] test.run.duration");
        Console.WriteLine($"[MSTEST-OTEL-RESOURCE] service.name={ApplicationServiceName}");

        await host.StopAsync();
        return exitCode;
    }

    private static string GetTargetFrameworkMoniker()
        => AppContext.TargetFrameworkName?.Replace(".NETCoreApp,Version=v", "net", StringComparison.Ordinal).ToLowerInvariant()
            ?? throw new InvalidOperationException("The target framework name is unavailable.");
}

internal static class ActivityTopologyVerifier
{
    private const string MtpSourceName = "Microsoft.Testing.Platform";

    public static void Verify(ActivitySnapshot[] activities)
    {
        try
        {
            ActivitySnapshot applicationRoot = Single(activities, TelemetryProbe.SourceName, "application-test-run");
            ActivitySnapshot builder = Single(activities, MtpSourceName, "TestHostBuilder");
            ActivitySnapshot testHost = Single(activities, MtpSourceName, "TestHost");
            ActivitySnapshot run = Single(activities, MtpSourceName, "Run");
            ActivitySnapshot testFrameworkInvoker = Single(activities, MtpSourceName, "TestFrameworkInvoker");
            ActivitySnapshot executeTestRequest = Single(activities, MtpSourceName, "ExecuteTestRequest");
            ActivitySnapshot testFramework = Single(activities, MtpSourceName, "TestFramework");
            ActivitySnapshot assemblyInitialize = Single(activities, MtpSourceName, "MSTest.AssemblyInitialize");
            ActivitySnapshot assemblyCleanup = Single(activities, MtpSourceName, "MSTest.AssemblyCleanup");
            ActivitySnapshot classInitialize = Single(activities, MtpSourceName, "MSTest.ClassInitialize");
            ActivitySnapshot classCleanup = Single(activities, MtpSourceName, "MSTest.ClassCleanup");
            ActivitySnapshot[] testInitialize = Multiple(activities, MtpSourceName, "MSTest.TestInitialize", expectedCount: 2);
            ActivitySnapshot[] testCleanup = Multiple(activities, MtpSourceName, "MSTest.TestCleanup", expectedCount: 2);
            ActivitySnapshot firstTest = SingleResult(activities, nameof(ParallelActivityTests.FirstTest));
            ActivitySnapshot secondTest = SingleResult(activities, nameof(ParallelActivityTests.SecondTest));
            ActivitySnapshot firstCustom = Single(activities, TelemetryProbe.SourceName, "custom-first");
            ActivitySnapshot secondCustom = Single(activities, TelemetryProbe.SourceName, "custom-second");
            ActivitySnapshot firstHttp = SingleChild(activities, "System.Net.Http", firstTest.SpanId);
            ActivitySnapshot secondHttp = SingleChild(activities, "System.Net.Http", secondTest.SpanId);
            ActivitySnapshot retry = SingleResult(activities, nameof(OccurrenceIdentityTests.RetryTest));
            ActivitySnapshot folded = SingleResult(activities, nameof(OccurrenceIdentityTests.FoldedDataRows));
            ActivitySnapshot[] unfolded = ResultsForMethod(activities, nameof(OccurrenceIdentityTests.UnfoldedDataRows), expectedCount: 2);
            ActivitySnapshot skipped = SingleResult(activities, nameof(OccurrenceIdentityTests.SkippedTest));

            foreach (ActivitySnapshot activity in activities)
            {
                Require(
                    activity.TraceId == applicationRoot.TraceId,
                    $"{activity.OperationName} trace id {activity.TraceId} did not match application root {applicationRoot.TraceId}.");
            }

            Require(builder.ParentSpanId == applicationRoot.SpanId, "TestHostBuilder was not parented to the application root.");
            Require(testHost.ParentSpanId == applicationRoot.SpanId, "TestHost was not parented to the application root.");
            Require(run.ParentSpanId == testHost.SpanId, "Run was not parented to TestHost.");
            Require(testFrameworkInvoker.ParentSpanId == run.SpanId, "TestFrameworkInvoker was not parented to Run.");
            Require(executeTestRequest.ParentSpanId == testFrameworkInvoker.SpanId, "ExecuteTestRequest was not parented to TestFrameworkInvoker.");
            Require(testFramework.ParentSpanId == executeTestRequest.SpanId, "TestFramework was not parented to ExecuteTestRequest.");

            Require(assemblyInitialize.ParentSpanId == testFramework.SpanId, "MSTest.AssemblyInitialize was not parented to TestFramework.");
            Require(assemblyCleanup.ParentSpanId == testFramework.SpanId, "MSTest.AssemblyCleanup was not parented to TestFramework.");
            Require(classInitialize.ParentSpanId == testFramework.SpanId, "MSTest.ClassInitialize was not parented to TestFramework.");
            Require(classCleanup.ParentSpanId == testFramework.SpanId, "MSTest.ClassCleanup was not parented to TestFramework.");
            Require(testInitialize.All(activity => activity.ParentSpanId == firstTest.SpanId || activity.ParentSpanId == secondTest.SpanId), "MSTest.TestInitialize was not parented to a canonical test activity.");
            Require(testCleanup.All(activity => activity.ParentSpanId == firstTest.SpanId || activity.ParentSpanId == secondTest.SpanId), "MSTest.TestCleanup was not parented to a canonical test activity.");
            Require(!activities.Any(activity => activity.SourceName == MtpSourceName && activity.OperationName == "MSTest.TestMethod"), "A duplicate MSTest.TestMethod activity was exported.");

            Require(firstTest.ParentSpanId == testFramework.SpanId, "The first canonical test activity was not parented to TestFramework.");
            Require(secondTest.ParentSpanId == testFramework.SpanId, "The second canonical test activity was not parented to TestFramework.");
            Require(firstTest.Links.Length == 0, "The first canonical test activity unexpectedly had an execution link.");
            Require(secondTest.Links.Length == 0, "The second canonical test activity unexpectedly had an execution link.");
            Require(firstTest.GetTagItem("test.case.duration_ms") is not null, "The first canonical test activity did not carry the reported duration.");
            Require(secondTest.GetTagItem("test.case.duration_ms") is not null, "The second canonical test activity did not carry the reported duration.");
            Require(firstTest.GetTagItem("test.case.result.status")?.ToString() == "pass", "The first canonical test activity did not carry the pass result.");
            Require(secondTest.GetTagItem("test.case.result.status")?.ToString() == "pass", "The second canonical test activity did not carry the pass result.");
            RequireDurationMatchesReportedTiming(firstTest);
            RequireDurationMatchesReportedTiming(secondTest);

            Require(firstCustom.ParentSpanId == firstTest.SpanId, "The first custom activity was not parented to the first canonical test activity.");
            Require(secondCustom.ParentSpanId == secondTest.SpanId, "The second custom activity was not parented to the second canonical test activity.");
            Require(firstHttp.ParentSpanId == firstTest.SpanId, "The first HTTP activity was not parented to the first canonical test activity.");
            Require(secondHttp.ParentSpanId == secondTest.SpanId, "The second HTTP activity was not parented to the second canonical test activity.");

            Require(TelemetryProbe.AssemblyInitializeAmbientSpanId == testFramework.SpanId, "AssemblyInitialize did not observe TestFramework as ambient.");
            Require(TelemetryProbe.AssemblyCleanupAmbientSpanId == testFramework.SpanId, "AssemblyCleanup did not observe TestFramework as ambient.");
            Require(TelemetryProbe.ClassInitializeAmbientSpanId == testFramework.SpanId, "ClassInitialize did not observe TestFramework as ambient.");
            Require(TelemetryProbe.ClassCleanupAmbientSpanId == testFramework.SpanId, "ClassCleanup did not observe TestFramework as ambient.");
            RequireFixtureAmbient(TelemetryProbe.TestInitializeAmbientSpanIds, firstTest, secondTest, "TestInitialize");
            RequireFixtureAmbient(TelemetryProbe.TestCleanupAmbientSpanIds, firstTest, secondTest, "TestCleanup");
            RequireAmbient(TelemetryProbe.GetSnapshot("first"), firstTest, firstCustom);
            RequireAmbient(TelemetryProbe.GetSnapshot("second"), secondTest, secondCustom);

            Require(firstTest.SpanId != secondTest.SpanId, "Parallel tests exported the same canonical activity.");
            Require(firstCustom.SpanId != secondCustom.SpanId, "Parallel tests exported the same custom span.");
            Require(firstHttp.SpanId != secondHttp.SpanId, "Parallel tests exported the same HTTP span.");
            Require(retry.GetTagItem("test.case.result.status")?.ToString() == "pass", "The retry occurrence did not finish with the final pass result.");
            Require(folded.GetTagItem("test.case.result.status")?.ToString() == "pass", "The folded data-row occurrence did not aggregate to pass.");
            Require(unfolded.Select(activity => activity.SpanId).Distinct().Count() == 2, "Unfolded data rows did not receive distinct occurrences.");
            Require(skipped.GetTagItem("test.case.result.status")?.ToString() == "skipped", "The skipped occurrence did not retain its result status.");

            PrintTopology("application-root", applicationRoot);
            PrintTopology("mtp-builder", builder);
            PrintTopology("mtp-test-host", testHost);
            PrintTopology("mtp-run", run);
            PrintTopology("mtp-test-framework-invoker", testFrameworkInvoker);
            PrintTopology("mtp-execute-test-request", executeTestRequest);
            PrintTopology("mtp-test-framework", testFramework);
            PrintTopology("canonical-first", firstTest);
            PrintTopology("custom-first", firstCustom);
            PrintTopology("http-first", firstHttp);
            PrintTopology("canonical-second", secondTest);
            PrintTopology("custom-second", secondCustom);
            PrintTopology("http-second", secondHttp);
            PrintTopology("retry", retry);
            PrintTopology("folded", folded);
            PrintTopology("skipped", skipped);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"{ex.Message}{Environment.NewLine}Captured activities:{Environment.NewLine}{Dump(activities)}",
                ex);
        }
    }

    private static ActivitySnapshot SingleResult(ActivitySnapshot[] activities, string methodName)
        => activities.Single(activity =>
            activity.SourceName == MtpSourceName
            && activity.GetTagItem("test.case.id") is not null
            && string.Equals(activity.GetTagItem("code.function.name")?.ToString()?.Split('.').Last(), methodName, StringComparison.Ordinal));

    private static ActivitySnapshot[] ResultsForMethod(ActivitySnapshot[] activities, string methodName, int expectedCount)
    {
        ActivitySnapshot[] matches = activities
            .Where(activity =>
                activity.SourceName == MtpSourceName
                && activity.GetTagItem("test.case.id") is not null
                && string.Equals(activity.GetTagItem("code.function.name")?.ToString()?.Split('.').Last(), methodName, StringComparison.Ordinal))
            .ToArray();
        Require(matches.Length == expectedCount, $"Expected {expectedCount} canonical activities for {methodName}, but found {matches.Length}.");
        return matches;
    }

    private static ActivitySnapshot SingleChild(ActivitySnapshot[] activities, string sourceName, ActivitySpanId parentSpanId)
        => activities.Single(activity => activity.SourceName == sourceName && activity.ParentSpanId == parentSpanId);

    private static ActivitySnapshot Single(ActivitySnapshot[] activities, string sourceName, string operationName)
        => activities.Single(activity => activity.SourceName == sourceName && activity.OperationName == operationName);

    private static ActivitySnapshot[] Multiple(ActivitySnapshot[] activities, string sourceName, string operationName, int expectedCount)
    {
        ActivitySnapshot[] matches = activities
            .Where(activity => activity.SourceName == sourceName && activity.OperationName == operationName)
            .ToArray();
        Require(matches.Length == expectedCount, $"Expected {expectedCount} {operationName} activities, but found {matches.Length}.");
        return matches;
    }

    private static void RequireAmbient(TelemetryProbe.Snapshot snapshot, ActivitySnapshot canonical, ActivitySnapshot custom)
    {
        Require(snapshot.BeforeOperationName == canonical.OperationName, $"{snapshot.Name} did not observe its canonical test activity before its custom activity.");
        Require(snapshot.BeforeTraceId == canonical.TraceId, $"{snapshot.Name} ambient trace id did not match its canonical test activity.");
        Require(snapshot.BeforeSpanId == canonical.SpanId, $"{snapshot.Name} ambient span id did not match its canonical test activity.");
        Require(snapshot.CustomTraceId == custom.TraceId, $"{snapshot.Name} custom trace id did not match the exported custom activity.");
        Require(snapshot.CustomSpanId == custom.SpanId, $"{snapshot.Name} custom span id did not match the exported custom activity.");
        Require(snapshot.AfterOperationName == canonical.OperationName, $"{snapshot.Name} did not restore its canonical test activity after its custom activity.");
        Require(snapshot.AfterTraceId == canonical.TraceId, $"{snapshot.Name} restored trace id did not match its canonical test activity.");
        Require(snapshot.AfterSpanId == canonical.SpanId, $"{snapshot.Name} restored span id did not match its canonical test activity.");
    }

    private static void RequireFixtureAmbient(
        IReadOnlyDictionary<string, ActivitySpanId> ambientSpanIds,
        ActivitySnapshot firstTest,
        ActivitySnapshot secondTest,
        string fixtureName)
    {
        Require(ambientSpanIds.Count == 2, $"Expected two {fixtureName} ambient snapshots, but found {ambientSpanIds.Count}.");
        Require(
            ambientSpanIds[nameof(ParallelActivityTests.FirstTest)] == firstTest.SpanId,
            $"{fixtureName} for the first test did not observe the first canonical activity.");
        Require(
            ambientSpanIds[nameof(ParallelActivityTests.SecondTest)] == secondTest.SpanId,
            $"{fixtureName} for the second test did not observe the second canonical activity.");
    }

    private static void RequireDurationMatchesReportedTiming(ActivitySnapshot activity)
    {
        double reportedDuration = Convert.ToDouble(
            activity.GetTagItem("test.case.duration_ms"),
            CultureInfo.InvariantCulture);
        double difference = Math.Abs(activity.Duration.TotalMilliseconds - reportedDuration);
        Require(
            difference < 500,
            $"{activity.OperationName} duration differed from the reported execution timing by {difference}ms.");
    }

    private static void PrintTopology(string name, ActivitySnapshot activity)
        => Console.WriteLine(
            $"[MSTEST-OTEL-TOPOLOGY] name={name};trace={activity.TraceId};span={activity.SpanId};" +
            $"parent={activity.ParentSpanId};links={string.Join(",", activity.Links)}");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string Dump(ActivitySnapshot[] activities)
        => string.Join(
            Environment.NewLine,
            activities
                .OrderBy(activity => activity.StartTimeUtc)
                .Select(activity =>
                    $"{activity.SourceName}|{activity.OperationName}|trace={activity.TraceId}|span={activity.SpanId}|" +
                    $"parent={activity.ParentSpanId}|links={string.Join(",", activity.Links)}|" +
                    $"test.case.name={activity.GetTagItem("test.case.name")}|test.case.id={activity.GetTagItem("test.case.id")}"));
}

internal sealed class CapturingActivityExporter : BaseExporter<Activity>
{
    private readonly object _syncRoot = new();
    private readonly List<ActivitySnapshot> _activities = [];
    private Resource? _capturedResource;

    public override ExportResult Export(in Batch<Activity> batch)
    {
        lock (_syncRoot)
        {
            _capturedResource ??= ParentProvider?.GetResource();
            foreach (Activity activity in batch)
            {
                _activities.Add(ActivitySnapshot.Create(activity));
            }
        }

        return ExportResult.Success;
    }

    public ActivitySnapshot[] Snapshot()
    {
        lock (_syncRoot)
        {
            return [.. _activities];
        }
    }

    public Resource? GetCapturedResource()
    {
        lock (_syncRoot)
        {
            return _capturedResource;
        }
    }
}

internal sealed record ActivitySnapshot(
    string SourceName,
    string OperationName,
    ActivityTraceId TraceId,
    ActivitySpanId SpanId,
    ActivitySpanId ParentSpanId,
    DateTime StartTimeUtc,
    TimeSpan Duration,
    ActivitySpanId[] Links,
    IReadOnlyDictionary<string, object?> Tags)
{
    public object? GetTagItem(string key)
        => Tags.TryGetValue(key, out object? value) ? value : null;

    public static ActivitySnapshot Create(Activity activity)
        => new(
            activity.Source.Name,
            activity.OperationName,
            activity.TraceId,
            activity.SpanId,
            activity.ParentSpanId,
            activity.StartTimeUtc,
            activity.Duration,
            activity.Links.Select(link => link.Context.SpanId).ToArray(),
            activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal));
}

internal sealed class CapturingMetricExporter : BaseExporter<Metric>
{
    private readonly object _syncRoot = new();
    private readonly HashSet<string> _metricNames = new(StringComparer.Ordinal);
    private Resource? _capturedResource;

    public override ExportResult Export(in Batch<Metric> batch)
    {
        lock (_syncRoot)
        {
            _capturedResource ??= ParentProvider?.GetResource();
            foreach (Metric metric in batch)
            {
                _metricNames.Add(metric.Name);
            }
        }

        return ExportResult.Success;
    }

    public bool Contains(string metricName)
    {
        lock (_syncRoot)
        {
            return _metricNames.Contains(metricName);
        }
    }

    public Resource? GetCapturedResource()
    {
        lock (_syncRoot)
        {
            return _capturedResource;
        }
    }
}

internal static class ResourceVerifier
{
    public static void Verify(Resource? resource, string signal, string expectedServiceName)
    {
        Dictionary<string, object> attributes = (resource
            ?? throw new InvalidOperationException($"The application-owned {signal} exporter did not receive a resource."))
            .Attributes
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
        if (!attributes.TryGetValue("service.name", out object? serviceName)
            || !string.Equals(serviceName?.ToString(), expectedServiceName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The application service.name was not preserved by the {signal} provider: '{serviceName}'.");
        }

        if (!attributes.TryGetValue("test.assembly.name", out object? assemblyName)
            || !string.Equals(assemblyName?.ToString(), "MSTestOpenTelemetryActivityTopology", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The focused testing-platform test resource was not applied to the {signal} provider: '{assemblyName}'.");
        }
    }
}

internal static class TelemetryProbe
{
    private static readonly TaskCompletionSource FirstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly TaskCompletionSource SecondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly ConcurrentDictionary<string, Snapshot> Snapshots = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ActivitySpanId> TestInitializeAmbient = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ActivitySpanId> TestCleanupAmbient = new(StringComparer.Ordinal);
    private static ActivitySource? s_activitySource;

    public const string SourceName = "MSTestOpenTelemetryActivityTopology";

    public static ActivitySpanId AssemblyInitializeAmbientSpanId { get; private set; }

    public static ActivitySpanId AssemblyCleanupAmbientSpanId { get; private set; }

    public static ActivitySpanId ClassInitializeAmbientSpanId { get; private set; }

    public static ActivitySpanId ClassCleanupAmbientSpanId { get; private set; }

    public static IReadOnlyDictionary<string, ActivitySpanId> TestInitializeAmbientSpanIds => TestInitializeAmbient;

    public static IReadOnlyDictionary<string, ActivitySpanId> TestCleanupAmbientSpanIds => TestCleanupAmbient;

    public static void Initialize(ActivitySource activitySource)
        => s_activitySource = activitySource;

    public static void CaptureAssemblyInitialize()
        => AssemblyInitializeAmbientSpanId = RequireCurrent("AssemblyInitialize").SpanId;

    public static void CaptureAssemblyCleanup()
        => AssemblyCleanupAmbientSpanId = RequireCurrent("AssemblyCleanup").SpanId;

    public static void CaptureClassInitialize()
        => ClassInitializeAmbientSpanId = RequireCurrent("ClassInitialize").SpanId;

    public static void CaptureClassCleanup()
        => ClassCleanupAmbientSpanId = RequireCurrent("ClassCleanup").SpanId;

    public static void CaptureTestInitialize(string testName)
        => TestInitializeAmbient[testName] = RequireCurrent($"{testName} TestInitialize").SpanId;

    public static void CaptureTestCleanup(string testName)
        => TestCleanupAmbient[testName] = RequireCurrent($"{testName} TestCleanup").SpanId;

    public static async Task CaptureTestAsync(string name, TaskCompletionSource started, Task otherStarted)
    {
        Activity before = RequireCurrent($"{name} before custom");
        using Activity custom = s_activitySource?.StartActivity($"custom-{name}")
            ?? throw new InvalidOperationException("The application-owned provider did not subscribe to the custom activity source.");
        Activity currentCustom = RequireCurrent($"{name} custom");

        started.SetResult();
        await otherStarted.WaitAsync(TimeSpan.FromSeconds(30));
        custom.Stop();
        await HttpProbe.GetAsync();

        Activity after = RequireCurrent($"{name} after custom");
        Snapshots[name] = new(
            name,
            before.OperationName,
            before.TraceId,
            before.SpanId,
            currentCustom.TraceId,
            currentCustom.SpanId,
            after.OperationName,
            after.TraceId,
            after.SpanId);
    }

    public static Task CaptureFirstAsync()
        => CaptureTestAsync("first", FirstStarted, SecondStarted.Task);

    public static Task CaptureSecondAsync()
        => CaptureTestAsync("second", SecondStarted, FirstStarted.Task);

    public static Snapshot GetSnapshot(string name)
        => Snapshots.TryGetValue(name, out Snapshot? snapshot)
            ? snapshot
            : throw new InvalidOperationException($"No telemetry snapshot was recorded for '{name}'.");

    private static Activity RequireCurrent(string location)
        => Activity.Current
            ?? throw new InvalidOperationException($"{location} did not have an ambient activity.");

    public sealed record Snapshot(
        string Name,
        string BeforeOperationName,
        ActivityTraceId BeforeTraceId,
        ActivitySpanId BeforeSpanId,
        ActivityTraceId CustomTraceId,
        ActivitySpanId CustomSpanId,
        string AfterOperationName,
        ActivityTraceId AfterTraceId,
        ActivitySpanId AfterSpanId);
}

internal static class HttpProbe
{
    public static async Task GetAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = ServeOnceAsync(listener);

        using var client = new HttpClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/"));
        response.EnsureSuccessStatusCode();
        await serverTask;
    }

    private static async Task ServeOnceAsync(TcpListener listener)
    {
        try
        {
            using TcpClient client = await listener.AcceptTcpClientAsync();
            using NetworkStream stream = client.GetStream();
            var buffer = new byte[1];
            const string HeaderTerminator = "\r\n\r\n";
            int matchedTerminatorBytes = 0;
            int totalRequestBytes = 0;
            int count;
            while ((count = await stream.ReadAsync(buffer)) > 0)
            {
                totalRequestBytes += count;
                if (totalRequestBytes > 16 * 1024)
                {
                    throw new InvalidOperationException("The HTTP request headers exceeded 16 KB.");
                }

                for (int i = 0; i < count; i++)
                {
                    char current = (char)buffer[i];
                    matchedTerminatorBytes = current == HeaderTerminator[matchedTerminatorBytes]
                        ? matchedTerminatorBytes + 1
                        : current == HeaderTerminator[0] ? 1 : 0;
                    if (matchedTerminatorBytes == HeaderTerminator.Length)
                    {
                        break;
                    }
                }

                if (matchedTerminatorBytes == HeaderTerminator.Length)
                {
                    break;
                }
            }

            byte[] response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK");
            await stream.WriteAsync(response);
        }
        finally
        {
            listener.Stop();
        }
    }
}

[TestClass]
public sealed class ParallelActivityTests
{
    public TestContext TestContext { get; set; } = null!;

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext _)
        => TelemetryProbe.CaptureAssemblyInitialize();

    [AssemblyCleanup]
    public static void AssemblyCleanup()
        => TelemetryProbe.CaptureAssemblyCleanup();

    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
        => TelemetryProbe.CaptureClassInitialize();

    [ClassCleanup]
    public static void ClassCleanup()
        => TelemetryProbe.CaptureClassCleanup();

    [TestInitialize]
    public void TestInitialize()
        => TelemetryProbe.CaptureTestInitialize(TestContext.TestName);

    [TestCleanup]
    public void TestCleanup()
        => TelemetryProbe.CaptureTestCleanup(TestContext.TestName);

    [TestMethod]
    public Task FirstTest()
        => TelemetryProbe.CaptureFirstAsync();

    [TestMethod]
    public Task SecondTest()
        => TelemetryProbe.CaptureSecondAsync();
}

[TestClass]
[DoNotParallelize]
public sealed class OccurrenceIdentityTests
{
    private static int s_retryAttempt;

    [TestMethod]
    [Retry(1)]
    public void RetryTest()
    {
        if (Interlocked.Increment(ref s_retryAttempt) == 1)
        {
            Assert.Fail("First attempt fails.");
        }
    }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DataRow(1)]
    [DataRow(2)]
    public void FoldedDataRows(int value)
        => Assert.IsGreaterThan(0, value);

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Unfold)]
    [DataRow(1)]
    [DataRow(2)]
    public void UnfoldedDataRows(int value)
        => Assert.IsGreaterThan(0, value);

    [TestMethod]
    [Ignore("Characterize a selected test that never enters user execution.")]
    public void SkippedTest()
    {
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (
                AssetName,
                AssetName,
                Sources
                    .PatchCodeWithReplace("$TargetFrameworks$", "net8.0;net9.0")
                    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                    .PatchCodeWithReplace("$MicrosoftExtensionsHostingVersion$", MicrosoftExtensionsHostingVersion)
                    .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                    .PatchCodeWithReplace("$OpenTelemetryVersion$", OpenTelemetryVersion));
    }
}
