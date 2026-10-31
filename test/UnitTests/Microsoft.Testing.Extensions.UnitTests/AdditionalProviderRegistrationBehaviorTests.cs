// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ctrf;

using Azure.Core;

using Microsoft.Testing.Extensions.AzureFoundry;
using Microsoft.Testing.Extensions.OpenTelemetry;
using Microsoft.Testing.Extensions.Telemetry;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.AI;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AdditionalProviderRegistrationBehaviorTests
{
    [TestMethod]
    [DataRow("html")]
    [DataRow("junit")]
    [DataRow("ctrf")]
    public async Task ReportHook_MaterializesSharedGeneratorRecoveryAndPostProcessor(string format)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        string option = "report-" + format;
        var fixture = new ReportBehaviorFixture(new() { [option] = [] });
        AddReportHook(builder, format);
        var host = (TestHostManager)builder.TestHost;
        List<ICompositeExtensionFactory> built = [];

        IExtension consumer = Assert.ContainsSingle((await host.BuildDataConsumersAsync(fixture.Services, built)).Select(value => value.Consumer));
        IExtension lifetime = Assert.ContainsSingle((await host.BuildTestSessionLifetimeHandleAsync(fixture.Services, built)).Select(value => value.TestSessionLifetimeHandler));
        Assert.AreSame(consumer, lifetime);
        IDataConsumer generator = Assert.IsInstanceOfType<IDataConsumer>(consumer);
        ITestSessionLifetimeHandler sessionHandler = Assert.IsInstanceOfType<ITestSessionLifetimeHandler>(lifetime);
        var session = new ReportBehaviorFixture.Session();
        await sessionHandler.OnTestSessionStartingAsync(session);
        await generator.ConsumeAsync(
            null!,
            new TestNodeUpdateMessage(new SessionUid("source"), new TestNode { Uid = "registered", DisplayName = "Registered", Properties = new(PassedTestNodeStateProperty.CachedInstance) }),
            CancellationToken.None);
        await fixture.FinishAsync(sessionHandler, session);

        SessionFileArtifact artifact = Assert.IsInstanceOfType<SessionFileArtifact>(Assert.ContainsSingle(fixture.Artifacts));
        Assert.AreEqual("report-session", artifact.SessionUid.Value);
        Assert.AreEqual("microsoft.testing." + format, artifact.Kind);
        TestHostControllerConfiguration controllers = await ((TestHostControllersManager)builder.TestHostControllers).BuildAsync(fixture.Services);
        Assert.ContainsSingle(controllers.EnvironmentVariableProviders);
        Assert.ContainsSingle(controllers.LifetimeHandlers);
        Assert.IsTrue(controllers.RequireProcessRestart);
        Assert.IsTrue(await controllers.EnvironmentVariableProviders[0].IsEnabledAsync());
        Assert.IsTrue(await controllers.LifetimeHandlers[0].IsEnabledAsync());
        IReadOnlyList<IArtifactPostProcessor> processors = await ((ArtifactPostProcessingManager)((IArtifactPostProcessingApplicationBuilder)builder).ArtifactPostProcessing).BuildAsync(fixture.Services);
        Assert.AreSequenceEqual(new[] { "microsoft.testing." + format }, Assert.ContainsSingle(processors).SupportedKinds);
        CommandLineHandler commandLine = await ((CommandLineManager)builder.CommandLine).BuildAsync(null!, fixture.Services, fixture.Configuration.Object);
        ICommandLineOptionsProvider provider = commandLine.ExtensionsCommandLineOptionsProviders.Single(value => value.GetCommandLineOptions().Any(value => value.Name == option));
        Assert.AreSequenceEqual(new[] { option, option + "-filename" }, provider.GetCommandLineOptions().Select(value => value.Name));
        (consumer as IDisposable)?.Dispose();
    }

    [TestMethod]
    [DataRow("html")]
    [DataRow("junit")]
    [DataRow("ctrf")]
    public void ReportProvider_UnsupportedBuilderRejectsWithoutPartialRegistration(string format)
    {
        var builder = new Mock<ITestApplicationBuilder>(MockBehavior.Strict);
        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() => AddReportHook(builder.Object, format));
        string expected = format switch
        {
            "html" => HtmlReport.Resources.ExtensionResources.InvalidTestApplicationBuilderType,
            "junit" => JUnitReport.Resources.ExtensionResources.JUnitReportRequiresArtifactPostProcessing,
            _ => CtrfReport.Resources.ExtensionResources.InvalidTestApplicationBuilderType,
        };
        Assert.AreEqual(expected, error.Message);
        builder.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task NonJournalRegistration_SharesOneUsableInstance()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        var fixture = new ReportBehaviorFixture();
        var extension = new RecordingReport();
        int constructions = 0;
        var commandLine = new Mock<ICommandLineOptionsProvider>();
        commandLine.SetupGet(value => value.Uid).Returns("recording-cli");
        commandLine.Setup(value => value.IsEnabledAsync()).ReturnsAsync(true);
        commandLine.Setup(value => value.GetCommandLineOptions())
            .Returns([new CommandLineOption("recording", "recording report", ArgumentArity.Zero, false)]);
        ctrf::Microsoft.Testing.Extensions.ReportProviderRegistration.AddReportProvider(
            builder, "wrong builder", () => commandLine.Object,
            _ =>
            {
                constructions++;
                return extension;
            });
        List<ICompositeExtensionFactory> built = [];
        var host = (TestHostManager)builder.TestHost;
        IExtension consumer = Assert.ContainsSingle((await host.BuildDataConsumersAsync(fixture.Services, built)).Select(value => value.Consumer));
        IExtension lifetime = Assert.ContainsSingle((await host.BuildTestSessionLifetimeHandleAsync(fixture.Services, built)).Select(value => value.TestSessionLifetimeHandler));
        Assert.AreSame(consumer, lifetime);
        var session = new ReportBehaviorFixture.Session();
        IData message = Mock.Of<IData>();

        await ((ITestSessionLifetimeHandler)lifetime).OnTestSessionStartingAsync(session);
        await ((IDataConsumer)consumer).ConsumeAsync(null!, message, CancellationToken.None);
        await ((ITestSessionLifetimeHandler)lifetime).OnTestSessionFinishingAsync(session);

        Assert.AreEqual(1, constructions);
        Assert.AreSame(message, extension.Message);
        Assert.AreSequenceEqual(new[] { "start:report-session", "consume", "finish:report-session" }, extension.Events);
        CommandLineHandler handler = await ((CommandLineManager)builder.CommandLine).BuildAsync(null!, fixture.Services, fixture.Configuration.Object);
        Assert.AreEqual("recording", handler.ExtensionsCommandLineOptionsProviders.Single(value => value.Uid == "recording-cli").GetCommandLineOptions().Single().Name);
        var unsupported = new Mock<ITestApplicationBuilder>(MockBehavior.Strict);
        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            ctrf::Microsoft.Testing.Extensions.ReportProviderRegistration.AddReportProvider(unsupported.Object, "wrong builder", () => commandLine.Object, _ => extension));
        Assert.AreEqual("wrong builder", error.Message);
        unsupported.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public async Task AzureProvider_RegistrationAndHookMaterializeUsableFactory(int mode)
    {
        var builder = (TestApplicationBuilder)await TestApplication.CreateBuilderAsync([]);
        if (mode == 0)
        {
            builder.AddAzureOpenAIChatClientProvider();
        }
        else if (mode == 1)
        {
            AzureFoundry.TestingPlatformBuilderHook.AddExtensions(builder, ["unused"]);
        }
        else
        {
            builder.AddAzureOpenAIChatClientProvider(Mock.Of<TokenCredential>(MockBehavior.Strict));
        }

        var services = new ServiceProvider();
        builder.ChatClientManager.BuildChatClients(services);

        Assert.IsInstanceOfType<AzureOpenAIChatClientProvider>(services.GetRequiredService<IChatClientProvider>());
        _ = Assert.ThrowsExactly<InvalidOperationException>(() => builder.AddAzureOpenAIChatClientProvider());
    }

    [TestMethod]
    public async Task AzureProvider_NullCredentialIsNotSilentlyDropped()
    {
        var builder = (TestApplicationBuilder)await TestApplication.CreateBuilderAsync([]);
        builder.AddAzureOpenAIChatClientProvider(null!);

        ArgumentNullException error = Assert.ThrowsExactly<ArgumentNullException>(() => builder.ChatClientManager.BuildChatClients(new ServiceProvider()));

        Assert.AreEqual("credential", error.ParamName);
    }

    [TestMethod]
    public async Task OpenTelemetryHook_ActivatesDiagnosticsWithoutAnExporter()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        OpenTelemetry.TestingPlatformBuilderHook.AddExtensions(builder, []);
        OpenTelemetry.TestingPlatformBuilderHook.AddExtensions(builder, ["unused"]);
        var manager = (TelemetryManager)((TestApplicationBuilder)builder).Telemetry;
        using IPlatformOpenTelemetryService service = Assert.IsInstanceOfType<OpenTelemetryPlatformService>(manager.BuildOTelService(new ServiceProvider()));

        service.CreateCounter<int>("registration-counter").Add(3);

        Assert.IsNull(manager.BuildOTelProvider(new ServiceProvider()));
    }

    [TestMethod]
    public async Task TelemetryHook_PropagatesSessionWithoutStartingARealIngestLoop()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        Telemetry.TestingPlatformBuilderHook.AddExtensions(builder, []);
        var fixture = new ReportBehaviorFixture(new() { ["no-banner"] = [] });
        fixture.Environment.Setup(value => value.GetEnvironmentVariable(AppInsightsProvider.SessionIdEnvVar)).Returns("inherited-session");
        fixture.Services.AddService(Mock.Of<ITestApplicationCancellationTokenSource>());
        fixture.TaskService.Setup(value => value.Run(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        fixture.TaskService.Setup(value => value.RunLongRunning(It.IsAny<Func<Task>>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        ITelemetryCollector collector = await ((TelemetryManager)((TestApplicationBuilder)builder).Telemetry).BuildTelemetryAsync(
            fixture.Services, fixture.LoggerFactory.Object, new TestApplicationOptions { EnableTelemetry = true });

        AppInsightsProvider provider = Assert.IsInstanceOfType<AppInsightsProvider>(collector);
        fixture.Environment.Verify(value => value.SetEnvironmentVariable(AppInsightsProvider.SessionIdEnvVar, "inherited-session"), Times.Once);
#if NETCOREAPP
        await provider.DisposeAsync();
#else
        provider.Dispose();
#endif
    }

    private static void AddReportHook(ITestApplicationBuilder builder, string format)
    {
        switch (format)
        {
            case "html":
                HtmlReport.TestingPlatformBuilderHook.AddExtensions(builder, []);
                break;
            case "junit":
                JUnitReport.TestingPlatformBuilderHook.AddExtensions(builder, []);
                break;
            default:
                CtrfReport.TestingPlatformBuilderHook.AddExtensions(builder, []);
                break;
        }
    }

    private sealed class RecordingReport : IDataConsumer, ITestSessionLifetimeHandler
    {
        public string Uid => "recording-report";

        public string Version => "1.0";

        public string DisplayName => "Recording";

        public string Description => "Recording report";

        public Type[] DataTypesConsumed => [typeof(IData)];

        public List<string> Events { get; } = [];

        public IData? Message { get; private set; }

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
        {
            Events.Add("consume");
            Message = value;
            return Task.CompletedTask;
        }

        public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
        {
            Events.Add("start:" + testSessionContext.SessionUid.Value);
            return Task.CompletedTask;
        }

        public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
        {
            Events.Add("finish:" + testSessionContext.SessionUid.Value);
            return Task.CompletedTask;
        }
    }
}
