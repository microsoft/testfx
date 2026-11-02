// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.Tools;

using Moq;

using OrchestratorManager = Microsoft.Testing.Platform.TestHostOrchestrator.ITestHostOrchestratorManager;
using TestSessionContext = Microsoft.Testing.Platform.Services.TestSessionContext;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class RetryTrxRegistrationBehaviorTests
{
    [TestMethod]
    [DataRow(false, DisplayName = "RetryRegistration_Direct")]
    [DataRow(true, DisplayName = "RetryRegistration_BuilderHook")]
    public async Task Retry_BuilderHookSharesInitializedHostRoles(bool useHook)
    {
        using var fixture = new TrxBehaviorFixture();
        fixture.Options[RetryCommandLineOptionsProvider.RetryFailedTestsOptionName] = ["2"];
        fixture.Options[RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName] = ["never-connected"];
        var host = new Mock<ITestHostManager>(MockBehavior.Strict);
        var commandLine = new Mock<ICommandLineManager>(MockBehavior.Strict);
        var orchestrators = new Mock<OrchestratorManager>(MockBehavior.Strict);
        var builder = new Mock<ITestApplicationBuilder>(MockBehavior.Strict);
        builder.SetupGet(value => value.TestHost).Returns(host.Object);
        builder.SetupGet(value => value.CommandLine).Returns(commandLine.Object);
        builder.SetupGet(value => value.TestHostOrchestrator).Returns(orchestrators.Object);
        Func<IServiceProvider, ITestHostApplicationLifetime>? lifecycleFactory = null;
        CompositeExtensionFactory<RetryDataConsumer>? consumerFactory = null;
        CompositeExtensionFactory<RetryDataConsumer>? sessionFactory = null;
        Func<IServiceProvider, ITestHostExecutionOrchestrator>? orchestratorFactory = null;
        Func<ICommandLineOptionsProvider>? commandFactory = null;
        host.Setup(value => value.AddTestHostApplicationLifetime(It.IsAny<Func<IServiceProvider, ITestHostApplicationLifetime>>()))
            .Callback((Func<IServiceProvider, ITestHostApplicationLifetime> factory) => lifecycleFactory = factory);
        host.Setup(value => value.AddDataConsumer(It.IsAny<CompositeExtensionFactory<RetryDataConsumer>>()))
            .Callback((CompositeExtensionFactory<RetryDataConsumer> factory) => consumerFactory = factory);
        host.Setup(value => value.AddTestSessionLifetimeHandler(It.IsAny<CompositeExtensionFactory<RetryDataConsumer>>()))
            .Callback((CompositeExtensionFactory<RetryDataConsumer> factory) => sessionFactory = factory);
        orchestrators.Setup(value => value.AddTestHostOrchestrator(It.IsAny<Func<IServiceProvider, ITestHostExecutionOrchestrator>>()))
            .Callback((Func<IServiceProvider, ITestHostExecutionOrchestrator> factory) => orchestratorFactory = factory);
        commandLine.Setup(value => value.AddProvider(It.IsAny<Func<ICommandLineOptionsProvider>>()))
            .Callback((Func<ICommandLineOptionsProvider> factory) => commandFactory = factory);

        if (useHook)
        {
            Retry.TestingPlatformBuilderHook.AddExtensions(builder.Object, ["unused"]);
        }
        else
        {
            builder.Object.AddRetryProvider();
        }

        using RetryLifecycleCallbacks lifecycle = Assert.IsInstanceOfType<RetryLifecycleCallbacks>(lifecycleFactory!(fixture.Services));
        fixture.Services.AddService(lifecycle);
        var consumer = (RetryDataConsumer)((ICompositeExtensionFactory)consumerFactory!).GetInstance(fixture.Services);
        Assert.AreSame(consumerFactory, sessionFactory);
        Assert.AreSame(consumer, ((ICompositeExtensionFactory)sessionFactory!).GetInstance(fixture.Services));
        await consumer.InitializeAsync();
        await consumer.OnTestSessionStartingAsync(new TestSessionContext(CancellationToken.None));
        Assert.IsTrue(await lifecycle.IsEnabledAsync());
        Assert.IsTrue(await consumer.IsEnabledAsync());
        Assert.IsTrue(await orchestratorFactory!(fixture.Services).IsEnabledAsync());
        ICommandLineOptionsProvider provider = commandFactory!();
        Assert.IsInstanceOfType<RetryCommandLineOptionsProvider>(provider);
        Assert.Contains(RetryCommandLineOptionsProvider.RetryFailedTestsOptionName, provider.GetCommandLineOptions().Select(value => value.Name));
        Assert.AreEqual("RetryLifecycleCallbacks", lifecycle.Uid);
        Assert.AreEqual(provider.Version, lifecycle.Version);
        Assert.AreEqual(Policy.Resources.ExtensionResources.RetryFailedTestsExtensionDisplayName, lifecycle.DisplayName);
        Assert.AreEqual(Policy.Resources.ExtensionResources.RetryFailedTestsExtensionDescription, lifecycle.Description);
        Assert.IsNull(lifecycle.Client, "Registration/initialization must not connect or bind transport.");
        host.Verify(value => value.AddTestHostApplicationLifetime(It.IsAny<Func<IServiceProvider, ITestHostApplicationLifetime>>()), Times.Once);
        host.Verify(value => value.AddDataConsumer(It.IsAny<CompositeExtensionFactory<RetryDataConsumer>>()), Times.Once);
        host.Verify(value => value.AddTestSessionLifetimeHandler(It.IsAny<CompositeExtensionFactory<RetryDataConsumer>>()), Times.Once);
        commandLine.Verify(value => value.AddProvider(It.IsAny<Func<ICommandLineOptionsProvider>>()), Times.Once);
        orchestrators.Verify(value => value.AddTestHostOrchestrator(It.IsAny<Func<IServiceProvider, ITestHostExecutionOrchestrator>>()), Times.Once);
        host.VerifyNoOtherCalls();
        commandLine.VerifyNoOtherCalls();
        orchestrators.VerifyNoOtherCalls();
        Assert.IsEmpty(fixture.Logs);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "TrxRegistration_Direct")]
    [DataRow(true, DisplayName = "TrxRegistration_BuilderHook")]
    public async Task Trx_RegistrationMaterializesSharedRolesAndBothTools(bool useHook)
    {
        using var fixture = new TrxBehaviorFixture();
        var controllers = new TestHostControllersManager();
        var host = new Mock<ITestHostManager>(MockBehavior.Strict);
        var commandLine = new Mock<ICommandLineManager>(MockBehavior.Strict);
        var tools = new Mock<IToolsManager>(MockBehavior.Strict);
        var postProcessing = new Mock<IArtifactPostProcessingManager>(MockBehavior.Strict);
        var builder = new Mock<IArtifactPostProcessingApplicationBuilder>(MockBehavior.Strict);
        builder.SetupGet(value => value.TestHost).Returns(host.Object);
        builder.SetupGet(value => value.TestHostControllers).Returns(controllers);
        builder.SetupGet(value => value.CommandLine).Returns(commandLine.Object);
        builder.SetupGet(value => value.Tools).Returns(tools.Object);
        builder.SetupGet(value => value.ArtifactPostProcessing).Returns(postProcessing.Object);
        Func<IServiceProvider, ITestHostApplicationLifetime>? lifecycleFactory = null;
        CompositeExtensionFactory<TrxReportGenerator>? consumerFactory = null;
        CompositeExtensionFactory<TrxReportGenerator>? sessionFactory = null;
        List<Func<ICommandLineOptionsProvider>> commands = [];
        List<Func<IServiceProvider, ITool>> toolFactories = [];
        Func<IServiceProvider, IArtifactPostProcessor>? postProcessorFactory = null;
        host.Setup(value => value.AddTestHostApplicationLifetime(It.IsAny<Func<IServiceProvider, ITestHostApplicationLifetime>>()))
            .Callback((Func<IServiceProvider, ITestHostApplicationLifetime> factory) => lifecycleFactory = factory);
        host.Setup(value => value.AddDataConsumer(It.IsAny<CompositeExtensionFactory<TrxReportGenerator>>()))
            .Callback((CompositeExtensionFactory<TrxReportGenerator> factory) => consumerFactory = factory);
        host.Setup(value => value.AddTestSessionLifetimeHandler(It.IsAny<CompositeExtensionFactory<TrxReportGenerator>>()))
            .Callback((CompositeExtensionFactory<TrxReportGenerator> factory) => sessionFactory = factory);
        commandLine.Setup(value => value.AddProvider(It.IsAny<Func<ICommandLineOptionsProvider>>()))
            .Callback((Func<ICommandLineOptionsProvider> factory) => commands.Add(factory));
        tools.Setup(value => value.AddTool(It.IsAny<Func<IServiceProvider, ITool>>()))
            .Callback((Func<IServiceProvider, ITool> factory) => toolFactories.Add(factory));
        postProcessing.Setup(value => value.AddArtifactPostProcessor(It.IsAny<Func<IServiceProvider, IArtifactPostProcessor>>()))
            .Callback((Func<IServiceProvider, IArtifactPostProcessor> factory) => postProcessorFactory = factory);

        if (useHook)
        {
            TrxReport.TestingPlatformBuilderHook.AddExtensions(builder.Object, ["unused"]);
        }
        else
        {
            builder.Object.AddTrxReportProvider();
        }

        using TrxTestApplicationLifecycleCallbacks lifecycle = Assert.IsInstanceOfType<TrxTestApplicationLifecycleCallbacks>(lifecycleFactory!(fixture.Services));
        fixture.Services.AddService(lifecycle);
        using var generator = (TrxReportGenerator)((ICompositeExtensionFactory)consumerFactory!).GetInstance(fixture.Services);
        Assert.AreSame(consumerFactory, sessionFactory);
        Assert.AreSame(generator, ((ICompositeExtensionFactory)sessionFactory!).GetInstance(fixture.Services));
        Assert.IsFalse(await lifecycle.IsEnabledAsync(), "A controller provider does not imply that this process is already its child.");
        Assert.IsTrue(await generator.IsEnabledAsync());
        var session = new TestSessionContext(CancellationToken.None);
        await generator.OnTestSessionStartingAsync(session);
        await fixture.FinishAsync(generator, session);
        Assert.AreSame(generator, Assert.ContainsSingle(fixture.Publications).Producer);

        TestHostControllerConfiguration configuration = await controllers.BuildAsync(fixture.Services);
        Assert.IsTrue(configuration.RequireProcessRestart);
        using TrxProcessLifetimeHandler controller = Assert.IsInstanceOfType<TrxProcessLifetimeHandler>(Assert.ContainsSingle(configuration.LifetimeHandlers));
        Assert.AreSame(controller, Assert.ContainsSingle(configuration.DataConsumer));
        Assert.AreEqual("TrxProcessLifetimeHandler", controller.Uid);
        Assert.AreEqual(generator.Version, controller.Version);
        Assert.AreEqual(string.Empty, controller.DisplayName);
        Assert.AreEqual(string.Empty, controller.Description);
        Assert.AreSequenceEqual(new[] { typeof(FileArtifact) }, controller.DataTypesConsumed);
        Assert.AreSequenceEqual(new[] { typeof(FileArtifact) }, controller.DataTypesProduced);
        ITestHostEnvironmentVariableProvider environment = Assert.ContainsSingle(configuration.EnvironmentVariableProviders);
        Assert.IsInstanceOfType<TrxEnvironmentVariableProvider>(environment);
        Assert.IsTrue(await environment.IsEnabledAsync());
        var variables = new EnvironmentVariables(fixture.LoggerFactory.Object) { CurrentProvider = environment };
        await environment.UpdateAsync(variables);
        Assert.IsTrue(variables.TryGetVariable(TrxEnvironmentVariableProvider.TRXNAMEDPIPENAME, out OwnedEnvironmentVariable? pipe));
        Assert.AreSame(environment, pipe.Owner);
        Assert.IsNotNull(pipe.Value);
        Assert.IsNotEmpty(pipe.Value);
        Assert.IsTrue(pipe.IsLocked);
        Assert.IsFalse(pipe.IsSecret);
        Assert.IsTrue((await environment.ValidateTestHostEnvironmentVariablesAsync(variables)).IsValid);

        ICommandLineOptionsProvider[] providers = [.. commands.Select(factory => factory())];
        Assert.HasCount(3, providers);
        Assert.AreSequenceEqual(["report-trx", "report-trx-filename"], providers[0].GetCommandLineOptions().Select(value => value.Name));
        ITool[] registeredTools = [.. toolFactories.Select(factory => factory(fixture.Services))];
        Assert.AreSequenceEqual([TrxCompareTool.ToolName, TrxMergeTool.ToolName], registeredTools.Select(value => value.Name));
        Assert.AreSequenceEqual(registeredTools.Select(value => value.Uid), providers.Skip(1).Select(value => value.Uid));
        Assert.AreSequenceEqual(registeredTools.Select(value => value.Name), providers.Skip(1).Cast<IToolCommandLineOptionsProvider>().Select(value => value.ToolName));
        // Use the platform's real informational consumer. A wrong ToolName would otherwise leave
        // a registered provider orphaned even though its factory and scoped validation look healthy.
        var handler = new CommandLineHandler(
            new CommandLineParseResult(null, [], []), providers, [], fixture.Module.Object, Mock.Of<IRuntimeFeature>());
        await handler.PrintInfoAsync(fixture.Output.Object, registeredTools, CancellationToken.None);
        string[] rendered = [.. fixture.Displays.Select(value => value.Data is FormattedTextOutputDeviceData formatted
            ? new string(' ', formatted.Padding ?? 0) + formatted.Text
            : Assert.IsInstanceOfType<TextOutputDeviceData>(value.Data).Text)];
        int toolSection = Array.IndexOf(rendered, "Registered tools:");
        Assert.IsGreaterThanOrEqualTo(0, toolSection);
        Assert.AreSequenceEqual(
            [
                "Registered tools:",
                .. ExpectedToolInfo(
                    "Microsoft.Testing.Extensions.TrxReport.MergeTool", "merge-trx",
                    TrxReport.Resources.ExtensionResources.TrxMergeToolDisplayName, registeredTools[1].Version,
                    TrxReport.Resources.ExtensionResources.TrxMergeToolDescription,
                    [
                        .. ExpectedOptionInfo("input", "2..N", TrxReport.Resources.ExtensionResources.TrxMergeToolInputOptionDescription),
                        .. ExpectedOptionInfo("output-trx", "1", TrxReport.Resources.ExtensionResources.TrxMergeToolOutputOptionDescription),
                    ]),
                .. ExpectedToolInfo(
                    "TrxCompareTool", "ms-trxcompare",
                    TrxReport.Resources.ExtensionResources.TrxComparerToolDisplayName, registeredTools[0].Version,
                    TrxReport.Resources.ExtensionResources.TrxComparerToolDescription,
                    [
                        .. ExpectedOptionInfo("baseline-trx", "1", TrxReport.Resources.ExtensionResources.TrxComparerToolBaselineFileOptionDescription),
                        .. ExpectedOptionInfo("trx-to-compare", "1", TrxReport.Resources.ExtensionResources.TrxComparerToolOtherFileOptionDescription),
                    ]),
                string.Empty,
            ],
            rendered.Skip(toolSection));
        IArtifactPostProcessor processor = postProcessorFactory!(fixture.Services);
        Assert.IsInstanceOfType<TrxArtifactPostProcessor>(processor);
        Assert.IsNull(await processor.ProcessAsync([], fixture.Root,
            new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None), CancellationToken.None));
        tools.Verify(value => value.AddTool(It.IsAny<Func<IServiceProvider, ITool>>()), Times.Exactly(2));
        postProcessing.Verify(value => value.AddArtifactPostProcessor(It.IsAny<Func<IServiceProvider, IArtifactPostProcessor>>()), Times.Once);
        commandLine.Verify(value => value.AddProvider(It.IsAny<Func<ICommandLineOptionsProvider>>()), Times.Exactly(3));
        host.Verify(value => value.AddTestHostApplicationLifetime(It.IsAny<Func<IServiceProvider, ITestHostApplicationLifetime>>()), Times.Once);
        host.Verify(value => value.AddDataConsumer(It.IsAny<CompositeExtensionFactory<TrxReportGenerator>>()), Times.Once);
        host.Verify(value => value.AddTestSessionLifetimeHandler(It.IsAny<CompositeExtensionFactory<TrxReportGenerator>>()), Times.Once);
        tools.VerifyNoOtherCalls();
        postProcessing.VerifyNoOtherCalls();
        host.VerifyNoOtherCalls();
        commandLine.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void Trx_UnsupportedBuilderRejectsWithoutPartialRegistration()
    {
        var builder = new Mock<ITestApplicationBuilder>(MockBehavior.Strict);

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(builder.Object.AddTrxReportProvider);

        Assert.AreEqual(TrxReport.Resources.ExtensionResources.InvalidTestApplicationBuilderType, exception.Message);
        builder.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task Trx_EnvironmentProviderTracksEndpointAndValidatesPresenceOnly()
    {
        using var fixture = new TrxBehaviorFixture();
        var endpoint = new NamedPipeServerEndpoint("initial");
        var provider = new TrxEnvironmentVariableProvider(fixture.CommandLine, endpoint);
        var variables = new EnvironmentVariables(fixture.LoggerFactory.Object) { CurrentProvider = provider };
        Assert.IsTrue(await provider.IsEnabledAsync());
        fixture.Options.Remove(TrxReportGeneratorCommandLine.TrxReportOptionName);
        Assert.IsFalse(await provider.IsEnabledAsync());
        ValidationResult missing = await provider.ValidateTestHostEnvironmentVariablesAsync(variables);
        Assert.IsFalse(missing.IsValid);
        Assert.AreEqual(
            string.Format(
            CultureInfo.InvariantCulture,
            TrxReport.Resources.ExtensionResources.TrxReportGeneratorMissingTrxNamedPipeEnvironmentVariable,
            TrxEnvironmentVariableProvider.TRXNAMEDPIPENAME), missing.ErrorMessage);

        endpoint.PipeName = "effective-controller-pipe";
        await provider.UpdateAsync(variables);
        Assert.IsTrue(variables.TryGetVariable(TrxEnvironmentVariableProvider.TRXNAMEDPIPENAME, out OwnedEnvironmentVariable? pipe));
        Assert.AreEqual("effective-controller-pipe", pipe.Value);
        Assert.AreSame(provider, pipe.Owner);
        Assert.IsTrue(pipe.IsLocked);
        Assert.IsFalse(pipe.IsSecret);
        var otherVariables = new EnvironmentVariables(fixture.LoggerFactory.Object) { CurrentProvider = provider };
        otherVariables.SetVariable(new EnvironmentVariable(TrxEnvironmentVariableProvider.TRXNAMEDPIPENAME, "different-child-value", isSecret: false, isLocked: true));
        Assert.IsTrue(
            (await provider.ValidateTestHostEnvironmentVariablesAsync(otherVariables)).IsValid,
            "TRX deliberately validates presence only; equality is not its child-environment contract.");
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, true, true)]
    [DataRow(false, false, true)]
    public async Task TrxLifecycle_DisabledAndCanceledStartupNeverConsultTransport(bool report, bool controller, bool canceled)
    {
        using var fixture = new TrxBehaviorFixture();
        if (!report)
        {
            fixture.Options.Remove(TrxReportGeneratorCommandLine.TrxReportOptionName);
        }

        if (controller)
        {
            fixture.Options[PlatformCommandLineProvider.TestHostControllerPIDOptionKey] = ["42"];
        }

        var environment = new Mock<Microsoft.Testing.Platform.Helpers.IEnvironment>(MockBehavior.Strict);
        using var lifecycle = new TrxTestApplicationLifecycleCallbacks(fixture.CommandLine, environment.Object);
        using var cancellation = new CancellationTokenSource();
        Assert.AreEqual(report && controller, await lifecycle.IsEnabledAsync());
        if (canceled)
        {
            cancellation.Cancel();
            OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => lifecycle.BeforeRunAsync(cancellation.Token));
            Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        }
        else
        {
            await lifecycle.BeforeRunAsync(cancellation.Token);
        }

        await lifecycle.AfterRunAsync(17, cancellation.Token);
        Assert.IsNull(lifecycle.NamedPipeClient);
        environment.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TrxLifecycle_EnabledWithoutPipeReportsActionableError()
    {
        using var fixture = new TrxBehaviorFixture();
        fixture.Options[PlatformCommandLineProvider.TestHostControllerPIDOptionKey] = ["42"];
        using var lifecycle = new TrxTestApplicationLifecycleCallbacks(fixture.CommandLine, fixture.Environment.Object);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => lifecycle.BeforeRunAsync(CancellationToken.None));

        Assert.AreEqual(
            string.Format(
            CultureInfo.InvariantCulture,
            TrxReport.Resources.ExtensionResources.TrxReportGeneratorMissingTrxNamedPipeEnvironmentVariable,
            TrxEnvironmentVariableProvider.TRXNAMEDPIPENAME), exception.Message);
        Assert.IsNull(lifecycle.NamedPipeClient);
        fixture.Environment.Verify(value => value.GetEnvironmentVariable(TrxEnvironmentVariableProvider.TRXNAMEDPIPENAME), Times.Once);
    }

    [TestMethod]
    public async Task TrxController_UnpreparedOrCanceledCallbacksDoNotInspectProcess()
    {
        using var fixture = new TrxBehaviorFixture();
        using var handler = new TrxProcessLifetimeHandler(
            fixture.CommandLine, fixture.Environment.Object, fixture.LoggerFactory.Object, fixture.MessageBus.Object,
            fixture.FileSystem.Object, fixture.Module.Object, fixture.Configuration.Object, fixture.Clock.Object,
            fixture.Scheduler, fixture.Output.Object, fixture.Services, new NamedPipeServerEndpoint("never-bound"));
        var process = new Mock<ITestHostProcessInformation>(MockBehavior.Strict);

        InvalidOperationException missingStart = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => handler.OnTestHostProcessStartedAsync(process.Object, CancellationToken.None));
        Assert.AreEqual(TrxReport.Resources.ExtensionResources.TrxReportGeneratorBeforeTestHostProcessStartAsyncNotCalled, missingStart.Message);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        OperationCanceledException canceled = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.OnTestHostProcessExitedAsync(process.Object, cancellation.Token));
        Assert.AreEqual(cancellation.Token, canceled.CancellationToken);
        handler.Dispose();
#if NETCOREAPP
        await handler.DisposeAsync();
#endif
        process.VerifyNoOtherCalls();
        fixture.FileSystem.VerifyNoOtherCalls();
        Assert.IsEmpty(fixture.Displays);
        Assert.IsEmpty(fixture.Publications);
    }

    private static string[] ExpectedToolInfo(
        string uid, string command, string displayName, string version, string description, string[] options)
        =>
        [
            "  " + uid,
            "    Command: " + command,
            "    Name: " + displayName,
            "    Version: " + version,
            "    Description: " + description,
            "    Tool command line providers:",
            "      " + uid,
            "        Name: " + displayName,
            "        Version: " + version,
            "        Description: " + description,
            "        Options:",
            .. options,
        ];

    private static string[] ExpectedOptionInfo(string name, string arity, string description)
        =>
        [
            "          --" + name,
            "            Arity: " + arity,
            "            Hidden: False",
            "            Description: " + description,
        ];
}
