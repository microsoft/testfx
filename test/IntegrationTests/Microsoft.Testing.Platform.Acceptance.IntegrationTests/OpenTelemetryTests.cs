// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class OpenTelemetryTests : AcceptanceTestBase<OpenTelemetryTests.TestAssetFixture>
{
    private const string AssetName = "ApplicationOwnedOpenTelemetry";

    [TestMethod]
    public async Task HostApplicationBuilderProviders_ConsumeMtpDiagnosticsAndPreserveApplicationResource()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContains("[APP-OWNED-TRACE] TestHostBuilder");
        result.AssertOutputContains("[APP-OWNED-TEST-TRACE] application-test-activity-one");
        result.AssertOutputContains("[APP-OWNED-METRIC] test.run.duration");
        result.AssertOutputContains("[APP-OWNED-RESOURCE] service.name=application-owned-tests");
        result.AssertOutputContains("[APP-OWNED-PARENT] inherited");
        result.AssertOutputContains("[APP-OWNED-TOPOLOGY] sibling-under-TestFramework");
        result.AssertOutputContains("[APP-OWNED-CORRELATION] activity-links");
        result.AssertOutputContains("[APP-OWNED-PARALLEL-CORRELATION] isolated");
    }

    public TestContext TestContext { get; set; } = null!;

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string TestCode = """
#file ApplicationOwnedOpenTelemetry.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$TargetFrameworks$</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="$MicrosoftExtensionsHostingVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.OpenTelemetry" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="OpenTelemetry" Version="$OpenTelemetryVersion$" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="$OpenTelemetryVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

internal static class Program
{
    private const string ApplicationActivitySourceName = "ApplicationOwnedOpenTelemetry";
    private const string ApplicationServiceName = "application-owned-tests";

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
                .AddSource(ApplicationActivitySourceName)
                .AddTestingPlatformInstrumentation()
                .AddProcessor(new SimpleActivityExportProcessor(activityExporter)))
            .WithMetrics(metrics => metrics
                .AddTestingPlatformInstrumentation()
                .AddReader(new PeriodicExportingMetricReader(metricExporter, exportIntervalMilliseconds: 10)));

        using IHost host = hostBuilder.Build();
        await host.StartAsync();

        using var applicationActivitySource = new ActivitySource(ApplicationActivitySourceName);
        using Activity? applicationActivity = applicationActivitySource.StartActivity("application-test-run");
        if (applicationActivity is null)
        {
            throw new InvalidOperationException("The application-owned provider did not subscribe to its activity source.");
        }

        ActivityTraceId applicationTraceId = applicationActivity.TraceId;
        ActivitySpanId applicationSpanId = applicationActivity.SpanId;

        ITestApplicationBuilder testBuilder = await TestApplication.CreateBuilderAsync(args);
        testBuilder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, _) => new SingleTestFramework(applicationActivitySource));
        testBuilder.AddTestingPlatformDiagnostics();

        int exitCode;
        using (ITestApplication testApplication = await testBuilder.BuildAsync())
        {
            exitCode = await testApplication.RunAsync();
        }

        applicationActivity.Stop();
        ServiceProviderServiceExtensions.GetRequiredService<TracerProvider>(host.Services).ForceFlush();
        ServiceProviderServiceExtensions.GetRequiredService<MeterProvider>(host.Services).ForceFlush();

        Activity applicationRootActivity = activityExporter.Single(
            activity => activity.Source.Name == ApplicationActivitySourceName
                && activity.OperationName == "application-test-run");
        Activity firstCustomTestActivity = activityExporter.Single(
            activity => activity.Source.Name == ApplicationActivitySourceName
                && activity.OperationName == "application-test-activity-one");
        Activity secondCustomTestActivity = activityExporter.Single(
            activity => activity.Source.Name == ApplicationActivitySourceName
                && activity.OperationName == "application-test-activity-two");
        Activity builderActivity = activityExporter.Single(
            activity => activity.Source.Name == "Microsoft.Testing.Platform"
                && activity.OperationName == "TestHostBuilder");
        Activity testFrameworkActivity = activityExporter.Single(
            activity => activity.Source.Name == "Microsoft.Testing.Platform"
                && activity.OperationName == "TestFramework");
        Activity firstTestResultActivity = activityExporter.Single(
            activity => activity.Source.Name == "Microsoft.Testing.Platform"
                && activity.GetTagItem("test.case.id")?.ToString() == "application-owned-test-one");
        Activity secondTestResultActivity = activityExporter.Single(
            activity => activity.Source.Name == "Microsoft.Testing.Platform"
                && activity.GetTagItem("test.case.id")?.ToString() == "application-owned-test-two");

        if (applicationRootActivity.TraceId != applicationTraceId
            || applicationRootActivity.SpanId != applicationSpanId)
        {
            throw new InvalidOperationException("The application-owned root activity was not exported.");
        }

        if (builderActivity.TraceId != applicationTraceId || builderActivity.ParentSpanId != applicationSpanId)
        {
            throw new InvalidOperationException(
                $"MTP did not inherit the application trace context. Expected {applicationTraceId}/{applicationSpanId}, " +
                $"actual {builderActivity.TraceId}/{builderActivity.ParentSpanId}.");
        }

        if (testFrameworkActivity.TraceId != applicationTraceId
            || firstCustomTestActivity.TraceId != testFrameworkActivity.TraceId
            || secondCustomTestActivity.TraceId != testFrameworkActivity.TraceId
            || firstTestResultActivity.TraceId != testFrameworkActivity.TraceId
            || secondTestResultActivity.TraceId != testFrameworkActivity.TraceId
            || firstCustomTestActivity.ParentSpanId != testFrameworkActivity.SpanId
            || secondCustomTestActivity.ParentSpanId != testFrameworkActivity.SpanId
            || firstTestResultActivity.ParentSpanId != testFrameworkActivity.SpanId
            || secondTestResultActivity.ParentSpanId != testFrameworkActivity.SpanId)
        {
            throw new InvalidOperationException(
                "The application test activities and MTP test-result activities must be siblings under TestFramework.");
        }

        ActivityLink firstLink = firstTestResultActivity.Links.Single();
        ActivityLink secondLink = secondTestResultActivity.Links.Single();
        if (firstLink.Context.TraceId != firstCustomTestActivity.TraceId
            || firstLink.Context.SpanId != firstCustomTestActivity.SpanId
            || secondLink.Context.TraceId != secondCustomTestActivity.TraceId
            || secondLink.Context.SpanId != secondCustomTestActivity.SpanId)
        {
            throw new InvalidOperationException("MTP test-result activity links did not match their test execution activities.");
        }

        if (firstLink.Context.SpanId == secondCustomTestActivity.SpanId
            || secondLink.Context.SpanId == firstCustomTestActivity.SpanId)
        {
            throw new InvalidOperationException("Parallel test execution activity links were crossed.");
        }

        if (!metricExporter.Contains("test.run.duration"))
        {
            throw new InvalidOperationException("The application-owned meter provider did not export test.run.duration.");
        }

        string traceServiceName = GetServiceName(activityExporter.GetCapturedResource(), "trace");
        _ = GetServiceName(metricExporter.GetCapturedResource(), "metric");

        Console.WriteLine($"[APP-OWNED-TRACE] {builderActivity.OperationName}");
        Console.WriteLine($"[APP-OWNED-TEST-TRACE] {firstCustomTestActivity.OperationName}");
        Console.WriteLine("[APP-OWNED-METRIC] test.run.duration");
        Console.WriteLine($"[APP-OWNED-RESOURCE] service.name={traceServiceName}");
        Console.WriteLine("[APP-OWNED-PARENT] inherited");
        Console.WriteLine("[APP-OWNED-TOPOLOGY] sibling-under-TestFramework");
        Console.WriteLine("[APP-OWNED-CORRELATION] activity-links");
        Console.WriteLine("[APP-OWNED-PARALLEL-CORRELATION] isolated");

        await host.StopAsync();
        return exitCode;
    }

    private static string GetServiceName(Resource? resource, string signal)
    {
        Dictionary<string, object> attributes = (resource
            ?? throw new InvalidOperationException($"The application-owned {signal} exporter did not receive a resource."))
            .Attributes
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
        if (!attributes.TryGetValue("service.name", out object? serviceName)
            || !string.Equals(serviceName?.ToString(), ApplicationServiceName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The application service.name was not preserved by the {signal} provider: '{serviceName}'.");
        }

        return ApplicationServiceName;
    }
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

    public Activity Single(Func<Activity, bool> predicate)
    {
        lock (_syncRoot)
        {
            return _activities.Single(predicate);
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

internal sealed class SingleTestFramework(ActivitySource activitySource) : ITestFramework, IDataProducer
{
    public string Uid => nameof(SingleTestFramework);
    public string Version => "1.0.0";
    public string DisplayName => nameof(SingleTestFramework);
    public string Description => "Publishes two passing tests in parallel.";
    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        TaskCompletionSource firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await Task.WhenAll(
            ExecuteTestAsync(
                context,
                uid: "application-owned-test-one",
                displayName: "Application-owned telemetry test one",
                activityName: "application-test-activity-one",
                started: firstStarted,
                otherStarted: secondStarted.Task),
            ExecuteTestAsync(
                context,
                uid: "application-owned-test-two",
                displayName: "Application-owned telemetry test two",
                activityName: "application-test-activity-two",
                started: secondStarted,
                otherStarted: firstStarted.Task));
        context.Complete();
    }

    private async Task ExecuteTestAsync(
        ExecuteRequestContext context,
        string uid,
        string displayName,
        string activityName,
        TaskCompletionSource started,
        Task otherStarted)
    {
        using Activity? testActivity = activitySource.StartActivity(activityName);
        if (testActivity is null)
        {
            throw new InvalidOperationException("The application-owned provider did not subscribe to the test activity source.");
        }

        testActivity.SetTag("test.id", uid);
        var sessionUid = new SessionUid("application-owned-session");
        await context.MessageBus.PublishAsync(
            this,
            new TestNodeUpdateMessage(
                sessionUid,
                new TestNode
                {
                    Uid = uid,
                    DisplayName = displayName,
                    Properties = new PropertyBag(new InProgressTestNodeStateProperty()),
                }));
        started.SetResult();
        await otherStarted;
        await context.MessageBus.PublishAsync(
            this,
            new TestNodeUpdateMessage(
                sessionUid,
                new TestNode
                {
                    Uid = uid,
                    DisplayName = displayName,
                    Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
                }));
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (
            AssetName,
            AssetName,
            TestCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftExtensionsHostingVersion$", MicrosoftExtensionsHostingVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$OpenTelemetryVersion$", OpenTelemetryVersion));
    }
}
