// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using BackCompat = Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using PublicApi = Microsoft.Testing.Platform.TestHostOrchestrator;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestHostOrchestratorManagerTests
{
    private readonly ServiceProvider _serviceProvider = new();

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

        Assert.HasCount(0, config.TestHostOrchestrators);
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

    private sealed class FakeOrchestrator : BackCompat.ITestHostOrchestrator
    {
        public FakeOrchestrator(string uid) => Uid = uid;

        public string Uid { get; }

        public string Version => AppVersion.DefaultSemVer;

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

        public string Version => AppVersion.DefaultSemVer;

        public string DisplayName => Uid;

        public string Description => Uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task BeforeRunAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AfterRunAsync(int exitCode, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
