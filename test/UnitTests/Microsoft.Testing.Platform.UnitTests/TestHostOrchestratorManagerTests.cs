// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostOrchestrator;

using BackCompat = Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using PublicApi = Microsoft.Testing.Platform.TestHostOrchestrator;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestHostOrchestratorManagerTests
{
    private static readonly TimeSpan CompletionLimit = TimeSpan.FromSeconds(30);
    private readonly ServiceProvider _serviceProvider = new();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AddTestHostOrchestrator_ViaPublicInterface_RegistersAndBuildsOrchestrator()
    {
        PublicApi.TestHostOrchestratorManager manager = new();

        PublicApi.ITestHostOrchestratorManager publicManager = manager;
        publicManager.AddTestHostOrchestrator(_ => new FakeOrchestrator("orch1"));

        PublicApi.TestHostOrchestratorConfiguration config = await manager.BuildAsync(_serviceProvider);

        Assert.HasCount(1, config.TestHostOrchestrators);
        Assert.AreEqual("orch1", config.TestHostOrchestrators[0].Uid);
    }

    [TestMethod]
    public async Task AddTestHostOrchestrator_ViaBackCompatInterface_RegistersAndBuildsOrchestrator()
    {
        PublicApi.TestHostOrchestratorManager manager = new();

        // Simulate what old extensions compiled against the old namespace do:
        // they cast to the back-compat ITestHostOrchestratorManager interface.
        BackCompat.ITestHostOrchestratorManager backCompatManager = manager;
        backCompatManager.AddTestHostOrchestrator(_ => new FakeOrchestrator("orch-backcompat"));

        PublicApi.TestHostOrchestratorConfiguration config = await manager.BuildAsync(_serviceProvider);

        Assert.HasCount(1, config.TestHostOrchestrators);
        Assert.AreEqual("orch-backcompat", config.TestHostOrchestrators[0].Uid);
    }

    [TestMethod]
    public async Task AddTestHostOrchestrator_BothPublicAndBackCompat_RegistersBoth()
    {
        PublicApi.TestHostOrchestratorManager manager = new();

        PublicApi.ITestHostOrchestratorManager publicManager = manager;
        publicManager.AddTestHostOrchestrator(_ => new FakeOrchestrator("public"));

        BackCompat.ITestHostOrchestratorManager backCompatManager = manager;
        backCompatManager.AddTestHostOrchestrator(_ => new FakeOrchestrator("backcompat"));

        PublicApi.TestHostOrchestratorConfiguration config = await manager.BuildAsync(_serviceProvider);

        Assert.HasCount(2, config.TestHostOrchestrators);
    }

    [TestMethod]
    public async Task AddTestHostOrchestratorApplicationLifetime_RegistersAndBuilds()
    {
        PublicApi.TestHostOrchestratorManager manager = new();

        PublicApi.ITestHostOrchestratorManager publicManager = manager;
        publicManager.AddTestHostOrchestratorApplicationLifetime(_ => new FakeOrchestratorLifetime("lifetime1"));

        BackCompat.ITestHostOrchestratorApplicationLifetime[] lifetimes = await manager.BuildTestHostOrchestratorApplicationLifetimesAsync(_serviceProvider);

        Assert.HasCount(1, lifetimes);
        Assert.AreEqual("lifetime1", lifetimes[0].Uid);
    }

    [TestMethod]
    public async Task BuildAsync_NoOrchestrators_ReturnsEmptyConfiguration()
    {
        PublicApi.TestHostOrchestratorManager manager = new();

        PublicApi.TestHostOrchestratorConfiguration config = await manager.BuildAsync(_serviceProvider);

        Assert.IsEmpty(config.TestHostOrchestrators);
    }

    [TestMethod]
    public async Task AddTestHostOrchestrator_DuplicatedId_ShouldFail()
    {
        PublicApi.TestHostOrchestratorManager manager = new();

        PublicApi.ITestHostOrchestratorManager publicManager = manager;
        publicManager.AddTestHostOrchestrator(_ => new FakeOrchestrator("duplicatedId"));
        publicManager.AddTestHostOrchestrator(_ => new FakeOrchestrator("duplicatedId"));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.BuildAsync(_serviceProvider));

        Assert.IsTrue(exception.Message.Contains("duplicatedId") && exception.Message.Contains(typeof(FakeOrchestrator).ToString()));
    }

    [TestMethod]
    public async Task AddTestHostOrchestratorApplicationLifetime_DuplicatedId_ShouldFail()
    {
        PublicApi.TestHostOrchestratorManager manager = new();

        PublicApi.ITestHostOrchestratorManager publicManager = manager;
        publicManager.AddTestHostOrchestratorApplicationLifetime(_ => new FakeOrchestratorLifetime("duplicatedId"));
        publicManager.AddTestHostOrchestratorApplicationLifetime(_ => new FakeOrchestratorLifetime("duplicatedId"));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.BuildTestHostOrchestratorApplicationLifetimesAsync(_serviceProvider));

        Assert.IsTrue(exception.Message.Contains("duplicatedId") && exception.Message.Contains(typeof(FakeOrchestratorLifetime).ToString()));
    }

    [TestMethod]
    public void BackCompatTestHostOrchestratorManager_IsAssignableToPublicManager()
    {
        // The back-compat subclass should be a valid TestHostOrchestratorManager
        BackCompat.TestHostOrchestratorManager backCompatManager = new();
        Assert.IsInstanceOfType<PublicApi.TestHostOrchestratorManager>(backCompatManager);
    }

    [TestMethod]
    public void TestHostBuilder_TestHostOrchestrator_ImplementsBackCompatInterface()
    {
        // TestHostBuilder creates a BackCompat.TestHostOrchestratorManager
        // which must implement both the public and back-compat interfaces.
        var manager = new BackCompat.TestHostOrchestratorManager();

        Assert.IsInstanceOfType<PublicApi.ITestHostOrchestratorManager>(manager);
        Assert.IsInstanceOfType<BackCompat.ITestHostOrchestratorManager>(manager);
    }

    [TestMethod]
    public async Task BackCompatTestHostOrchestratorManager_AddViaBackCompat_BuildsViaBase()
    {
        // End-to-end test: use the back-compat subclass and register through the back-compat interface,
        // then build through the base class method.
        var backCompatManager = new BackCompat.TestHostOrchestratorManager();

        BackCompat.ITestHostOrchestratorManager backCompatInterface = backCompatManager;
        backCompatInterface.AddTestHostOrchestrator(_ => new FakeOrchestrator("e2e-compat"));

        PublicApi.TestHostOrchestratorConfiguration config = await backCompatManager.BuildAsync(_serviceProvider);

        Assert.HasCount(1, config.TestHostOrchestrators);
        Assert.AreEqual("e2e-compat", config.TestHostOrchestrators[0].Uid);
    }

    [TestMethod]
    public async Task AddTestHostExecutionOrchestratorMiddleware_RegistersAndBuildsInRegistrationOrder()
    {
        PublicApi.TestHostOrchestratorManager manager = new();

        PublicApi.ITestHostOrchestratorManager publicManager = manager;
        publicManager.AddTestHostExecutionOrchestratorMiddleware(_ => new FakeMiddleware("outer"));
        publicManager.AddTestHostExecutionOrchestratorMiddleware(_ => new FakeMiddleware("inner"));

        PublicApi.TestHostOrchestratorConfiguration config = await manager.BuildAsync(_serviceProvider);

        Assert.HasCount(2, config.Middleware);
        Assert.AreEqual("outer", config.Middleware[0].Uid);
        Assert.AreEqual("inner", config.Middleware[1].Uid);
    }

    [TestMethod]
    public async Task BuildAsync_NoMiddleware_ReturnsEmptyMiddlewareArray()
    {
        PublicApi.TestHostOrchestratorManager manager = new();

        PublicApi.TestHostOrchestratorConfiguration config = await manager.BuildAsync(_serviceProvider);

        Assert.IsEmpty(config.Middleware);
    }

    [TestMethod]
    public async Task AddTestHostExecutionOrchestratorMiddleware_DuplicatedId_ShouldFail()
    {
        PublicApi.TestHostOrchestratorManager manager = new();

        PublicApi.ITestHostOrchestratorManager publicManager = manager;
        publicManager.AddTestHostExecutionOrchestratorMiddleware(_ => new FakeMiddleware("duplicatedId"));
        publicManager.AddTestHostExecutionOrchestratorMiddleware(_ => new FakeMiddleware("duplicatedId"));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.BuildAsync(_serviceProvider));

        Assert.Contains("duplicatedId", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(FakeMiddleware).ToString(), exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task AddTestHostExecutionOrchestratorMiddleware_DisabledMiddleware_IsNotIncludedInConfiguration()
    {
        PublicApi.TestHostOrchestratorManager manager = new();

        PublicApi.ITestHostOrchestratorManager publicManager = manager;
        publicManager.AddTestHostExecutionOrchestratorMiddleware(_ => new FakeMiddleware("disabled", enabled: false));
        publicManager.AddTestHostExecutionOrchestratorMiddleware(_ => new FakeMiddleware("enabled"));

        PublicApi.TestHostOrchestratorConfiguration config = await manager.BuildAsync(_serviceProvider);

        Assert.HasCount(1, config.Middleware);
        Assert.AreEqual("enabled", config.Middleware[0].Uid);
    }

    [TestMethod]
    public async Task AddTestHostExecutionOrchestratorMiddleware_ViaBackCompatConcreteManager_RegistersAndBuilds()
    {
        // TestHostBuilder constructs the back-compat subclass at runtime; it must inherit the middleware
        // capability from the base class unchanged.
        var backCompatManager = new BackCompat.TestHostOrchestratorManager();

        PublicApi.ITestHostOrchestratorManager publicManager = backCompatManager;
        publicManager.AddTestHostExecutionOrchestratorMiddleware(_ => new FakeMiddleware("via-backcompat-concrete-type"));

        PublicApi.TestHostOrchestratorConfiguration config = await backCompatManager.BuildAsync(_serviceProvider);

        Assert.HasCount(1, config.Middleware);
        Assert.AreEqual("via-backcompat-concrete-type", config.Middleware[0].Uid);
    }

    [TestMethod]
    public async Task AddTestHostExecutionOrchestratorMiddleware_ExtensionMethod_SupportedManager_Registers()
    {
        PublicApi.TestHostOrchestratorManager manager = new();

        PublicApi.ITestHostOrchestratorManager publicManager = manager;
        publicManager.AddTestHostExecutionOrchestratorMiddleware(_ => new FakeMiddleware("via-extension"));

        PublicApi.TestHostOrchestratorConfiguration config = await manager.BuildAsync(_serviceProvider);

        Assert.HasCount(1, config.Middleware);
        Assert.AreEqual("via-extension", config.Middleware[0].Uid);
    }

    [TestMethod]
    public void AddTestHostExecutionOrchestratorMiddleware_ExtensionMethod_UnsupportedManager_ThrowsNotSupportedException()
    {
        PublicApi.ITestHostOrchestratorManager unsupportedManager = new UnsupportedOrchestratorManager();

        NotSupportedException exception = Assert.ThrowsExactly<NotSupportedException>(
            () => unsupportedManager.AddTestHostExecutionOrchestratorMiddleware(_ => new FakeMiddleware("never-registered")));

        Assert.Contains(nameof(UnsupportedOrchestratorManager), exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task BackCompatConcreteOverload_ForwardsFactoryAndProvider()
    {
        var manager = new BackCompat.TestHostOrchestratorManager();
        var orchestrator = new FakeOrchestrator("concrete-compat");
        IServiceProvider? observedProvider = null;
        Func<IServiceProvider, BackCompat.ITestHostOrchestrator> factory = provider =>
        {
            observedProvider = provider;
            return orchestrator;
        };

        manager.AddTestHostOrchestrator(factory);
        PublicApi.TestHostOrchestratorConfiguration config = await manager.BuildAsync(_serviceProvider);

        Assert.AreSame(_serviceProvider, observedProvider);
        Assert.AreSequenceEqual(new[] { orchestrator }, config.TestHostOrchestrators);
        Assert.IsEmpty(config.Middleware);
    }

    [TestMethod]
    [DataRow("public")]
    [DataRow("legacy-interface")]
    [DataRow("legacy-concrete")]
    public void AddTestHostOrchestrator_NullFactory_ThrowsWithParameterName(string registration)
    {
        var manager = new BackCompat.TestHostOrchestratorManager();

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() =>
        {
            switch (registration)
            {
                case "public":
                    ((PublicApi.ITestHostOrchestratorManager)manager).AddTestHostOrchestrator(null!);
                    break;
                case "legacy-interface":
                    ((BackCompat.ITestHostOrchestratorManager)manager).AddTestHostOrchestrator(null!);
                    break;
                default:
                    manager.AddTestHostOrchestrator(null!);
                    break;
            }
        });

        Assert.AreEqual("factory", exception.ParamName);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AddTestHostOrchestratorApplicationLifetime_NullFactory_ThrowsWithParameterName(bool legacy)
    {
        var manager = new PublicApi.TestHostOrchestratorManager();

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() =>
        {
            if (legacy)
            {
                ((BackCompat.ITestHostOrchestratorManager)manager).AddTestHostOrchestratorApplicationLifetime(null!);
            }
            else
            {
                manager.AddTestHostOrchestratorApplicationLifetime(null!);
            }
        });

        Assert.AreEqual("testHostOrchestratorApplicationLifetimeFactory", exception.ParamName);
    }

    [TestMethod]
    public void AddTestHostExecutionOrchestratorMiddleware_NullFactory_ThrowsWithParameterName()
    {
        var manager = new PublicApi.TestHostOrchestratorManager();

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => manager.AddTestHostExecutionOrchestratorMiddleware(null!));

        Assert.AreEqual("factory", exception.ParamName);
    }

    [TestMethod]
    public async Task BackCompatLifetimeRegistration_ForwardsFactoryAndProvider()
    {
        var manager = new PublicApi.TestHostOrchestratorManager();
        var lifetime = new FakeOrchestratorLifetime("legacy-lifetime");
        IServiceProvider? observedProvider = null;
        ((BackCompat.ITestHostOrchestratorManager)manager).AddTestHostOrchestratorApplicationLifetime(provider =>
        {
            observedProvider = provider;
            return lifetime;
        });

        BackCompat.ITestHostOrchestratorApplicationLifetime[] result =
            await manager.BuildTestHostOrchestratorApplicationLifetimesAsync(_serviceProvider);

        Assert.AreSame(_serviceProvider, observedProvider);
        Assert.AreSequenceEqual(new[] { lifetime }, result);
    }

    [TestMethod]
    public void Configuration_CompatibilityConstructor_PreservesOrderedOrchestratorsWithoutMiddleware()
    {
        BackCompat.ITestHostExecutionOrchestrator[] orchestrators =
            [new FakeOrchestrator("first"), new FakeOrchestrator("second")];

        PublicApi.TestHostOrchestratorConfiguration config = new(orchestrators);

        Assert.AreSame(orchestrators, config.TestHostOrchestrators);
        Assert.AreSequenceEqual(new[] { "first", "second" }, config.TestHostOrchestrators.Select(x => x.Uid));
        Assert.IsEmpty(config.Middleware);
    }

    [TestMethod]
    public void Configuration_WithMiddleware_PreservesBothOrderedArrays()
    {
        BackCompat.ITestHostExecutionOrchestrator[] orchestrators = [new FakeOrchestrator("orchestrator")];
        BackCompat.ITestHostExecutionOrchestratorMiddleware[] middleware = [new FakeMiddleware("outer"), new FakeMiddleware("inner")];

        PublicApi.TestHostOrchestratorConfiguration config = new(orchestrators, middleware);

        Assert.AreSame(orchestrators, config.TestHostOrchestrators);
        Assert.AreSame(middleware, config.Middleware);
        Assert.AreSequenceEqual(new[] { "outer", "inner" }, config.Middleware.Select(x => x.Uid));
    }

    [TestMethod]
    [DataRow("orchestrator")]
    [DataRow("middleware")]
    [DataRow("lifetime")]
    public async Task Build_EnabledExtensions_InitializeInOrderAndUseProvidedServices(string kind)
    {
        var manager = new PublicApi.TestHostOrchestratorManager();
        var events = new List<string>();
        var first = new InitializableExtension("first", events);
        var second = new InitializableExtension("second", events);
        Register(kind, manager, provider =>
        {
            Assert.AreSame(_serviceProvider, provider);
            events.Add("factory:first");
            return first;
        });
        Register(kind, manager, provider =>
        {
            Assert.AreSame(_serviceProvider, provider);
            events.Add("factory:second");
            return second;
        });

        IExtension[] result = await BuildAsync(kind, manager);

        Assert.AreSequenceEqual(new IExtension[] { first, second }, result);
        Assert.AreSequenceEqual(
            new[] { "factory:first", "enabled:first", "initialize:first", "factory:second", "enabled:second", "initialize:second" },
            events);
    }

    [TestMethod]
    [DataRow("orchestrator")]
    [DataRow("middleware")]
    [DataRow("lifetime")]
    public async Task Build_DisabledExtensions_AreNotInitializedOrReturned(string kind)
    {
        var manager = new PublicApi.TestHostOrchestratorManager();
        var events = new List<string>();
        var disabled = new InitializableExtension("disabled", events, enabled: false);
        var enabled = new InitializableExtension("enabled", events);
        Register(kind, manager, _ => disabled);
        Register(kind, manager, _ => enabled);

        IExtension[] result = await BuildAsync(kind, manager);

        Assert.AreSequenceEqual(new IExtension[] { enabled }, result);
        Assert.AreSequenceEqual(new[] { "enabled:disabled", "enabled:enabled", "initialize:enabled" }, events);
    }

    [TestMethod]
    [DataRow("orchestrator", "factory")]
    [DataRow("orchestrator", "enabled")]
    [DataRow("orchestrator", "initialize")]
    [DataRow("middleware", "factory")]
    [DataRow("middleware", "enabled")]
    [DataRow("middleware", "initialize")]
    [DataRow("lifetime", "factory")]
    [DataRow("lifetime", "enabled")]
    [DataRow("lifetime", "initialize")]
    public async Task Build_ExtensionFailure_PreservesExceptionAndStopsLaterFactories(string kind, string stage)
    {
        var manager = new PublicApi.TestHostOrchestratorManager();
        var failure = new InvalidOperationException("extension failed");
        var events = new List<string>();
        Register(kind, manager, _ =>
        {
            events.Add("factory:first");
            return stage == "factory"
                ? throw failure
                : new InitializableExtension("first", events, failureStage: stage, failure: failure);
        });
        Register(kind, manager, _ =>
        {
            events.Add("factory:unreachable");
            return new InitializableExtension("unreachable", events);
        });

        InvalidOperationException observed = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => BuildAsync(kind, manager));

        Assert.AreSame(failure, observed);
        string[] expected = stage switch
        {
            "factory" => ["factory:first"],
            "enabled" => ["factory:first", "enabled:first"],
            _ => ["factory:first", "enabled:first", "initialize:first"],
        };
        Assert.AreSequenceEqual(expected, events);
    }

    [TestMethod]
    [DataRow("orchestrator")]
    [DataRow("middleware")]
    [DataRow("lifetime")]
    public async Task Build_RepeatedCalls_RebuildsFreshOrderedResults(string kind)
    {
        var manager = new PublicApi.TestHostOrchestratorManager();
        int generation = 0;
        Register(kind, manager, _ => new InitializableExtension($"first-{++generation}", []));
        Register(kind, manager, _ => new InitializableExtension($"second-{generation}", []));

        IExtension[] first = await BuildAsync(kind, manager);
        IExtension[] second = await BuildAsync(kind, manager);

        Assert.AreSequenceEqual(new[] { "first-1", "second-1" }, first.Select(x => x.Uid));
        Assert.AreSequenceEqual(new[] { "first-2", "second-2" }, second.Select(x => x.Uid));
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    [DataRow("orchestrator", false)]
    [DataRow("orchestrator", true)]
    [DataRow("middleware", false)]
    [DataRow("middleware", true)]
    [DataRow("lifetime", false)]
    [DataRow("lifetime", true)]
    public async Task Build_AsynchronousLifecycle_WaitsBeforeBuildingNextExtension(string kind, bool initialize)
    {
        var manager = new PublicApi.TestHostOrchestratorManager();
        var events = new List<string>();
        TaskCompletionSource<bool> gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new InitializableExtension(
            "first", events,
            enablement: initialize ? null : gate.Task,
            initialization: initialize ? gate.Task : null);
        var second = new InitializableExtension("second", events);
        Register(kind, manager, _ => first);
        Register(kind, manager, _ =>
        {
            events.Add("factory:second");
            return second;
        });
        Task<IExtension[]> build = BuildAsync(kind, manager);
        Exception? assertionFailure = null;

        try
        {
            Assert.IsFalse(build.IsCompleted);
            Assert.AreSequenceEqual(
                initialize ? new[] { "enabled:first", "initialize:first" } : new[] { "enabled:first" },
                events);

            gate.SetResult(true);
            IExtension[] result = await build.WaitAsync(CompletionLimit, TestContext.CancellationToken);

            Assert.AreSequenceEqual(new IExtension[] { first, second }, result);
            Assert.AreSequenceEqual(
                new[] { "enabled:first", "initialize:first", "factory:second", "enabled:second", "initialize:second" },
                events);
        }
        catch (Exception exception)
        {
            assertionFailure = exception;
            throw;
        }
        finally
        {
            gate.TrySetResult(true);
            await JoinForCleanupAsync(build, assertionFailure);
        }
    }

    [TestMethod]
    public async Task Build_IndependentCategories_AllowSameUidAndKeepSeparateResults()
    {
        var manager = new PublicApi.TestHostOrchestratorManager();
        var orchestrator = new FakeOrchestrator("same-uid");
        var middleware = new FakeMiddleware("same-uid");
        var lifetime = new FakeOrchestratorLifetime("same-uid");
        manager.AddTestHostOrchestrator(_ => orchestrator);
        manager.AddTestHostExecutionOrchestratorMiddleware(_ => middleware);
        manager.AddTestHostOrchestratorApplicationLifetime(_ => lifetime);

        PublicApi.TestHostOrchestratorConfiguration config = await manager.BuildAsync(_serviceProvider);
        BackCompat.ITestHostOrchestratorApplicationLifetime[] lifetimes =
            await manager.BuildTestHostOrchestratorApplicationLifetimesAsync(_serviceProvider);

        Assert.AreSequenceEqual(new[] { orchestrator }, config.TestHostOrchestrators);
        Assert.AreSequenceEqual(new[] { middleware }, config.Middleware);
        Assert.AreSequenceEqual(new[] { lifetime }, lifetimes);
    }

    [TestMethod]
    public async Task Build_LifetimesWithoutRegistration_ReturnsEmptyArray()
    {
        var manager = new PublicApi.TestHostOrchestratorManager();

        Assert.IsEmpty(await manager.BuildTestHostOrchestratorApplicationLifetimesAsync(_serviceProvider));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AddMiddlewareExtension_NullManager_IsValidatedBeforeFactory(bool nullFactory)
    {
        Func<IServiceProvider, BackCompat.ITestHostExecutionOrchestratorMiddleware> factory = _ => new FakeMiddleware("unused");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => PublicApi.TestHostOrchestratorManagerExtensions.AddTestHostExecutionOrchestratorMiddleware(
                null!, nullFactory ? null! : factory));

        Assert.AreEqual("manager", exception.ParamName);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AddMiddlewareExtension_NullFactory_IsValidatedBeforeCapability(bool supported)
    {
        PublicApi.ITestHostOrchestratorManager manager = supported
            ? new PublicApi.TestHostOrchestratorManager()
            : new UnsupportedOrchestratorManager();

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => PublicApi.TestHostOrchestratorManagerExtensions.AddTestHostExecutionOrchestratorMiddleware(manager, null!));

        Assert.AreEqual("factory", exception.ParamName);
    }

    [TestMethod]
    public void AddMiddlewareExtension_Capability_ForwardsFactoryUnchangedAndPreservesFailure()
    {
        var failure = new InvalidOperationException("registration failed");
        var manager = new Moq.Mock<PublicApi.ITestHostOrchestratorManager>();
        Func<IServiceProvider, BackCompat.ITestHostExecutionOrchestratorMiddleware> factory = _ => new FakeMiddleware("unused");
        Moq.Mock<PublicApi.ITestHostExecutionOrchestratorMiddlewareManager> capability =
            manager.As<PublicApi.ITestHostExecutionOrchestratorMiddlewareManager>();
        capability.Setup(x => x.AddTestHostExecutionOrchestratorMiddleware(factory)).Throws(failure);

        InvalidOperationException observed = Assert.ThrowsExactly<InvalidOperationException>(
            () => PublicApi.TestHostOrchestratorManagerExtensions.AddTestHostExecutionOrchestratorMiddleware(manager.Object, factory));

        Assert.AreSame(failure, observed);
        capability.Verify(x => x.AddTestHostExecutionOrchestratorMiddleware(factory), Moq.Times.Once);
    }

    private static async Task JoinForCleanupAsync(Task work, Exception? assertionFailure)
    {
        try
        {
            // Joining the original work must not be abandoned because the test token is canceled.
            await work.WaitAsync(CompletionLimit, CancellationToken.None);
        }
        catch (Exception cleanupFailure) when (assertionFailure is not null)
        {
            throw new AggregateException("Test assertions and cleanup both failed.", assertionFailure, cleanupFailure);
        }
    }

    private static void Register(
        string kind,
        PublicApi.TestHostOrchestratorManager manager,
        Func<IServiceProvider, InitializableExtension> factory)
    {
        switch (kind)
        {
            case "orchestrator":
                manager.AddTestHostOrchestrator(factory);
                break;
            case "middleware":
                manager.AddTestHostExecutionOrchestratorMiddleware(factory);
                break;
            default:
                manager.AddTestHostOrchestratorApplicationLifetime(factory);
                break;
        }
    }

    private async Task<IExtension[]> BuildAsync(string kind, PublicApi.TestHostOrchestratorManager manager)
    {
        if (kind == "lifetime")
        {
            return await manager.BuildTestHostOrchestratorApplicationLifetimesAsync(_serviceProvider);
        }

        PublicApi.TestHostOrchestratorConfiguration config = await manager.BuildAsync(_serviceProvider);
        return kind == "orchestrator" ? config.TestHostOrchestrators : config.Middleware;
    }

    private sealed class InitializableExtension(
        string uid,
        List<string> events,
        bool enabled = true,
        string? failureStage = null,
        Exception? failure = null,
        Task<bool>? enablement = null,
        Task? initialization = null) :
        BackCompat.ITestHostOrchestrator,
        BackCompat.ITestHostOrchestratorApplicationLifetime,
        BackCompat.ITestHostExecutionOrchestratorMiddleware,
        IAsyncInitializableExtension
    {
        public string Uid => uid;

        public string Version => PlatformVersion.Version;

        public string DisplayName => uid;

        public string Description => uid;

        public Task<bool> IsEnabledAsync()
        {
            events.Add($"enabled:{uid}");
            return failureStage == "enabled" ? Task.FromException<bool>(failure!) : enablement ?? Task.FromResult(enabled);
        }

        public Task InitializeAsync()
        {
            events.Add($"initialize:{uid}");
            return failureStage == "initialize" ? Task.FromException(failure!) : initialization ?? Task.CompletedTask;
        }

        public Task<int> OrchestrateTestHostExecutionAsync(CancellationToken cancellationToken) => Task.FromResult(7);

        public Task<int> OrchestrateTestHostExecutionAsync(Func<CancellationToken, Task<int>> next, CancellationToken cancellationToken)
            => next(cancellationToken);

        public Task BeforeRunAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AfterRunAsync(int exitCode, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeOrchestrator : BackCompat.ITestHostOrchestrator
    {
        public FakeOrchestrator(string uid) => Uid = uid;

        public string Uid { get; }

        public string Version => PlatformVersion.Version;

        public string DisplayName => Uid;

        public string Description => Uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<int> OrchestrateTestHostExecutionAsync(CancellationToken cancellationToken)
            => Task.FromResult(0);
    }

    private sealed class FakeOrchestratorLifetime : BackCompat.ITestHostOrchestratorApplicationLifetime
    {
        public FakeOrchestratorLifetime(string uid) => Uid = uid;

        public string Uid { get; }

        public string Version => PlatformVersion.Version;

        public string DisplayName => Uid;

        public string Description => Uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task BeforeRunAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AfterRunAsync(int exitCode, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeMiddleware(string uid, bool enabled = true) : BackCompat.ITestHostExecutionOrchestratorMiddleware
    {
        public string Uid => uid;

        public string Version => PlatformVersion.Version;

        public string DisplayName => uid;

        public string Description => uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(enabled);

        public Task<int> OrchestrateTestHostExecutionAsync(Func<CancellationToken, Task<int>> next, CancellationToken cancellationToken)
            => next(cancellationToken);
    }

    /// <summary>
    /// A manager that only implements the base <see cref="PublicApi.ITestHostOrchestratorManager"/>, not the
    /// optional <see cref="PublicApi.ITestHostExecutionOrchestratorMiddlewareManager"/> capability - the
    /// scenario <see cref="PublicApi.TestHostOrchestratorManagerExtensions.AddTestHostExecutionOrchestratorMiddleware"/>
    /// must reject with a clear, actionable exception rather than an obscure cast failure.
    /// </summary>
    private sealed class UnsupportedOrchestratorManager : PublicApi.ITestHostOrchestratorManager
    {
        public void AddTestHostOrchestrator(Func<IServiceProvider, BackCompat.ITestHostExecutionOrchestrator> factory)
        {
        }

        public void AddTestHostOrchestratorApplicationLifetime(Func<IServiceProvider, BackCompat.ITestHostOrchestratorApplicationLifetime> testHostOrchestratorApplicationLifetimeFactory)
        {
        }
    }
}
