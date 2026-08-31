// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Extensions.OpenTelemetry;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using EnvironmentConfiguration = Microsoft.Testing.Extensions.OpenTelemetryProviderExtensions.EnvironmentConfiguration;

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Direct tests for the two turnkey OpenTelemetry helpers that shipped as stable API in this release —
/// <see cref="OpenTelemetryProviderExtensions.AddTestingPlatformResource(ResourceBuilder)"/> and
/// <see cref="OpenTelemetryProviderExtensions.AddOpenTelemetryProviderFromEnvironment(ITestApplicationBuilder, System.Action{TracerProviderBuilder}?, System.Action{MeterProviderBuilder}?)"/> —
/// plus an end-to-end trace test that runs the real OpenTelemetry SDK pipeline.
/// </summary>
/// <remarks>
/// The end-to-end test stands up a real <see cref="TracerProvider"/> listening on the platform activity source, so
/// the class is <see cref="DoNotParallelizeAttribute"/> to keep only one such provider alive at a time; captured
/// spans are additionally filtered by a per-test unique name prefix so an ambient provider in the test host cannot
/// pollute the assertions.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class OpenTelemetryProviderExtensionsTests
{
    private static readonly string[] ObservedEnvironmentVariables =
    [
        "OTEL_SDK_DISABLED",
        "OTEL_TRACES_EXPORTER",
        "OTEL_METRICS_EXPORTER",
        "OTEL_EXPORTER_OTLP_ENDPOINT",
    ];

    [TestMethod]
    public void AddTestingPlatformResource_WithNullBuilder_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => OpenTelemetryProviderExtensions.AddTestingPlatformResource(null!));

    [TestMethod]
    public void AddTestingPlatformResource_AttachesPlatformAttributesToTheBuiltResource()
    {
        Resource resource = ResourceBuilder.CreateEmpty().AddTestingPlatformResource().Build();

        Dictionary<string, object> attributes = [];
        foreach (KeyValuePair<string, object> attribute in resource.Attributes)
        {
            attributes[attribute.Key] = attribute.Value;
        }

        Assert.IsTrue(attributes.TryGetValue("service.name", out object? serviceName));
        Assert.IsFalse(string.IsNullOrWhiteSpace(serviceName as string));
        Assert.AreEqual(Environment.MachineName, attributes["host.name"]);
        Assert.AreEqual(".NET", attributes["process.runtime.name"]);
    }

    [TestMethod]
    public void AddOpenTelemetryProviderFromEnvironment_WithNullBuilder_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => ((ITestApplicationBuilder)null!).AddOpenTelemetryProviderFromEnvironment());

    [TestMethod]
    public async Task AddOpenTelemetryProviderFromEnvironment_RegistersProviderWithDelegateAndSkipsWhenSdkDisabled()
        => await WithEnvironmentAsync(
            new()
            {
                ["OTEL_TRACES_EXPORTER"] = "none",
                ["OTEL_METRICS_EXPORTER"] = "none",
            },
            async () =>
            {
                ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
                bool tracingConfigured = false;

                builder.AddOpenTelemetryProviderFromEnvironment(configureTracing: _ => tracingConfigured = true);

                IOpenTelemetryProvider? provider = ((TelemetryManager)((TestApplicationBuilder)builder).Telemetry).BuildOTelProvider(new ServiceProvider());
                using (provider)
                {
                    Assert.IsNotNull(provider);
                    Assert.IsTrue(tracingConfigured);
                }

                ITestApplicationBuilder disabledBuilder = await TestApplication.CreateBuilderAsync([]);
                Environment.SetEnvironmentVariable("OTEL_SDK_DISABLED", "true");
                disabledBuilder.AddOpenTelemetryProviderFromEnvironment();

                IOpenTelemetryProvider? disabledProvider = ((TelemetryManager)((TestApplicationBuilder)disabledBuilder).Telemetry).BuildOTelProvider(new ServiceProvider());
                using (disabledProvider)
                {
                    Assert.IsNull(disabledProvider);
                }
            });

    [TestMethod]
    public void ResolveEnvironmentConfiguration_WhenSdkDisabled_RegistersNothingEvenWithEndpointAndDelegates()
    {
        // OTEL_SDK_DISABLED must win over an explicit endpoint and over caller-supplied delegates.
        EnvironmentConfiguration configuration = OpenTelemetryProviderExtensions.ResolveEnvironmentConfiguration(
            Env(new()
            {
                ["OTEL_SDK_DISABLED"] = "true",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
            }),
            hasTracingDelegate: true,
            hasMetricsDelegate: true);

        Assert.IsFalse(configuration.ShouldRegisterProvider);
        Assert.IsFalse(configuration.ConfigureTracingProvider);
        Assert.IsFalse(configuration.ConfigureMetricsProvider);
        Assert.IsFalse(configuration.UseOtlpTracing);
        Assert.IsFalse(configuration.UseOtlpMetrics);
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("True")]
    [DataRow("TRUE")]
    [DataRow("tRuE")]
    public void ResolveEnvironmentConfiguration_TreatsCaseInsensitiveTrueSdkDisabledAsDisabled(string value)
    {
        EnvironmentConfiguration configuration = OpenTelemetryProviderExtensions.ResolveEnvironmentConfiguration(
            Env(new() { ["OTEL_SDK_DISABLED"] = value }),
            hasTracingDelegate: true,
            hasMetricsDelegate: true);

        Assert.IsFalse(configuration.ShouldRegisterProvider);
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("yes")]
    [DataRow("false")]
    [DataRow("")]
    [DataRow(" true ")]
    public void ResolveEnvironmentConfiguration_TreatsNonTrueSdkDisabledValuesAsEnabled(string value)
    {
        // The OpenTelemetry boolean convention recognises only a case-insensitive "true"; "1" and other spellings
        // must leave the SDK enabled. Pairing the value with an endpoint proves the SDK was not disabled: if it had
        // been, the endpoint opt-in below would have been ignored.
        EnvironmentConfiguration configuration = OpenTelemetryProviderExtensions.ResolveEnvironmentConfiguration(
            Env(new()
            {
                ["OTEL_SDK_DISABLED"] = value,
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
            }),
            hasTracingDelegate: false,
            hasMetricsDelegate: false);

        Assert.IsTrue(configuration.ShouldRegisterProvider);
        Assert.IsTrue(configuration.UseOtlpTracing);
        Assert.IsTrue(configuration.UseOtlpMetrics);
    }

    [TestMethod]
    public void ResolveEnvironmentConfiguration_WithNoExporterAndNoDelegates_RegistersNothing()
    {
        EnvironmentConfiguration configuration = OpenTelemetryProviderExtensions.ResolveEnvironmentConfiguration(
            Env([]),
            hasTracingDelegate: false,
            hasMetricsDelegate: false);

        Assert.IsFalse(configuration.ShouldRegisterProvider);
    }

    [TestMethod]
    public void ResolveEnvironmentConfiguration_WithOtlpEndpointOnly_EnablesBothProvidersAndExporters()
    {
        EnvironmentConfiguration configuration = OpenTelemetryProviderExtensions.ResolveEnvironmentConfiguration(
            Env(new() { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317" }),
            hasTracingDelegate: false,
            hasMetricsDelegate: false);

        Assert.IsTrue(configuration.ConfigureTracingProvider);
        Assert.IsTrue(configuration.ConfigureMetricsProvider);
        Assert.IsTrue(configuration.UseOtlpTracing);
        Assert.IsTrue(configuration.UseOtlpMetrics);
    }

    [TestMethod]
    public void ResolveEnvironmentConfiguration_WithTracesExporterOtlpOnly_EnablesOnlyTracing()
    {
        EnvironmentConfiguration configuration = OpenTelemetryProviderExtensions.ResolveEnvironmentConfiguration(
            Env(new() { ["OTEL_TRACES_EXPORTER"] = "otlp" }),
            hasTracingDelegate: false,
            hasMetricsDelegate: false);

        Assert.IsTrue(configuration.ConfigureTracingProvider);
        Assert.IsTrue(configuration.UseOtlpTracing);
        Assert.IsFalse(configuration.ConfigureMetricsProvider);
        Assert.IsFalse(configuration.UseOtlpMetrics);
    }

    [TestMethod]
    public void ResolveEnvironmentConfiguration_WithExporterNone_OverridesAnEndpoint()
    {
        // An explicit 'none' disables the exporter even when an endpoint is configured.
        EnvironmentConfiguration configuration = OpenTelemetryProviderExtensions.ResolveEnvironmentConfiguration(
            Env(new()
            {
                ["OTEL_TRACES_EXPORTER"] = "none",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
            }),
            hasTracingDelegate: false,
            hasMetricsDelegate: false);

        Assert.IsFalse(configuration.UseOtlpTracing);
        Assert.IsFalse(configuration.ConfigureTracingProvider);
    }

    [TestMethod]
    public void ResolveEnvironmentConfiguration_WithCommaSeparatedExporterList_StillEnablesOtlp()
    {
        EnvironmentConfiguration configuration = OpenTelemetryProviderExtensions.ResolveEnvironmentConfiguration(
            Env(new() { ["OTEL_TRACES_EXPORTER"] = "otlp,console" }),
            hasTracingDelegate: false,
            hasMetricsDelegate: false);

        Assert.IsTrue(configuration.UseOtlpTracing);
    }

    [TestMethod]
    public void ResolveEnvironmentConfiguration_WithTracingDelegateButNoExporter_ConfiguresProviderWithoutOtlp()
    {
        EnvironmentConfiguration configuration = OpenTelemetryProviderExtensions.ResolveEnvironmentConfiguration(
            Env([]),
            hasTracingDelegate: true,
            hasMetricsDelegate: false);

        Assert.IsTrue(configuration.ConfigureTracingProvider);
        Assert.IsFalse(configuration.UseOtlpTracing);
        Assert.IsFalse(configuration.ConfigureMetricsProvider);
    }

    [TestMethod]
    public void EndToEnd_InstrumentationAndResource_ExportSpanWithSemanticConventionTagsAndResource()
    {
        string namePrefix = $"e2e-{Guid.NewGuid():N}-";
        List<Activity> exported = [];
        CapturingActivityExporter exporter = new(exported, namePrefix);

        // The built TracerProvider (disposed by the using below) takes ownership of the processor added through
        // AddProcessor and disposes it — and the exporter it wraps — on Dispose, so there is no undisposed local.
        using (TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddTestingPlatformInstrumentation()
            .ConfigureResource(resource => resource.AddTestingPlatformResource())
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build())
        {
            using var service = new OpenTelemetryPlatformService();
            using var handler = new OpenTelemetryResultHandler(service);

            var testNode = new TestNode
            {
                Uid = new TestNodeUid("MyNamespace.MyTests.MyTest"),
                DisplayName = namePrefix + "MyTest",
                Properties = new PropertyBag(
                    new TestMethodIdentifierProperty(
                        assemblyFullName: "MyAssembly",
                        @namespace: "MyNamespace",
                        typeName: "MyTests",
                        methodName: "MyTest",
                        methodArity: 0,
                        parameterTypeFullNames: [],
                        returnTypeFullName: "System.Void"),
                    new TestFileLocationProperty("/repo/src/MyTests.cs", new LinePositionSpan(new LinePosition(10, 0), new LinePosition(12, 0))),
                    new StandardOutputProperty("hello from the test")),
            };

            handler.NotifyInProgress(testNode, parentUid: null);
            handler.NotifyPassed(testNode, PassedTestNodeStateProperty.CachedInstance);
        }

        Activity activity = exported.Single();
        Assert.AreEqual(namePrefix + "MyTest", activity.GetTagItem("test.case.name"));
        Assert.AreEqual("MyNamespace.MyTests.MyTest", activity.GetTagItem("code.function.name"));
        Assert.AreEqual("MyTests", activity.GetTagItem("test.suite.name"));
        Assert.AreEqual("/repo/src/MyTests.cs", activity.GetTagItem("code.file.path"));
        Assert.AreEqual("pass", activity.GetTagItem("test.case.result.status"));
        Assert.AreEqual("hello from the test", activity.GetTagItem("test.output.stdout"));

        Assert.IsNotNull(exporter.CapturedResource);
        Dictionary<string, object> resourceAttributes = [];
        foreach (KeyValuePair<string, object> attribute in exporter.CapturedResource.Attributes)
        {
            resourceAttributes[attribute.Key] = attribute.Value;
        }

        Assert.IsTrue(resourceAttributes.ContainsKey("service.name"));
        Assert.AreEqual(Environment.MachineName, resourceAttributes["host.name"]);
    }

    private static Func<string, string?> Env(Dictionary<string, string?> values)
        => name => values.TryGetValue(name, out string? value) ? value : null;

    private static async Task WithEnvironmentAsync(Dictionary<string, string?> values, Func<Task> body)
    {
        Dictionary<string, string?> snapshot = [];
        foreach (string name in ObservedEnvironmentVariables)
        {
            snapshot[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }

        try
        {
            foreach (KeyValuePair<string, string?> value in values)
            {
                Environment.SetEnvironmentVariable(value.Key, value.Value);
            }

            await body().ConfigureAwait(false);
        }
        finally
        {
            foreach (KeyValuePair<string, string?> entry in snapshot)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
        }
    }

    private sealed class CapturingActivityExporter : BaseExporter<Activity>
    {
        private readonly List<Activity> _activities;
        private readonly string _namePrefix;

        public CapturingActivityExporter(List<Activity> activities, string namePrefix)
        {
            _activities = activities;
            _namePrefix = namePrefix;
        }

        public Resource? CapturedResource { get; private set; }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            CapturedResource ??= ParentProvider?.GetResource();
            foreach (Activity activity in batch)
            {
                if (activity.OperationName.StartsWith(_namePrefix, StringComparison.Ordinal))
                {
                    lock (_activities)
                    {
                        _activities.Add(activity);
                    }
                }
            }

            return ExportResult.Success;
        }
    }
}
