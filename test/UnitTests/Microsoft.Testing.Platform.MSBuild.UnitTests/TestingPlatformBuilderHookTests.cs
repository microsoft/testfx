// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.MSBuild;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostOrchestrator;

using Moq;

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestClass]
public sealed class TestingPlatformBuilderHookTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void AddExtensions_ControllerPidOptionAtAnyPosition_RegistersFullMSBuildIntegration(int position)
    {
        List<Func<ICommandLineOptionsProvider>> providers = [];
        List<Func<IServiceProvider, ITestHostApplicationLifetime>> hostLifetimes = [];
        List<Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime>> orchestratorLifetimes = [];
        List<CompositeExtensionFactory<MSBuildConsumer>> dataConsumers = [];
        List<CompositeExtensionFactory<MSBuildConsumer>> sessionLifetimes = [];
        Mock<ICommandLineManager> commandLine = new(MockBehavior.Strict);
        Mock<ITestHostManager> testHost = new(MockBehavior.Strict);
        Mock<ITestHostOrchestratorManager> orchestrator = new(MockBehavior.Strict);
        Mock<ITestApplicationBuilder> builder = new(MockBehavior.Strict);
        builder.SetupGet(x => x.CommandLine).Returns(commandLine.Object);
        builder.SetupGet(x => x.TestHost).Returns(testHost.Object);
        builder.SetupGet(x => x.TestHostOrchestrator).Returns(orchestrator.Object);
        commandLine.Setup(x => x.AddProvider(It.IsAny<Func<ICommandLineOptionsProvider>>()))
            .Callback<Func<ICommandLineOptionsProvider>>(providers.Add);
        testHost.Setup(x => x.AddTestHostApplicationLifetime(It.IsAny<Func<IServiceProvider, ITestHostApplicationLifetime>>()))
            .Callback<Func<IServiceProvider, ITestHostApplicationLifetime>>(hostLifetimes.Add);
        orchestrator.Setup(x => x.AddTestHostOrchestratorApplicationLifetime(It.IsAny<Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime>>()))
            .Callback<Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime>>(orchestratorLifetimes.Add);
        testHost.Setup(x => x.AddDataConsumer(It.IsAny<CompositeExtensionFactory<MSBuildConsumer>>()))
            .Callback<CompositeExtensionFactory<MSBuildConsumer>>(dataConsumers.Add);
        testHost.Setup(x => x.AddTestSessionLifetimeHandler(It.IsAny<CompositeExtensionFactory<MSBuildConsumer>>()))
            .Callback<CompositeExtensionFactory<MSBuildConsumer>>(sessionLifetimes.Add);
        string[] arguments = ["other", "123", "last"];
        arguments[position] = "--internal-testhostcontroller-pid";

        TestingPlatformBuilderHook.AddExtensions(builder.Object, arguments);

        Assert.IsInstanceOfType<MSBuildCommandLineProvider>(Assert.ContainsSingle(providers)());
        Assert.HasCount(1, hostLifetimes);
        Assert.HasCount(1, orchestratorLifetimes);
        Assert.AreSame(Assert.ContainsSingle(dataConsumers), Assert.ContainsSingle(sessionLifetimes));
        commandLine.VerifyAll();
        testHost.VerifyAll();
        orchestrator.VerifyAll();
        builder.VerifyAll();
    }
}
