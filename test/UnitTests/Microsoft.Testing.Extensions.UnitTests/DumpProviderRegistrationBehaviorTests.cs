// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class DumpProviderRegistrationBehaviorTests
{
    [TestMethod]
    [DataRow(false, DisplayName = "HangDump_DirectRegistration")]
    [DataRow(true, DisplayName = "HangDump_BuilderHook")]
    public async Task HangDump_RegisteredFactoriesMaterializeUsableSharedRoles(bool useHook)
    {
        using var fixture = new DumpBehaviorFixture();
        var registration = new Registration<HangDumpActivityIndicator>();
        if (useHook)
        {
            HangDump.TestingPlatformBuilderHook.AddExtensions(registration.Builder.Object, ["unused-hook-argument"]);
        }
        else
        {
            registration.Builder.Object.AddHangDumpProvider();
        }

        registration.VerifyAllRolesRegisteredOnce();
        ITestHostEnvironmentVariableProvider environment = registration.EnvironmentFactory!(fixture.Services);
        using var lifetime = (HangDumpProcessLifetimeHandler)registration.LifetimeFactory!(fixture.Services);
        Assert.IsFalse(await environment.IsEnabledAsync());
        Assert.IsFalse(await lifetime.IsEnabledAsync());

        fixture.Options[HangDumpCommandLineProvider.HangDumpOptionName] = [];
        Assert.IsTrue(await environment.IsEnabledAsync());
        Assert.IsTrue(await lifetime.IsEnabledAsync());

        var variables = new EnvironmentVariables(fixture.LoggerFactory.Object) { CurrentProvider = environment };
        await environment.UpdateAsync(variables);
        Assert.IsTrue(variables.TryGetVariable(HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName, out OwnedEnvironmentVariable? pipe));
        Assert.AreSame(environment, pipe.Owner);
        Assert.IsNotNull(pipe.Value);
        Assert.IsNotEmpty(pipe.Value);
        Assert.IsTrue(pipe.IsLocked);
        Assert.IsFalse(pipe.IsSecret);
        Assert.IsTrue((await environment.ValidateTestHostEnvironmentVariablesAsync(variables)).IsValid);

        fixture.EnvironmentValues[HangDumpEnvironmentVariableProvider.PipeNameEnvironmentVariableName] = pipe.Value;
        using var activity = (HangDumpActivityIndicator)((ICompositeExtensionFactory)registration.ConsumerFactory!).GetInstance(fixture.Services);
        object sessionHandler = ((ICompositeExtensionFactory)registration.SessionFactory!).GetInstance(fixture.Services);
        Assert.AreSame(activity, sessionHandler, "The session callback must initialize the same consumer that tracks running tests.");
        Assert.IsTrue(await activity.IsEnabledAsync());

        ICommandLineOptionsProvider commandLine = registration.CommandLineFactory!();
        Assert.IsInstanceOfType<HangDumpCommandLineProvider>(commandLine);
        Assert.AreSequenceEqual(
            ["hangdump", "hangdump-timeout", "hangdump-filename", "hangdump-type", "hangdump-type-if-supported"],
            commandLine.GetCommandLineOptions().Select(option => option.Name));
    }

    [TestMethod]
    [DataRow(0, DisplayName = "CrashDump_DefaultRegistration")]
    [DataRow(1, DisplayName = "CrashDump_IgnoreUnsupportedRuntime")]
    [DataRow(2, DisplayName = "CrashDump_BuilderHookIgnoresUnsupportedRuntime")]
    public async Task CrashDump_RegisteredFactoriesShareConfigurationAndHostJournal(int registrationMode)
    {
        using var fixture = new DumpBehaviorFixture();
        fixture.Options[CrashDumpCommandLineOptions.CrashDumpOptionName] = [];
        fixture.Options[CrashDumpCommandLineOptions.CrashDumpFileNameOptionName] = ["host_%p.dmp"];
        var registration = new Registration<CrashDumpSequenceLogger>();
        if (registrationMode == 2)
        {
            CrashDump.TestingPlatformBuilderHook.AddExtensions(registration.Builder.Object, ["unused-hook-argument"]);
        }
        else
        {
            registration.Builder.Object.AddCrashDumpProvider(ignoreIfNotSupported: registrationMode == 1);
        }

        registration.VerifyAllRolesRegisteredOnce();
        ITestHostEnvironmentVariableProvider environment = registration.EnvironmentFactory!(fixture.Services);
        ITestHostProcessLifetimeHandler lifetime = registration.LifetimeFactory!(fixture.Services);
#if NETCOREAPP
        bool expectedEnabled = true;
#else
        bool expectedEnabled = registrationMode == 0;
#endif
        Assert.AreEqual(expectedEnabled, await environment.IsEnabledAsync());
        Assert.AreEqual(expectedEnabled, await lifetime.IsEnabledAsync());

        var variables = new EnvironmentVariables(fixture.LoggerFactory.Object) { CurrentProvider = environment };
        await environment.UpdateAsync(variables);
        Assert.IsTrue(variables.TryGetVariable(CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName, out OwnedEnvironmentVariable? journal));
        Assert.AreSame(environment, journal.Owner);
        Assert.IsTrue(journal.IsLocked);
        Assert.IsFalse(journal.IsSecret);
        Assert.IsNotNull(journal.Value);
        Assert.AreEqual(fixture.RootDirectory, Path.GetDirectoryName(journal.Value));
        fixture.EnvironmentValues[CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName] = journal.Value;

        using var consumer = (CrashDumpSequenceLogger)((ICompositeExtensionFactory)registration.ConsumerFactory!).GetInstance(fixture.Services);
        Assert.AreSame(consumer, ((ICompositeExtensionFactory)registration.SessionFactory!).GetInstance(fixture.Services));
        Assert.IsTrue(await consumer.IsEnabledAsync());
        await consumer.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));
        await consumer.ConsumeAsync(null!, DumpBehaviorFixture.Update("registered-test", "Registered test", InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);
        await consumer.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));
        consumer.Dispose();

        Assert.AreSequenceEqual(
            [DumpBehaviorFixture.JournalHeader, "STARTED\t2026-10-30T12:00:00.0000000+00:00\tregistered-test\tRegistered test"],
            File.ReadAllLines(journal.Value));

        // The environment and lifecycle factories must close over the same configuration instance.
        // Graceful exit only needs that shared sequence path; it never consults ambient hang-dump state.
        await lifetime.OnTestHostProcessExitedAsync(new TestHostProcessInformation(4321, 0, true), CancellationToken.None);
        Assert.IsFalse(File.Exists(journal.Value));
        Assert.IsEmpty(fixture.Publications);
        Assert.IsEmpty(fixture.Displays);

        ICommandLineOptionsProvider commandLine = registration.CommandLineFactory!();
        Assert.IsInstanceOfType<CrashDumpCommandLineProvider>(commandLine);
        Assert.Contains(CrashDumpCommandLineOptions.CrashSequenceOptionName, commandLine.GetCommandLineOptions().Select(option => option.Name));
    }

    private sealed class Registration<TExtension>
        where TExtension : class, IDataConsumer, ITestSessionLifetimeHandler
    {
        private readonly Mock<ITestHostControllersManager> _controllers = new(MockBehavior.Strict);
        private readonly Mock<ITestHostManager> _host = new(MockBehavior.Strict);
        private readonly Mock<ICommandLineManager> _commandLine = new(MockBehavior.Strict);

        public Registration()
        {
            Builder.SetupGet(x => x.TestHostControllers).Returns(_controllers.Object);
            Builder.SetupGet(x => x.TestHost).Returns(_host.Object);
            Builder.SetupGet(x => x.CommandLine).Returns(_commandLine.Object);
            _controllers
                .Setup(x => x.AddEnvironmentVariableProvider(It.IsAny<Func<IServiceProvider, ITestHostEnvironmentVariableProvider>>()))
                .Callback((Func<IServiceProvider, ITestHostEnvironmentVariableProvider> factory) => EnvironmentFactory = factory);
            _controllers
                .Setup(x => x.AddProcessLifetimeHandler(It.IsAny<Func<IServiceProvider, ITestHostProcessLifetimeHandler>>()))
                .Callback((Func<IServiceProvider, ITestHostProcessLifetimeHandler> factory) => LifetimeFactory = factory);
            _commandLine
                .Setup(x => x.AddProvider(It.IsAny<Func<ICommandLineOptionsProvider>>()))
                .Callback((Func<ICommandLineOptionsProvider> factory) => CommandLineFactory = factory);
            _host
                .Setup(x => x.AddDataConsumer(It.IsAny<CompositeExtensionFactory<TExtension>>()))
                .Callback((CompositeExtensionFactory<TExtension> factory) => ConsumerFactory = factory);
            _host
                .Setup(x => x.AddTestSessionLifetimeHandler(It.IsAny<CompositeExtensionFactory<TExtension>>()))
                .Callback((CompositeExtensionFactory<TExtension> factory) => SessionFactory = factory);
        }

        public Mock<ITestApplicationBuilder> Builder { get; } = new(MockBehavior.Strict);

        public Func<IServiceProvider, ITestHostEnvironmentVariableProvider>? EnvironmentFactory { get; private set; }

        public Func<IServiceProvider, ITestHostProcessLifetimeHandler>? LifetimeFactory { get; private set; }

        public Func<ICommandLineOptionsProvider>? CommandLineFactory { get; private set; }

        public CompositeExtensionFactory<TExtension>? ConsumerFactory { get; private set; }

        public CompositeExtensionFactory<TExtension>? SessionFactory { get; private set; }

        public void VerifyAllRolesRegisteredOnce()
        {
            _controllers.Verify(x => x.AddEnvironmentVariableProvider(It.IsAny<Func<IServiceProvider, ITestHostEnvironmentVariableProvider>>()), Times.Once);
            _controllers.Verify(x => x.AddProcessLifetimeHandler(It.IsAny<Func<IServiceProvider, ITestHostProcessLifetimeHandler>>()), Times.Once);
            _commandLine.Verify(x => x.AddProvider(It.IsAny<Func<ICommandLineOptionsProvider>>()), Times.Once);
            _host.Verify(x => x.AddDataConsumer(It.IsAny<CompositeExtensionFactory<TExtension>>()), Times.Once);
            _host.Verify(x => x.AddTestSessionLifetimeHandler(It.IsAny<CompositeExtensionFactory<TExtension>>()), Times.Once);
            Assert.AreSame(ConsumerFactory, SessionFactory);
            _controllers.VerifyNoOtherCalls();
            _commandLine.VerifyNoOtherCalls();
            _host.VerifyNoOtherCalls();
        }
    }
}
