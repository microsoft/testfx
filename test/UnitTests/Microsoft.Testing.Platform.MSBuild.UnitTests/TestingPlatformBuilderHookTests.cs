// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.MSBuild;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
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
    public void AddExtensions_ControllerPidOptionAtAnyPosition_RegistersOnlyCommandLineProvider(int position)
    {
        var providers = new List<Func<ICommandLineOptionsProvider>>();
        Mock<ICommandLineManager> commandLine = new(MockBehavior.Strict);
        commandLine.Setup(x => x.AddProvider(It.IsAny<Func<ICommandLineOptionsProvider>>()))
            .Callback<Func<ICommandLineOptionsProvider>>(providers.Add);
        Mock<ITestApplicationBuilder> builder = new(MockBehavior.Strict);
        builder.SetupGet(x => x.CommandLine).Returns(commandLine.Object);
        string[] arguments = ["other", "123", "last"];
        arguments[position] = "--internal-testhostcontroller-pid";

        TestingPlatformBuilderHook.AddExtensions(builder.Object, arguments);

        Assert.IsInstanceOfType<MSBuildCommandLineProvider>(Assert.ContainsSingle(providers)());
        builder.VerifyGet(x => x.TestHost, Times.Never);
        builder.VerifyGet(x => x.TestHostOrchestrator, Times.Never);
        commandLine.VerifyAll();
    }

    [TestMethod]
    [DataRow("--INTERNAL-TESTHOSTCONTROLLER-PID")]
    [DataRow("--internal-testhostcontroller-pid=123")]
    [DataRow("prefix--internal-testhostcontroller-pid")]
    public void AddExtensions_ControllerOptionLookalikes_StillRegisterFullMSBuildIntegration(string argument)
    {
        RegistrationRecorder registrations = new();

        TestingPlatformBuilderHook.AddExtensions(registrations.Builder.Object, [argument]);

        AssertFullRegistration(registrations);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task AddMSBuild_RegistersFactoriesWithSharedConsumerAndWorkingLifecycleDependencies(bool useHook)
    {
        RegistrationRecorder registrations = new();
        if (useHook)
        {
            TestingPlatformBuilderHook.AddExtensions(registrations.Builder.Object, []);
        }
        else
        {
            registrations.Builder.Object.AddMSBuild();
        }

        AssertFullRegistration(registrations);
        Mock<ICommandLineOptions> options = new();
        options.Setup(x => x.IsOptionSet("internal-msbuild-node")).Returns(true);
        Mock<IServiceProvider> services = new(MockBehavior.Strict);
        services.Setup(x => x.GetService(typeof(IConfiguration))).Returns(Mock.Of<IConfiguration>());
        services.Setup(x => x.GetService(typeof(ICommandLineOptions))).Returns(options.Object);

        using MSBuildTestApplicationLifecycleCallbacks hostLifetime = Assert.IsInstanceOfType<MSBuildTestApplicationLifecycleCallbacks>(
            Assert.ContainsSingle(registrations.HostLifetimes)(services.Object));
        MSBuildOrchestratorLifetime orchestratorLifetime = Assert.IsInstanceOfType<MSBuildOrchestratorLifetime>(
            Assert.ContainsSingle(registrations.OrchestratorLifetimes)(services.Object));
        MSBuildConsumer consumer = GetConsumer(Assert.ContainsSingle(registrations.DataConsumers), services.Object);

        Assert.IsTrue(await hostLifetime.IsEnabledAsync());
        Assert.IsTrue(await orchestratorLifetime.IsEnabledAsync());
        Assert.IsTrue(await consumer.IsEnabledAsync());
        Assert.AreSame(consumer, GetConsumer(Assert.ContainsSingle(registrations.SessionLifetimes), services.Object));
        Assert.IsNull(hostLifetime.PipeClient);
        services.VerifyAll();
    }

    private static void AssertFullRegistration(RegistrationRecorder registrations)
    {
        Assert.IsInstanceOfType<MSBuildCommandLineProvider>(Assert.ContainsSingle(registrations.Providers)());
        Assert.HasCount(1, registrations.HostLifetimes);
        Assert.HasCount(1, registrations.OrchestratorLifetimes);
        Assert.AreSame(Assert.ContainsSingle(registrations.DataConsumers), Assert.ContainsSingle(registrations.SessionLifetimes));
        registrations.CommandLine.VerifyAll();
        registrations.TestHost.VerifyAll();
        registrations.Orchestrator.VerifyAll();
    }

    private static MSBuildConsumer GetConsumer(CompositeExtensionFactory<MSBuildConsumer> factory, IServiceProvider services)
    {
        // Instance creation belongs to an internal platform interface, not the public factory API.
        MethodInfo getInstance = factory.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name.EndsWith(".GetInstance", StringComparison.Ordinal));
        return (MSBuildConsumer)getInstance.Invoke(factory, [services])!;
    }

    private sealed class RegistrationRecorder
    {
        public RegistrationRecorder()
        {
            Builder.SetupGet(x => x.CommandLine).Returns(CommandLine.Object);
            Builder.SetupGet(x => x.TestHost).Returns(TestHost.Object);
            Builder.SetupGet(x => x.TestHostOrchestrator).Returns(Orchestrator.Object);
            CommandLine.Setup(x => x.AddProvider(It.IsAny<Func<ICommandLineOptionsProvider>>()))
                .Callback<Func<ICommandLineOptionsProvider>>(Providers.Add);
            TestHost.Setup(x => x.AddTestHostApplicationLifetime(It.IsAny<Func<IServiceProvider, ITestHostApplicationLifetime>>()))
                .Callback<Func<IServiceProvider, ITestHostApplicationLifetime>>(HostLifetimes.Add);
            Orchestrator.Setup(x => x.AddTestHostOrchestratorApplicationLifetime(It.IsAny<Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime>>()))
                .Callback<Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime>>(OrchestratorLifetimes.Add);
            TestHost.Setup(x => x.AddDataConsumer(It.IsAny<CompositeExtensionFactory<MSBuildConsumer>>()))
                .Callback<CompositeExtensionFactory<MSBuildConsumer>>(DataConsumers.Add);
            TestHost.Setup(x => x.AddTestSessionLifetimeHandler(It.IsAny<CompositeExtensionFactory<MSBuildConsumer>>()))
                .Callback<CompositeExtensionFactory<MSBuildConsumer>>(SessionLifetimes.Add);
        }

        public Mock<ITestApplicationBuilder> Builder { get; } = new(MockBehavior.Strict);

        public Mock<ICommandLineManager> CommandLine { get; } = new(MockBehavior.Strict);

        public Mock<ITestHostManager> TestHost { get; } = new(MockBehavior.Strict);

        public Mock<ITestHostOrchestratorManager> Orchestrator { get; } = new(MockBehavior.Strict);

        public List<Func<ICommandLineOptionsProvider>> Providers { get; } = [];

        public List<Func<IServiceProvider, ITestHostApplicationLifetime>> HostLifetimes { get; } = [];

        public List<Func<IServiceProvider, ITestHostOrchestratorApplicationLifetime>> OrchestratorLifetimes { get; } = [];

        public List<CompositeExtensionFactory<MSBuildConsumer>> DataConsumers { get; } = [];

        public List<CompositeExtensionFactory<MSBuildConsumer>> SessionLifetimes { get; } = [];
    }
}
