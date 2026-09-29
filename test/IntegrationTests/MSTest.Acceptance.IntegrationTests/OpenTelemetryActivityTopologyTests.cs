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
    public async Task MSTestSdk_ApplicationOwnedOpenTelemetry_CharacterizesCurrentActivityTopology(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult result = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContains("[MSTEST-OTEL-CHARACTERIZATION] exact-current-behavior");
        result.AssertOutputContains($"[MSTEST-OTEL-TFM] {tfm}");
        result.AssertOutputContains("[MSTEST-OTEL-AMBIENT] TestFramework");
        result.AssertOutputContains("[MSTEST-OTEL-LINKS] empty");
        result.AssertOutputContains("[MSTEST-OTEL-PARALLEL] isolated");
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
  </ItemGroup>
</Project>

#file Program.cs
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
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
        ActivityTopologyVerifier.Verify(activityExporter.Snapshot(), applicationRoot);
        ResourceVerifier.Verify(activityExporter.GetCapturedResource(), "trace", ApplicationServiceName);
        ResourceVerifier.Verify(metricExporter.GetCapturedResource(), "metric", ApplicationServiceName);
        if (!metricExporter.Contains("test.run.duration"))
        {
            throw new InvalidOperationException("The application-owned meter provider did not export test.run.duration.");
        }

        Console.WriteLine("[MSTEST-OTEL-CHARACTERIZATION] exact-current-behavior");
        Console.WriteLine($"[MSTEST-OTEL-TFM] {GetTargetFrameworkMoniker()}");
        Console.WriteLine("[MSTEST-OTEL-AMBIENT] TestFramework");
        Console.WriteLine("[MSTEST-OTEL-LINKS] empty");
        Console.WriteLine("[MSTEST-OTEL-PARALLEL] isolated");
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

    public static void Verify(Activity[] activities, Activity applicationRoot)
    {
        try
        {
            Activity builder = Single(activities, MtpSourceName, "TestHostBuilder");
            Activity testHost = Single(activities, MtpSourceName, "TestHost");
            Activity run = Single(activities, MtpSourceName, "Run");
            Activity testFrameworkInvoker = Single(activities, MtpSourceName, "TestFrameworkInvoker");
            Activity executeTestRequest = Single(activities, MtpSourceName, "ExecuteTestRequest");
            Activity testFramework = Single(activities, MtpSourceName, "TestFramework");
            Activity assemblyInitialize = Single(activities, MtpSourceName, "MSTest.AssemblyInitialize");
            Activity assemblyCleanup = Single(activities, MtpSourceName, "MSTest.AssemblyCleanup");
            Activity classInitialize = Single(activities, MtpSourceName, "MSTest.ClassInitialize");
            Activity classCleanup = Single(activities, MtpSourceName, "MSTest.ClassCleanup");
            Activity[] testInitialize = Multiple(activities, MtpSourceName, "MSTest.TestInitialize", expectedCount: 2);
            Activity[] testCleanup = Multiple(activities, MtpSourceName, "MSTest.TestCleanup", expectedCount: 2);
            Activity firstMethod = SingleMSTestMethod(activities, nameof(ParallelActivityTests.FirstTest));
            Activity secondMethod = SingleMSTestMethod(activities, nameof(ParallelActivityTests.SecondTest));
            Activity firstCustom = Single(activities, TelemetryProbe.SourceName, "custom-first");
            Activity secondCustom = Single(activities, TelemetryProbe.SourceName, "custom-second");
            Activity firstResult = SingleResult(activities, nameof(ParallelActivityTests.FirstTest));
            Activity secondResult = SingleResult(activities, nameof(ParallelActivityTests.SecondTest));

            foreach (Activity activity in activities)
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
            Require(testInitialize.All(activity => activity.ParentSpanId == testFramework.SpanId), "MSTest.TestInitialize was not parented to TestFramework.");
            Require(testCleanup.All(activity => activity.ParentSpanId == testFramework.SpanId), "MSTest.TestCleanup was not parented to TestFramework.");
            Require(firstMethod.ParentSpanId == testFramework.SpanId, "The first MSTest.TestMethod was not parented to TestFramework.");
            Require(secondMethod.ParentSpanId == testFramework.SpanId, "The second MSTest.TestMethod was not parented to TestFramework.");

            // Result messages are consumed asynchronously, so their spans can start on either side of the
            // non-ambient MSTest.TestMethod spans. The stable contract is their shared parent and empty links.
            Require(firstResult.ParentSpanId == testFramework.SpanId, "The first result activity was not parented to TestFramework.");
            Require(secondResult.ParentSpanId == testFramework.SpanId, "The second result activity was not parented to TestFramework.");
            Require(!firstResult.Links.Any(), "The first result activity unexpectedly had an execution link.");
            Require(!secondResult.Links.Any(), "The second result activity unexpectedly had an execution link.");
            Require(firstResult.GetTagItem("test.case.duration_ms") is not null, "The first result activity did not carry the reported duration.");
            Require(secondResult.GetTagItem("test.case.duration_ms") is not null, "The second result activity did not carry the reported duration.");
            Require(firstResult.GetTagItem("test.case.result.status")?.ToString() == "pass", "The first result activity did not carry the pass result.");
            Require(secondResult.GetTagItem("test.case.result.status")?.ToString() == "pass", "The second result activity did not carry the pass result.");
            Require(firstMethod.GetTagItem("test.case.duration_ms") is null, "The first MSTest.TestMethod unexpectedly duplicated the reported duration attribute.");
            Require(secondMethod.GetTagItem("test.case.duration_ms") is null, "The second MSTest.TestMethod unexpectedly duplicated the reported duration attribute.");
            Require(firstMethod.GetTagItem("test.case.result.status")?.ToString() == "pass", "The first MSTest.TestMethod did not carry the engine result.");
            Require(secondMethod.GetTagItem("test.case.result.status")?.ToString() == "pass", "The second MSTest.TestMethod did not carry the engine result.");

            Require(firstCustom.ParentSpanId == testFramework.SpanId, "The first custom activity was not parented to ambient TestFramework.");
            Require(secondCustom.ParentSpanId == testFramework.SpanId, "The second custom activity was not parented to ambient TestFramework.");
            Require(firstCustom.ParentSpanId != firstMethod.SpanId, "The first custom activity unexpectedly parented to MSTest.TestMethod.");
            Require(secondCustom.ParentSpanId != secondMethod.SpanId, "The second custom activity unexpectedly parented to MSTest.TestMethod.");

            Require(TelemetryProbe.AssemblyInitializeAmbientSpanId == testFramework.SpanId, "AssemblyInitialize did not observe TestFramework as ambient.");
            Require(TelemetryProbe.AssemblyCleanupAmbientSpanId == testFramework.SpanId, "AssemblyCleanup did not observe TestFramework as ambient.");
            Require(TelemetryProbe.ClassInitializeAmbientSpanId == testFramework.SpanId, "ClassInitialize did not observe TestFramework as ambient.");
            Require(TelemetryProbe.ClassCleanupAmbientSpanId == testFramework.SpanId, "ClassCleanup did not observe TestFramework as ambient.");
            RequireFixtureAmbient(TelemetryProbe.TestInitializeAmbientSpanIds, testFramework, "TestInitialize");
            RequireFixtureAmbient(TelemetryProbe.TestCleanupAmbientSpanIds, testFramework, "TestCleanup");
            RequireAmbient(TelemetryProbe.GetSnapshot("first"), testFramework, firstCustom);
            RequireAmbient(TelemetryProbe.GetSnapshot("second"), testFramework, secondCustom);

            Require(firstMethod.SpanId != secondMethod.SpanId, "Parallel tests exported the same MSTest.TestMethod span.");
            Require(firstCustom.SpanId != secondCustom.SpanId, "Parallel tests exported the same custom span.");
            Require(firstResult.SpanId != secondResult.SpanId, "Parallel tests exported the same result span.");

            PrintTopology("application-root", applicationRoot);
            PrintTopology("mtp-builder", builder);
            PrintTopology("mtp-test-host", testHost);
            PrintTopology("mtp-run", run);
            PrintTopology("mtp-test-framework-invoker", testFrameworkInvoker);
            PrintTopology("mtp-execute-test-request", executeTestRequest);
            PrintTopology("mtp-test-framework", testFramework);
            PrintTopology("mstest-method-first", firstMethod);
            PrintTopology("custom-first", firstCustom);
            PrintTopology("mtp-result-first", firstResult);
            PrintTopology("mstest-method-second", secondMethod);
            PrintTopology("custom-second", secondCustom);
            PrintTopology("mtp-result-second", secondResult);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"{ex.Message}{Environment.NewLine}Captured activities:{Environment.NewLine}{Dump(activities)}",
                ex);
        }
    }

    private static Activity SingleMSTestMethod(Activity[] activities, string methodName)
        => activities.Single(activity =>
            activity.Source.Name == MtpSourceName
            && activity.OperationName == "MSTest.TestMethod"
            && string.Equals(activity.GetTagItem("test.case.name")?.ToString(), methodName, StringComparison.Ordinal));

    private static Activity SingleResult(Activity[] activities, string methodName)
        => activities.Single(activity =>
            activity.Source.Name == MtpSourceName
            && activity.OperationName == methodName
            && activity.GetTagItem("test.case.id") is not null
            && string.Equals(activity.GetTagItem("test.case.name")?.ToString(), methodName, StringComparison.Ordinal));

    private static Activity Single(Activity[] activities, string sourceName, string operationName)
        => activities.Single(activity =>
            activity.Source.Name == sourceName
            && activity.OperationName == operationName);

    private static Activity[] Multiple(Activity[] activities, string sourceName, string operationName, int expectedCount)
    {
        Activity[] matches = activities
            .Where(activity => activity.Source.Name == sourceName && activity.OperationName == operationName)
            .ToArray();
        Require(matches.Length == expectedCount, $"Expected {expectedCount} {operationName} activities, but found {matches.Length}.");
        return matches;
    }

    private static void RequireAmbient(TelemetryProbe.Snapshot snapshot, Activity testFramework, Activity custom)
    {
        Require(snapshot.BeforeOperationName == "TestFramework", $"{snapshot.Name} did not observe TestFramework before its custom activity.");
        Require(snapshot.BeforeTraceId == testFramework.TraceId, $"{snapshot.Name} ambient trace id did not match TestFramework.");
        Require(snapshot.BeforeSpanId == testFramework.SpanId, $"{snapshot.Name} ambient span id did not match TestFramework.");
        Require(snapshot.CustomTraceId == custom.TraceId, $"{snapshot.Name} custom trace id did not match the exported custom activity.");
        Require(snapshot.CustomSpanId == custom.SpanId, $"{snapshot.Name} custom span id did not match the exported custom activity.");
        Require(snapshot.AfterOperationName == "TestFramework", $"{snapshot.Name} did not restore TestFramework after its custom activity.");
        Require(snapshot.AfterTraceId == testFramework.TraceId, $"{snapshot.Name} restored trace id did not match TestFramework.");
        Require(snapshot.AfterSpanId == testFramework.SpanId, $"{snapshot.Name} restored span id did not match TestFramework.");
    }

    private static void RequireFixtureAmbient(
        IReadOnlyDictionary<string, ActivitySpanId> ambientSpanIds,
        Activity testFramework,
        string fixtureName)
    {
        Require(ambientSpanIds.Count == 2, $"Expected two {fixtureName} ambient snapshots, but found {ambientSpanIds.Count}.");
        Require(
            ambientSpanIds.Values.All(spanId => spanId == testFramework.SpanId),
            $"{fixtureName} did not consistently observe TestFramework as ambient.");
    }

    private static void PrintTopology(string name, Activity activity)
        => Console.WriteLine(
            $"[MSTEST-OTEL-TOPOLOGY] name={name};trace={activity.TraceId};span={activity.SpanId};" +
            $"parent={activity.ParentSpanId};links={string.Join(",", activity.Links.Select(link => link.Context.SpanId))}");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string Dump(Activity[] activities)
        => string.Join(
            Environment.NewLine,
            activities
                .OrderBy(activity => activity.StartTimeUtc)
                .Select(activity =>
                    $"{activity.Source.Name}|{activity.OperationName}|trace={activity.TraceId}|span={activity.SpanId}|" +
                    $"parent={activity.ParentSpanId}|links={string.Join(",", activity.Links.Select(link => link.Context.SpanId))}|" +
                    $"test.case.name={activity.GetTagItem("test.case.name")}|test.case.id={activity.GetTagItem("test.case.id")}"));
}

internal sealed class CapturingActivityExporter : BaseExporter<Activity>
{
    private readonly object _syncRoot = new();
    private readonly List<Activity> _activities = [];
    private Resource? _capturedResource;

    public override ExportResult Export(in Batch<Activity> batch)
    {
        lock (_syncRoot)
        {
            _capturedResource ??= ParentProvider?.GetResource();
            foreach (Activity activity in batch)
            {
                _activities.Add(activity);
            }
        }

        return ExportResult.Success;
    }

    public Activity[] Snapshot()
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
