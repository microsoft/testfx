// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class GeneratedMicrosoftExtensionsHostingTests : AcceptanceTestBase<GeneratedMicrosoftExtensionsHostingTests.TestAssetFixture>
{
    private const string AssetName = "GeneratedMicrosoftExtensionsHostingTest";
    private static readonly string[] HostingCompatibilityFrameworks =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["net462", "net8.0", "net9.0", "net10.0"]
            : ["net8.0", "net9.0", "net10.0"];

    public static IEnumerable<object[]> HostingCompatibilityFrameworksForDynamicData { get; } =
        HostingCompatibilityFrameworks.Select(static tfm => new object[] { tfm });

    [DynamicData(nameof(HostingCompatibilityFrameworksForDynamicData))]
    [TestMethod]
    public async Task GeneratedEntryPoint_RunsMSTestWithOneHostLifecycle(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult result = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContains("Passed!");
        Assert.AreEqual(1, CountOccurrences(result.StandardError, "HOST_FACTORY"));
        Assert.AreEqual(1, CountOccurrences(result.StandardError, "HOST_STARTED"));
        Assert.AreEqual(1, CountOccurrences(result.StandardError, "HOST_STOPPED"));
        Assert.AreEqual(1, CountOccurrences(result.StandardError, "OTEL_TRACE=TestHostBuilder"));
        Assert.AreEqual(1, CountOccurrences(result.StandardError, "OTEL_METRIC=test.run.duration"));
        Assert.AreEqual(1, CountOccurrences(result.StandardError, "OTEL_SERVICE=generated-host-tests"));
    }

    [DynamicData(nameof(HostingCompatibilityFrameworksForDynamicData))]
    [TestMethod]
    public async Task GeneratedEntryPoint_HelpDoesNotCreateHost(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult result = await testHost.ExecuteAsync("--help", cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.DoesNotContain("HOST_FACTORY", result.StandardError);
        Assert.DoesNotContain("HOST_STARTED", result.StandardError);
    }

    [DynamicData(nameof(HostingCompatibilityFrameworksForDynamicData))]
    [TestMethod]
    public async Task GeneratedEntryPoint_ResponseFileHelpDoesNotCreateHost(string tfm)
    {
        using TempDirectory tempDirectory = new();
        string responseFile = Path.Combine(tempDirectory.Path, "help.rsp");
        File.WriteAllText(responseFile, "--help");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult result = await testHost.ExecuteAsync($"@{responseFile}", cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.DoesNotContain("HOST_FACTORY", result.StandardError);
        Assert.DoesNotContain("HOST_STARTED", result.StandardError);
    }

    [DynamicData(nameof(HostingCompatibilityFrameworksForDynamicData))]
    [TestMethod]
    public async Task GeneratedEntryPoint_JsonListingKeepsStdoutProtocolSafe(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult result = await testHost.ExecuteAsync("--list-tests json", cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.AreEqual(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.AreEqual(1, document.RootElement.GetProperty("tests").GetArrayLength());
        Assert.AreEqual(1, CountOccurrences(result.StandardError, "HOST_FACTORY"));
        Assert.AreEqual(1, CountOccurrences(result.StandardError, "HOST_STARTED"));
        Assert.AreEqual(1, CountOccurrences(result.StandardError, "HOST_STOPPED"));
    }

    public TestContext TestContext { get; set; } = null!;

    private static int CountOccurrences(string value, string fragment)
        => (value.Length - value.Replace(fragment, string.Empty).Length) / fragment.Length;

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string TestCode = """
#file GeneratedMicrosoftExtensionsHostingTest.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <RestorePackagesPath>$(MSBuildProjectDirectory)\.packages</RestorePackagesPath>
        <MicrosoftTestingPlatformVersion>$MicrosoftTestingPlatformVersion$</MicrosoftTestingPlatformVersion>
        <EnableMicrosoftTestingExtensionsCodeCoverage>false</EnableMicrosoftTestingExtensionsCodeCoverage>
        <TestingPlatformHostFactory>GeneratedTestHost.CreateHost</TestingPlatformHostFactory>
        <TestingPlatformOpenTelemetryMode>HostOwned</TestingPlatformOpenTelemetryMode>
        <NoWarn>$(NoWarn);TPEXP</NoWarn>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Extensions.Hosting" Version="$MicrosoftTestingExtensionsHostingVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.OpenTelemetry" Version="$MicrosoftTestingExtensionsOpenTelemetryVersion$" />
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" Condition="'$(TargetFramework)' == 'net462'" />
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" Condition="'$(TargetFramework)' == 'net8.0'" />
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" Condition="'$(TargetFramework)' == 'net9.0'" />
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.12" Condition="'$(TargetFramework)' == 'net10.0'" />
        <PackageReference Include="OpenTelemetry" Version="$OpenTelemetryVersion$" />
        <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="$OpenTelemetryVersion$" />
        <PackageReference Include="System.Text.Json" Version="10.0.12" />
    </ItemGroup>
</Project>

#file Tests.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Testing.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

internal static class GeneratedTestHost
{
    public static Task<IHost> CreateHost()
    {
        Console.Error.WriteLine("HOST_FACTORY");
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        var activityExporter = new CapturingActivityExporter();
        var metricExporter = new CapturingMetricExporter();
        builder.Services.AddSingleton(activityExporter);
        builder.Services.AddSingleton(metricExporter);
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("generated-host-tests")
                .AddTestingPlatformTestResource()
                .AddTestingPlatformCIResource())
            .WithTracing(tracing => tracing
                .AddTestingPlatformInstrumentation()
                .AddProcessor(new SimpleActivityExportProcessor(activityExporter)))
            .WithMetrics(metrics => metrics
                .AddTestingPlatformInstrumentation()
                .AddReader(new PeriodicExportingMetricReader(metricExporter, exportIntervalMilliseconds: 10)));
        builder.Services.AddHostedService<MarkerHostedService>();
        return Task.FromResult(builder.Build());
    }
}

internal sealed class MarkerHostedService(
    TracerProvider tracerProvider,
    MeterProvider meterProvider,
    CapturingActivityExporter activityExporter,
    CapturingMetricExporter metricExporter) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.Error.WriteLine("HOST_STARTED");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        tracerProvider.ForceFlush();
        meterProvider.ForceFlush();
        if (!activityExporter.Contains("TestHostBuilder"))
        {
            throw new InvalidOperationException("The host-owned tracer provider did not export the MTP builder span.");
        }

        if (!metricExporter.Contains("test.run.duration"))
        {
            throw new InvalidOperationException("The host-owned meter provider did not flush the final run metric.");
        }

        string? serviceName = activityExporter.GetCapturedResource()?.Attributes
            .Single(attribute => attribute.Key == "service.name").Value.ToString();
        if (serviceName != "generated-host-tests")
        {
            throw new InvalidOperationException($"The host-owned service identity was replaced: '{serviceName}'.");
        }

        Console.Error.WriteLine("OTEL_TRACE=TestHostBuilder");
        Console.Error.WriteLine("OTEL_METRIC=test.run.duration");
        Console.Error.WriteLine($"OTEL_SERVICE={serviceName}");
        Console.Error.WriteLine("HOST_STOPPED");
        return Task.CompletedTask;
    }
}

internal sealed class CapturingActivityExporter : BaseExporter<System.Diagnostics.Activity>
{
    private readonly object _sync = new();
    private readonly HashSet<string> _activityNames = new(StringComparer.Ordinal);
    private Resource? _resource;

    public override ExportResult Export(in Batch<System.Diagnostics.Activity> batch)
    {
        lock (_sync)
        {
            _resource ??= ParentProvider?.GetResource();
            foreach (System.Diagnostics.Activity activity in batch)
            {
                _activityNames.Add(activity.OperationName);
            }
        }

        return ExportResult.Success;
    }

    public bool Contains(string activityName)
    {
        lock (_sync)
        {
            return _activityNames.Contains(activityName);
        }
    }

    public Resource? GetCapturedResource()
    {
        lock (_sync)
        {
            return _resource;
        }
    }
}

internal sealed class CapturingMetricExporter : BaseExporter<Metric>
{
    private readonly object _sync = new();
    private readonly HashSet<string> _metricNames = new(StringComparer.Ordinal);

    public override ExportResult Export(in Batch<Metric> batch)
    {
        lock (_sync)
        {
            foreach (Metric metric in batch)
            {
                _metricNames.Add(metric.Name);
            }
        }

        return ExportResult.Success;
    }

    public bool Contains(string metricName)
    {
        lock (_sync)
        {
            return _metricNames.Contains(metricName);
        }
    }
}

[TestClass]
public sealed class GeneratedHostTests
{
    [TestMethod]
    public void Pass()
    {
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            TestCode
                .PatchTargetFrameworks(HostingCompatibilityFrameworks)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsHostingVersion$", MicrosoftTestingExtensionsHostingVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsOpenTelemetryVersion$", MicrosoftTestingExtensionsOpenTelemetryVersion)
                .PatchCodeWithReplace("$OpenTelemetryVersion$", OpenTelemetryVersion));
    }
}
