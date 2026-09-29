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
        TestHostResult result = await testHost.ExecuteAsync(
            environmentVariables: new() { ["MTP_OTEL_EXECUTION_MODE"] = "fallback" },
            cancellationToken: TestContext.CancellationToken);

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

    [TestMethod]
    public async Task ExternalFramework_PublicCanonicalExecutionApi_ProducesOneIsolatedSpanPerExecution()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync(
            environmentVariables: new() { ["MTP_OTEL_EXECUTION_MODE"] = "canonical" },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContains("[APP-OWNED-CANONICAL] one-span-per-execution");
        result.AssertOutputContains("[APP-OWNED-CANONICAL-PARALLEL] isolated");
        result.AssertOutputContains("[APP-OWNED-CANONICAL-CUSTOM] children");
        result.AssertOutputContains("[APP-OWNED-CANONICAL-HTTP] children");
        result.AssertOutputContains("[APP-OWNED-CANONICAL-MULTI-RESULT] one-span");
        result.AssertOutputContains("[APP-OWNED-CANONICAL-LINKS] none");
        result.AssertOutputContains("[APP-OWNED-CANONICAL-API] all-run-overloads");
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
    <NoWarn>$(NoWarn);TPEXP</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="$MicrosoftExtensionsHostingVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.OpenTelemetry" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="OpenTelemetry" Version="$OpenTelemetryVersion$" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="$OpenTelemetryVersion$" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.19.0" />
  </ItemGroup>
</Project>

#file Program.cs
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
                .AddHttpClientInstrumentation()
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
        string executionMode = Environment.GetEnvironmentVariable("MTP_OTEL_EXECUTION_MODE") ?? "fallback";
        testBuilder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, _) => new SingleTestFramework(applicationActivitySource, executionMode));
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
        Activity builderActivity = activityExporter.Single(
            activity => activity.Source.Name == "Microsoft.Testing.Platform"
                && activity.OperationName == "TestHostBuilder");

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

        switch (executionMode)
        {
            case "fallback":
                VerifyFallback(activityExporter, applicationTraceId);
                break;
            case "canonical":
                VerifyCanonical(activityExporter, applicationTraceId);
                break;
            default:
                throw new InvalidOperationException($"Unknown MTP_OTEL_EXECUTION_MODE value '{executionMode}'.");
        }

        if (!metricExporter.Contains("test.run.duration"))
        {
            throw new InvalidOperationException("The application-owned meter provider did not export test.run.duration.");
        }

        string traceServiceName = GetServiceName(activityExporter.GetCapturedResource(), "trace");
        _ = GetServiceName(metricExporter.GetCapturedResource(), "metric");

        Console.WriteLine($"[APP-OWNED-TRACE] {builderActivity.OperationName}");
        Console.WriteLine("[APP-OWNED-METRIC] test.run.duration");
        Console.WriteLine($"[APP-OWNED-RESOURCE] service.name={traceServiceName}");
        Console.WriteLine("[APP-OWNED-PARENT] inherited");

        await host.StopAsync();
        return exitCode;
    }

    private static void VerifyFallback(CapturingActivityExporter activityExporter, ActivityTraceId applicationTraceId)
    {
        Activity firstCustomTestActivity = activityExporter.Single(
            activity => activity.Source.Name == ApplicationActivitySourceName
                && activity.OperationName == "application-test-activity-one");
        Activity secondCustomTestActivity = activityExporter.Single(
            activity => activity.Source.Name == ApplicationActivitySourceName
                && activity.OperationName == "application-test-activity-two");
        Activity testFrameworkActivity = activityExporter.Single(
            activity => activity.Source.Name == "Microsoft.Testing.Platform"
                && activity.OperationName == "TestFramework");
        Activity firstTestResultActivity = activityExporter.Single(
            activity => activity.Source.Name == "Microsoft.Testing.Platform"
                && activity.GetTagItem("test.case.id")?.ToString() == "application-owned-test-one");
        Activity secondTestResultActivity = activityExporter.Single(
            activity => activity.Source.Name == "Microsoft.Testing.Platform"
                && activity.GetTagItem("test.case.id")?.ToString() == "application-owned-test-two");

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

        Console.WriteLine($"[APP-OWNED-TEST-TRACE] {firstCustomTestActivity.OperationName}");
        Console.WriteLine("[APP-OWNED-TOPOLOGY] sibling-under-TestFramework");
        Console.WriteLine("[APP-OWNED-CORRELATION] activity-links");
        Console.WriteLine("[APP-OWNED-PARALLEL-CORRELATION] isolated");
    }

    private static void VerifyCanonical(CapturingActivityExporter activityExporter, ActivityTraceId applicationTraceId)
    {
        Activity[] activities = activityExporter.Snapshot();
        Activity testFrameworkActivity = activities.Single(
            activity => activity.Source.Name == "Microsoft.Testing.Platform"
                && activity.OperationName == "TestFramework");
        Activity[] canonicalActivities = activities
            .Where(activity => activity.Source.Name == "Microsoft.Testing.Platform"
                && activity.GetTagItem("test.case.id")?.ToString() is
                    "application-owned-test-one" or "application-owned-test-two")
            .ToArray();
        if (canonicalActivities.Length != 2)
        {
            throw new InvalidOperationException(
                $"Expected exactly two canonical MTP spans, but found {canonicalActivities.Length}.");
        }

        Activity firstCanonicalActivity = canonicalActivities.Single(
            activity => activity.GetTagItem("test.case.id")?.ToString() == "application-owned-test-one");
        Activity secondCanonicalActivity = canonicalActivities.Single(
            activity => activity.GetTagItem("test.case.id")?.ToString() == "application-owned-test-two");
        Activity firstCustomTestActivity = activities.Single(
            activity => activity.Source.Name == ApplicationActivitySourceName
                && activity.OperationName == "application-test-activity-one");
        Activity secondCustomTestActivity = activities.Single(
            activity => activity.Source.Name == ApplicationActivitySourceName
                && activity.OperationName == "application-test-activity-two");
        Activity firstHttpActivity = activities.Single(
            activity => activity.Source.Name == "System.Net.Http"
                && activity.ParentSpanId == firstCanonicalActivity.SpanId);
        Activity secondHttpActivity = activities.Single(
            activity => activity.Source.Name == "System.Net.Http"
                && activity.ParentSpanId == secondCanonicalActivity.SpanId);

        if (testFrameworkActivity.TraceId != applicationTraceId
            || firstCanonicalActivity.TraceId != testFrameworkActivity.TraceId
            || secondCanonicalActivity.TraceId != testFrameworkActivity.TraceId
            || firstCanonicalActivity.ParentSpanId != testFrameworkActivity.SpanId
            || secondCanonicalActivity.ParentSpanId != testFrameworkActivity.SpanId)
        {
            throw new InvalidOperationException("Canonical MTP spans were not isolated children of TestFramework.");
        }

        if (firstCanonicalActivity.SpanId == secondCanonicalActivity.SpanId
            || firstCustomTestActivity.TraceId != firstCanonicalActivity.TraceId
            || secondCustomTestActivity.TraceId != secondCanonicalActivity.TraceId
            || firstCustomTestActivity.ParentSpanId != firstCanonicalActivity.SpanId
            || secondCustomTestActivity.ParentSpanId != secondCanonicalActivity.SpanId
            || firstCustomTestActivity.ParentSpanId == secondCanonicalActivity.SpanId
            || secondCustomTestActivity.ParentSpanId == firstCanonicalActivity.SpanId
            || firstHttpActivity.TraceId != firstCanonicalActivity.TraceId
            || secondHttpActivity.TraceId != secondCanonicalActivity.TraceId
            || firstHttpActivity.ParentSpanId != firstCanonicalActivity.SpanId
            || secondHttpActivity.ParentSpanId != secondCanonicalActivity.SpanId)
        {
            throw new InvalidOperationException("Parallel canonical test execution contexts were crossed.");
        }

        if (firstCanonicalActivity.Links.Any() || secondCanonicalActivity.Links.Any())
        {
            throw new InvalidOperationException("Canonical MTP spans must not contain ActivityLink correlation.");
        }

        if (firstCanonicalActivity.GetTagItem("test.case.result.status")?.ToString() != "pass")
        {
            throw new InvalidOperationException("The canonical span did not aggregate multiple result messages to the final pass.");
        }

        Console.WriteLine("[APP-OWNED-CANONICAL] one-span-per-execution");
        Console.WriteLine("[APP-OWNED-CANONICAL-PARALLEL] isolated");
        Console.WriteLine("[APP-OWNED-CANONICAL-CUSTOM] children");
        Console.WriteLine("[APP-OWNED-CANONICAL-HTTP] children");
        Console.WriteLine("[APP-OWNED-CANONICAL-MULTI-RESULT] one-span");
        Console.WriteLine("[APP-OWNED-CANONICAL-LINKS] none");
        Console.WriteLine("[APP-OWNED-CANONICAL-API] all-run-overloads");
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

internal sealed class SingleTestFramework(ActivitySource activitySource, string executionMode) : ITestFramework, IDataProducer
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

        if (executionMode == "canonical")
        {
            await Task.WhenAll(
                ExecuteCanonicalTestAsync(
                    context,
                    uid: "application-owned-test-one",
                    displayName: "Application-owned telemetry test one",
                    activityName: "application-test-activity-one",
                    started: firstStarted,
                    otherStarted: secondStarted.Task,
                    publishMultipleResults: true),
                ExecuteCanonicalTestAsync(
                    context,
                    uid: "application-owned-test-two",
                    displayName: "Application-owned telemetry test two",
                    activityName: "application-test-activity-two",
                    started: secondStarted,
                    otherStarted: firstStarted.Task,
                    publishMultipleResults: false));
        }
        else if (executionMode == "fallback")
        {
            await Task.WhenAll(
                ExecuteFallbackTestAsync(
                    context,
                    uid: "application-owned-test-one",
                    displayName: "Application-owned telemetry test one",
                    activityName: "application-test-activity-one",
                    started: firstStarted,
                    otherStarted: secondStarted.Task),
                ExecuteFallbackTestAsync(
                    context,
                    uid: "application-owned-test-two",
                    displayName: "Application-owned telemetry test two",
                    activityName: "application-test-activity-two",
                    started: secondStarted,
                    otherStarted: firstStarted.Task));
        }
        else
        {
            throw new InvalidOperationException($"Unknown MTP_OTEL_EXECUTION_MODE value '{executionMode}'.");
        }

        context.Complete();
    }

    private async Task ExecuteCanonicalTestAsync(
        ExecuteRequestContext context,
        string uid,
        string displayName,
        string activityName,
        TaskCompletionSource started,
        Task otherStarted,
        bool publishMultipleResults)
    {
        var sessionUid = new SessionUid("application-owned-session");
        using TestExecution execution = await context.StartTestExecutionAsync(
            this,
            new TestNodeUpdateMessage(
                sessionUid,
                new TestNode
                {
                    Uid = uid,
                    DisplayName = displayName,
                    Properties = new PropertyBag(new InProgressTestNodeStateProperty()),
                }));

        ActivitySpanId canonicalSpanId = execution.Run(
            () => RequireCurrent($"{uid} Run<T>").SpanId);
        execution.Run(() =>
        {
            if (RequireCurrent($"{uid} Run").SpanId != canonicalSpanId)
            {
                throw new InvalidOperationException("Run did not enter the canonical test execution context.");
            }
        });

        await execution.RunAsync(async () =>
        {
            Activity canonicalActivity = RequireCurrent($"{uid} RunAsync");
            if (canonicalActivity.SpanId != canonicalSpanId)
            {
                throw new InvalidOperationException("RunAsync did not enter the canonical test execution context.");
            }

            using Activity? testActivity = activitySource.StartActivity(activityName);
            if (testActivity is null)
            {
                throw new InvalidOperationException("The application-owned provider did not subscribe to the test activity source.");
            }

            testActivity.SetTag("test.id", uid);
            started.SetResult();
            await otherStarted.WaitAsync(TimeSpan.FromSeconds(30));
        });

        HttpStatusCode statusCode = await execution.RunAsync(async () =>
        {
            if (RequireCurrent($"{uid} RunAsync<T>").SpanId != canonicalSpanId)
            {
                throw new InvalidOperationException("RunAsync<T> did not enter the canonical test execution context.");
            }

            return await HttpProbe.GetAsync();
        });
        if (statusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"The HTTP probe returned {statusCode}.");
        }

        TestNodeUpdateMessage CreateResult(PropertyBag properties)
            => new(
                sessionUid,
                new TestNode
                {
                    Uid = uid,
                    DisplayName = displayName,
                    Properties = properties,
                });

        TestNodeUpdateMessage[] results = publishMultipleResults
            ? [
                CreateResult(new PropertyBag(
                    new FailedTestNodeStateProperty("Superseded retry attempt."),
                    new RetryAttemptProperty(attemptNumber: 1, isSuperseded: true))),
                CreateResult(new PropertyBag(
                    PassedTestNodeStateProperty.CachedInstance,
                    new RetryAttemptProperty(attemptNumber: 2, isSuperseded: false))),
            ]
            : [CreateResult(new PropertyBag(PassedTestNodeStateProperty.CachedInstance))];
        await execution.CompleteAsync(results, DateTimeOffset.UtcNow);
    }

    private async Task ExecuteFallbackTestAsync(
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

    private static Activity RequireCurrent(string location)
        => Activity.Current
            ?? throw new InvalidOperationException($"{location} did not have an ambient activity.");
}

internal static class HttpProbe
{
    public static async Task<HttpStatusCode> GetAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = ServeOnceAsync(listener);

        using var client = new HttpClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/"));
        response.EnsureSuccessStatusCode();
        await serverTask;
        return response.StatusCode;
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
