// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reflection;

using Microsoft.Testing.Extensions.OpenTelemetry;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using EnvironmentConfiguration = Microsoft.Testing.Extensions.OpenTelemetryProviderExtensions.EnvironmentConfiguration;

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Direct tests for the OpenTelemetry registration and configuration helpers —
/// <see cref="OpenTelemetryProviderExtensions.AddTestingPlatformDiagnostics(ITestApplicationBuilder)"/>,
/// <see cref="OpenTelemetryProviderExtensions.AddTestingPlatformResource(ResourceBuilder)"/>,
/// <see cref="OpenTelemetryProviderExtensions.AddTestingPlatformTestResource(ResourceBuilder)"/>,
/// <see cref="OpenTelemetryProviderExtensions.AddTestingPlatformCIResource(ResourceBuilder)"/> and
/// <see cref="OpenTelemetryProviderExtensions.AddOpenTelemetryProviderFromEnvironment(ITestApplicationBuilder, System.Action{TracerProviderBuilder}?, System.Action{MeterProviderBuilder}?)"/> —
/// including raw-listener and real OpenTelemetry SDK coverage.
/// </summary>
/// <remarks>
/// Methods that mutate real environment variables carry a method-level
/// <see cref="ResourceLockAttribute"/> on <see cref="WellKnownResources.EnvironmentVariables"/> (the same pattern
/// used by <c>AzureFoundryChatClientProviderTests</c> and <c>TestingPlatformResourceDetectorTests</c> in this
/// project): they still serialize against every other test in the assembly that mutates environment variables, but
/// can run in parallel with tests that never touch environment variables at all. The end-to-end
/// test still carries <see cref="DoNotParallelizeAttribute"/> because it stands up a real
/// <see cref="TracerProvider"/> against the shared platform <c>ActivitySource</c>, an unbounded process-global
/// resource that a <see cref="ResourceLockAttribute"/> key cannot narrow. The remaining methods use a pure
/// in-memory environment fake (or only read process state) and stay in the parallel set. Captured spans in the
/// end-to-end test are additionally filtered by a per-test unique name prefix so an ambient provider in the test
/// host cannot pollute the assertions.
/// </remarks>
[TestClass]
public sealed class OpenTelemetryProviderExtensionsTests
{
    private static readonly string[] ObservedEnvironmentVariables =
    [
        "OTEL_SDK_DISABLED",
        "OTEL_TRACES_EXPORTER",
        "OTEL_METRICS_EXPORTER",
        "OTEL_EXPORTER_OTLP_ENDPOINT",
        "OTEL_SERVICE_NAME",
        "GITHUB_ACTIONS", "GITHUB_WORKFLOW", "GITHUB_RUN_ID", "GITHUB_JOB", "GITHUB_REF_NAME", "GITHUB_SHA", "GITHUB_REPOSITORY",
        "TF_BUILD", "BUILD_DEFINITIONNAME", "BUILD_BUILDID", "SYSTEM_JOBID", "BUILD_SOURCEBRANCHNAME", "BUILD_SOURCEVERSION", "BUILD_REPOSITORY_URI",
        "GITLAB_CI", "CI_PIPELINE_NAME", "CI_PIPELINE_ID", "CI_JOB_ID", "CI_COMMIT_REF_NAME", "CI_COMMIT_SHA", "CI_REPOSITORY_URL",
        "JENKINS_URL", "JOB_NAME", "BUILD_NUMBER", "GIT_BRANCH", "GIT_COMMIT", "GIT_URL",
    ];

    [TestMethod]
    public void AddTestingPlatformDiagnostics_WithNullBuilder_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => ((ITestApplicationBuilder)null!).AddTestingPlatformDiagnostics());

    [TestMethod]
    public void AddTestingPlatformResource_WithNullBuilder_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => OpenTelemetryProviderExtensions.AddTestingPlatformResource(null!));

    [TestMethod]
    public void AddTestingPlatformTestResource_WithNullBuilder_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => OpenTelemetryProviderExtensions.AddTestingPlatformTestResource(null!));

    [TestMethod]
    public void AddTestingPlatformCIResource_WithNullBuilder_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => OpenTelemetryProviderExtensions.AddTestingPlatformCIResource(null!));

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void AddTestingPlatformResource_PreservesAggregateResourceBehavior()
        => WithEnvironment(
            new()
            {
                ["TF_BUILD"] = "true",
                ["BUILD_DEFINITIONNAME"] = "testfx-ci",
                ["BUILD_BUILDID"] = "7",
                ["SYSTEM_JOBID"] = "job-guid",
                ["BUILD_SOURCEBRANCHNAME"] = "main",
                ["BUILD_SOURCEVERSION"] = "deadbeef",
                ["BUILD_REPOSITORY_URI"] = "https://user:token@dev.azure.com/org/_git/repo",
            },
            () =>
            {
                Dictionary<string, object> attributes = GetResourceAttributeMap(
                    ResourceBuilder.CreateEmpty().AddTestingPlatformResource().Build());

                Assert.IsTrue(attributes.TryGetValue("service.name", out object? serviceName));
                Assert.IsFalse(string.IsNullOrWhiteSpace(serviceName as string));
                Assert.IsTrue(attributes.TryGetValue("service.instance.id", out object? serviceInstanceId));
                Assert.IsFalse(string.IsNullOrWhiteSpace(serviceInstanceId as string));
                Assert.AreEqual(Environment.MachineName, attributes["host.name"]);
                Assert.AreEqual(".NET", attributes["process.runtime.name"]);
                Assert.AreEqual(Assembly.GetEntryAssembly()!.GetName().Name, attributes["test.assembly.name"]);
                Assert.AreEqual("azure_pipelines", attributes["cicd.provider.name"]);
                Assert.AreEqual("testfx-ci", attributes["cicd.pipeline.name"]);
                Assert.AreEqual("deadbeef", attributes["vcs.ref.head.revision"]);
                Assert.AreEqual("https://dev.azure.com/org/_git/repo", attributes["vcs.repository.url.full"]);
            });

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void AddTestingPlatformTestResource_AddsOnlyTestSpecificAttributes()
        => WithEnvironment(
            [],
            () =>
            {
                Dictionary<string, object> attributes = GetResourceAttributeMap(
                    ResourceBuilder.CreateEmpty().AddTestingPlatformTestResource().Build());

                Assert.HasCount(1, attributes);
                Assert.AreEqual(Assembly.GetEntryAssembly()!.GetName().Name, attributes["test.assembly.name"]);
                AssertDoesNotContainPrefixes(attributes, "service.", "host.", "os.", "process.", "cicd.", "vcs.");
            });

    [TestMethod]
    [DataRow("github_actions")]
    [DataRow("azure_pipelines")]
    [DataRow("gitlab")]
    [DataRow("jenkins")]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void AddTestingPlatformCIResource_EmitsExistingProviderMappingsWithoutApplicationOrTestIdentity(string provider)
    {
        Dictionary<string, string?> environment = provider switch
        {
            "github_actions" => new()
            {
                ["GITHUB_ACTIONS"] = "true",
                ["GITHUB_WORKFLOW"] = "CI",
                ["GITHUB_RUN_ID"] = "42",
                ["GITHUB_JOB"] = "build",
                ["GITHUB_REF_NAME"] = "main",
                ["GITHUB_SHA"] = "abc123",
                ["GITHUB_REPOSITORY"] = "microsoft/testfx",
            },
            "azure_pipelines" => new()
            {
                ["TF_BUILD"] = "true",
                ["BUILD_DEFINITIONNAME"] = "testfx-ci",
                ["BUILD_BUILDID"] = "7",
                ["SYSTEM_JOBID"] = "job-guid",
                ["BUILD_SOURCEBRANCHNAME"] = "main",
                ["BUILD_SOURCEVERSION"] = "deadbeef",
                ["BUILD_REPOSITORY_URI"] = "https://user:token@dev.azure.com/org/_git/repo",
            },
            "gitlab" => new()
            {
                ["GITLAB_CI"] = "true",
                ["CI_PIPELINE_NAME"] = "pipeline",
                ["CI_PIPELINE_ID"] = "9",
                ["CI_JOB_ID"] = "13",
                ["CI_COMMIT_REF_NAME"] = "feature",
                ["CI_COMMIT_SHA"] = "cafe",
                ["CI_REPOSITORY_URL"] = "https://gitlab.example.com/group/project.git",
            },
            "jenkins" => new()
            {
                ["JENKINS_URL"] = "https://jenkins.example.com/",
                ["JOB_NAME"] = "nightly",
                ["BUILD_NUMBER"] = "128",
                ["GIT_BRANCH"] = "origin/main",
                ["GIT_COMMIT"] = "1234abcd",
                ["GIT_URL"] = "https://github.com/microsoft/testfx.git",
            },
            _ => throw new InvalidOperationException($"Unknown provider '{provider}'."),
        };

        WithEnvironment(
            environment,
            () =>
            {
                Dictionary<string, object> attributes = GetResourceAttributeMap(
                    ResourceBuilder.CreateEmpty().AddTestingPlatformCIResource().Build());

                Assert.AreEqual(provider, attributes["cicd.provider.name"]);
                Assert.IsTrue(attributes.ContainsKey("cicd.pipeline.name"));
                Assert.IsTrue(attributes.ContainsKey("cicd.pipeline.run.id"));
                Assert.IsTrue(attributes.ContainsKey("vcs.ref.head.name"));
                Assert.IsTrue(attributes.ContainsKey("vcs.ref.head.revision"));
                AssertDoesNotContainPrefixes(attributes, "service.", "host.", "os.", "process.", "test.");

                if (provider == "github_actions")
                {
                    Assert.AreEqual("build", attributes["cicd.pipeline.task.name"]);
                    Assert.AreEqual("microsoft/testfx", attributes["vcs.repository.name"]);
                }
                else if (provider == "azure_pipelines")
                {
                    Assert.AreEqual("job-guid", attributes["cicd.pipeline.task.run.id"]);
                    Assert.AreEqual("https://dev.azure.com/org/_git/repo", attributes["vcs.repository.url.full"]);
                }
            });
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void FocusedResourceHelpers_PreserveApplicationOwnedIdentity()
        => WithEnvironment(
            new()
            {
                ["GITHUB_ACTIONS"] = "true",
                ["GITHUB_WORKFLOW"] = "CI",
            },
            () =>
            {
                Dictionary<string, object> attributes = GetResourceAttributeMap(
                    ResourceBuilder.CreateEmpty()
                        .AddService(
                            serviceName: "application-service",
                            serviceVersion: "1.2.3",
                            serviceInstanceId: "application-instance")
                        .AddAttributes(
                        [
                            new("host.name", "application-host"),
                            new("os.description", "application-os"),
                            new("process.pid", 123),
                        ])
                        .AddTestingPlatformTestResource()
                        .AddTestingPlatformCIResource()
                        .Build());

                Assert.AreEqual("application-service", attributes["service.name"]);
                Assert.AreEqual("1.2.3", attributes["service.version"]);
                Assert.AreEqual("application-instance", attributes["service.instance.id"]);
                Assert.AreEqual("application-host", attributes["host.name"]);
                Assert.AreEqual("application-os", attributes["os.description"]);
                Assert.AreEqual(123L, attributes["process.pid"]);
                Assert.IsTrue(attributes.ContainsKey("test.assembly.name"));
                Assert.AreEqual("github_actions", attributes["cicd.provider.name"]);
            });

    [TestMethod]
    public void AddOpenTelemetryProviderFromEnvironment_WithNullBuilder_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => ((ITestApplicationBuilder)null!).AddOpenTelemetryProviderFromEnvironment());

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
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

                var telemetryManager = (TelemetryManager)((TestApplicationBuilder)builder).Telemetry;
                using IPlatformOpenTelemetryService? service = telemetryManager.BuildOTelService(new ServiceProvider());
                IOpenTelemetryProvider? provider = telemetryManager.BuildOTelProvider(new ServiceProvider());
                using (provider)
                {
                    Assert.IsNotNull(service);
                    Assert.IsNotNull(provider);
                    Assert.IsTrue(tracingConfigured);
                }

                ITestApplicationBuilder disabledBuilder = await TestApplication.CreateBuilderAsync([]);
                Environment.SetEnvironmentVariable("OTEL_SDK_DISABLED", "true");
                disabledBuilder.AddOpenTelemetryProviderFromEnvironment();

                var disabledTelemetryManager = (TelemetryManager)((TestApplicationBuilder)disabledBuilder).Telemetry;
                using IPlatformOpenTelemetryService? disabledService = disabledTelemetryManager.BuildOTelService(new ServiceProvider());
                IOpenTelemetryProvider? disabledProvider = disabledTelemetryManager.BuildOTelProvider(new ServiceProvider());
                using (disabledProvider)
                {
                    Assert.IsNull(disabledService);
                    Assert.IsNull(disabledProvider);
                }
            });

    [TestMethod]
    [DataRow("diagnostics-only")]
    [DataRow("diagnostics-twice")]
    [DataRow("provider-only")]
    [DataRow("diagnostics-provider")]
    [DataRow("provider-diagnostics")]
    public async Task DiagnosticsAndProviderRegistration_IsIdempotentAndOrdered(string registrationOrder)
    {
        ITestApplicationBuilder builder = await CreateBuilderAsync();

        switch (registrationOrder)
        {
            case "diagnostics-only":
                builder.AddTestingPlatformDiagnostics();
                break;

            case "diagnostics-twice":
                builder.AddTestingPlatformDiagnostics();
                builder.AddTestingPlatformDiagnostics();
                break;

            case "provider-only":
                builder.AddOpenTelemetryProvider();
                break;

            case "diagnostics-provider":
                builder.AddTestingPlatformDiagnostics();
                builder.AddOpenTelemetryProvider();
                break;

            case "provider-diagnostics":
                builder.AddOpenTelemetryProvider();
                builder.AddTestingPlatformDiagnostics();
                break;

            default:
                throw new InvalidOperationException($"Unknown registration order '{registrationOrder}'.");
        }

        await AssertSingleDiagnosticsRegistrationAsync(builder, expectProvider: registrationOrder.Contains("provider", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public async Task DiagnosticsAndEnvironmentProviderRegistration_IsIdempotentAndOrdered(bool diagnosticsFirst)
        => await WithEnvironmentAsync(
            new()
            {
                ["OTEL_TRACES_EXPORTER"] = "none",
                ["OTEL_METRICS_EXPORTER"] = "none",
            },
            async () =>
            {
                ITestApplicationBuilder builder = await CreateBuilderAsync();
                if (diagnosticsFirst)
                {
                    builder.AddTestingPlatformDiagnostics();
                    builder.AddOpenTelemetryProviderFromEnvironment(configureTracing: _ => { });
                }
                else
                {
                    builder.AddOpenTelemetryProviderFromEnvironment(configureTracing: _ => { });
                    builder.AddTestingPlatformDiagnostics();
                }

                await AssertSingleDiagnosticsRegistrationAsync(builder, expectProvider: true);
            });

    [TestMethod]
    [DoNotParallelize]
    public async Task AddTestingPlatformDiagnostics_RawListenerObservesBuilderActivityWithoutProvider()
    {
        List<Activity> stoppedActivities = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OpenTelemetryPlatformService.ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (stoppedActivities)
                {
                    stoppedActivities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        ITestApplicationBuilder builder = await CreateBuilderAsync();
        builder.AddTestingPlatformDiagnostics();

        var application = (TestApplication)await builder.BuildAsync();
        var serviceProvider = (ServiceProvider)application.ServiceProvider;
        serviceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        try
        {
            Assert.IsNotNull(serviceProvider.GetServiceInternal<IPlatformOpenTelemetryService>());
            Assert.IsNull(serviceProvider.GetServiceInternal<IOpenTelemetryProvider>());
            Assert.Contains(
                activity => activity.OperationName == TestingPlatformSemanticConventions.Activities.TestHostBuilder,
                stoppedActivities);
        }
        finally
        {
            Assert.AreEqual(0, await application.RunAsync());
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task LegacyProviderFactorySideEffect_StillActivatesDiagnostics()
    {
        List<Activity> stoppedActivities = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OpenTelemetryPlatformService.ActivitySourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (stoppedActivities)
                {
                    stoppedActivities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        ITestApplicationBuilder builder = await CreateBuilderAsync();
        ((TelemetryManager)((TestApplicationBuilder)builder).Telemetry).AddOpenTelemetryProvider(serviceProvider =>
        {
            ((ServiceProvider)serviceProvider).AddService(new OpenTelemetryPlatformService());
            return new LegacyOpenTelemetryProvider();
        });

        var application = (TestApplication)await builder.BuildAsync();
        var serviceProvider = (ServiceProvider)application.ServiceProvider;
        serviceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        try
        {
            IPlatformOpenTelemetryService service = serviceProvider.GetRequiredService<IPlatformOpenTelemetryService>();
            LegacyOpenTelemetryProvider provider = Assert.IsInstanceOfType<LegacyOpenTelemetryProvider>(
                serviceProvider.GetServiceInternal<IOpenTelemetryProvider>());
            Assert.IsLessThan(serviceProvider.Services.ToList().IndexOf(provider), serviceProvider.Services.ToList().IndexOf(service));
            Assert.Contains(
                activity => activity.OperationName == TestingPlatformSemanticConventions.Activities.TestHostBuilder,
                stoppedActivities);
        }
        finally
        {
            Assert.AreEqual(0, await application.RunAsync());
        }
    }

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
        Assert.IsTrue(configuration.ConfigureTracingProvider);
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
    [DoNotParallelize]
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

    private static Dictionary<string, object> GetResourceAttributeMap(Resource resource)
    {
        Dictionary<string, object> attributes = [];
        foreach (KeyValuePair<string, object> attribute in resource.Attributes)
        {
            attributes[attribute.Key] = attribute.Value;
        }

        return attributes;
    }

    private static void AssertDoesNotContainPrefixes(Dictionary<string, object> attributes, params string[] prefixes)
    {
        foreach (string prefix in prefixes)
        {
            Assert.DoesNotContain(
                key => key.StartsWith(prefix, StringComparison.Ordinal),
                attributes.Keys,
                $"Resource unexpectedly contained an attribute with the '{prefix}' prefix.");
        }
    }

    private static async Task<ITestApplicationBuilder> CreateBuilderAsync()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(["--no-banner", "--ignore-exit-code", "8", "--internal-testingplatform-skipbuildercheck"]);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, _) => new MockTestFramework());
        return builder;
    }

    private static async Task AssertSingleDiagnosticsRegistrationAsync(ITestApplicationBuilder builder, bool expectProvider)
    {
        var application = (TestApplication)await builder.BuildAsync();
        var serviceProvider = (ServiceProvider)application.ServiceProvider;
        serviceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        try
        {
            IPlatformOpenTelemetryService service = serviceProvider.Services.OfType<IPlatformOpenTelemetryService>().Single();
            IOpenTelemetryProvider? provider = serviceProvider.Services.OfType<IOpenTelemetryProvider>().SingleOrDefault();

            Assert.IsNotNull(service);
            Assert.AreEqual(expectProvider, provider is not null);
            if (provider is not null)
            {
                Assert.IsLessThan(serviceProvider.Services.ToList().IndexOf(provider), serviceProvider.Services.ToList().IndexOf(service));
            }
        }
        finally
        {
            Assert.AreEqual(0, await application.RunAsync());
        }
    }

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

    private static void WithEnvironment(Dictionary<string, string?> values, Action body)
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

            body();
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

    private sealed class MockTestFramework : ITestFramework
    {
        public ICapability[] Capabilities => [];

        public string Uid => nameof(MockTestFramework);

        public string Version => "1.0.0";

        public string DisplayName => nameof(MockTestFramework);

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
            => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

        public Task ExecuteRequestAsync(ExecuteRequestContext context)
        {
            context.Complete();
            return Task.CompletedTask;
        }

        public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
            => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
    }

    private sealed class LegacyOpenTelemetryProvider : IOpenTelemetryProvider
    {
        public void Dispose()
        {
        }
    }
}
