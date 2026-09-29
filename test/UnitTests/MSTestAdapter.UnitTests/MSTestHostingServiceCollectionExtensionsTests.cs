// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Testing.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestHostingServiceCollectionExtensionsTests : TestContainer
{
    public void AddMSTestTestClassInjectionRegistersOneConfigurator()
    {
        ServiceCollection services = [];

        services.AddMSTestTestClassInjection();
        services.AddMSTestTestClassInjection();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetServices<ITestingPlatformBuilderConfigurator>().Should().ContainSingle();
    }

    public async Task CreateInstanceServiceOnlyConstructorResolvesScopedService()
    {
        ServiceCollection services = [];
        services.AddScoped<ScopedService>();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());

        ITestClassInstanceLease lease = factory.CreateInstance(typeof(ServiceOnlyTestClass), CreateTestContext());

        ServiceOnlyTestClass instance = lease.Instance.Should().BeOfType<ServiceOnlyTestClass>().Subject;
        instance.Service.Should().NotBeNull();
        await lease.DisposeAsync();
    }

    public async Task CreateInstanceTestContextAndServiceConstructorSuppliesBoth()
    {
        ServiceCollection services = [];
        services.AddScoped<ScopedService>();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        TestContext testContext = CreateTestContext();

        ITestClassInstanceLease lease = factory.CreateInstance(typeof(TestContextAndServiceTestClass), testContext);

        TestContextAndServiceTestClass instance = lease.Instance.Should().BeOfType<TestContextAndServiceTestClass>().Subject;
        instance.TestContext.Should().BeSameAs(testContext);
        instance.Service.Should().NotBeNull();
        await lease.DisposeAsync();
    }

    public async Task CreateInstanceActivatorUtilitiesConstructorTakesPrecedenceOverTestContextConstructor()
    {
        ServiceCollection services = [];
        services.AddScoped<ScopedService>();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());

        ITestClassInstanceLease lease = factory.CreateInstance(typeof(PreferredConstructorTestClass), CreateTestContext());

        lease.Instance.Should().BeOfType<PreferredConstructorTestClass>()
            .Which.SelectedConstructor.Should().Be("preferred");
        await lease.DisposeAsync();
    }

    public void CreateInstanceAmbiguousConstructorsReportsStableMSTestDiagnostic()
    {
        ServiceCollection services = [];
        services.AddSingleton<ServiceA>();
        services.AddSingleton<ServiceB>();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());

        Action action = () => factory.CreateInstance(typeof(AmbiguousTestClass), CreateTestContext());

        action.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(typeof(AmbiguousTestClass).FullName)
            .And.Contain("exactly one public constructor");
    }

    public void CreateInstanceMissingServiceReportsStableMSTestDiagnostic()
    {
        using ServiceProvider serviceProvider = new ServiceCollection().BuildServiceProvider();
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());

        Action action = () => factory.CreateInstance(typeof(MissingServiceTestClass), CreateTestContext());

        action.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(typeof(MissingServiceTestClass).FullName)
            .And.Contain("registered services");
    }

    public void CreateInstanceWhenConstructorThrowsDisposesCreatedScope()
    {
        var tracker = new DisposalTracker();
        ServiceCollection services = [];
        services.AddSingleton(tracker);
        services.AddScoped<TrackedScopedService>();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());

        Action action = () => factory.CreateInstance(typeof(ThrowingConstructorTestClass), CreateTestContext());

        action.Should().Throw<InvalidOperationException>();
        tracker.ScopeDisposeCount.Should().Be(1);
    }

    public async Task DisposeAsyncDisposesTestInstanceThenScopeExactlyOnce()
    {
        var tracker = new DisposalTracker();
        ServiceCollection services = [];
        services.AddSingleton(tracker);
        services.AddScoped<TrackedScopedService>();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        ITestClassInstanceLease lease = factory.CreateInstance(typeof(DualDisposableTestClass), CreateTestContext());

        await lease.DisposeAsync();

        tracker.Events.Should().Equal("test-async", "test-sync", "scope-async");
        tracker.TestAsyncDisposeCount.Should().Be(1);
        tracker.TestDisposeCount.Should().Be(1);
        tracker.ScopeDisposeCount.Should().Be(1);
    }

    public void FactoryProviderRestoresPreviousFactory()
    {
        var first = new StubFactory();
        var second = new StubFactory();

        using (TestClassInstanceFactoryProvider.Push(first))
        {
            TestClassInstanceFactoryProvider.Current.Should().BeSameAs(first);
            using (TestClassInstanceFactoryProvider.Push(second))
            {
                TestClassInstanceFactoryProvider.Current.Should().BeSameAs(second);
            }

            TestClassInstanceFactoryProvider.Current.Should().BeSameAs(first);
        }

        TestClassInstanceFactoryProvider.Current.Should().BeNull();
    }

    public async Task FactoryProviderKeepsConcurrentExecutionContextsIsolated()
    {
        var first = new StubFactory();
        var second = new StubFactory();
        using Barrier barrier = new(2);

        var firstTask = Task.Run(() =>
        {
            using (TestClassInstanceFactoryProvider.Push(first))
            {
                barrier.SignalAndWait();
                TestClassInstanceFactoryProvider.Current.Should().BeSameAs(first);
            }
        });
        var secondTask = Task.Run(() =>
        {
            using (TestClassInstanceFactoryProvider.Push(second))
            {
                barrier.SignalAndWait();
                TestClassInstanceFactoryProvider.Current.Should().BeSameAs(second);
            }
        });

        await Task.WhenAll(firstTask, secondTask);
        TestClassInstanceFactoryProvider.Current.Should().BeNull();
    }

    private static TestContext CreateTestContext() => new Mock<TestContext>().Object;

    private sealed class ScopedService;

    private sealed class ServiceA;

    private sealed class ServiceB;

    private sealed class ServiceOnlyTestClass
    {
        public ServiceOnlyTestClass(ScopedService service) => Service = service;

        public ScopedService Service { get; }
    }

    private sealed class TestContextAndServiceTestClass
    {
        public TestContextAndServiceTestClass(TestContext testContext, ScopedService service)
        {
            TestContext = testContext;
            Service = service;
        }

        public TestContext TestContext { get; }

        public ScopedService Service { get; }
    }

    private sealed class PreferredConstructorTestClass
    {
        [ActivatorUtilitiesConstructor]
        public PreferredConstructorTestClass(ScopedService service)
        {
            _ = service;
            SelectedConstructor = "preferred";
        }

        public PreferredConstructorTestClass(TestContext testContext, ScopedService service)
        {
            _ = testContext;
            _ = service;
            SelectedConstructor = "test-context";
        }

        public string SelectedConstructor { get; }
    }

    private sealed class AmbiguousTestClass
    {
        public AmbiguousTestClass(ServiceA service) => _ = service;

        public AmbiguousTestClass(ServiceB service) => _ = service;
    }

    private sealed class MissingServiceTestClass
    {
        public MissingServiceTestClass(ServiceA service) => Service = service;

        public ServiceA Service { get; }
    }

    private sealed class ThrowingConstructorTestClass
    {
        public ThrowingConstructorTestClass(TrackedScopedService service, TestContext testContext)
        {
            _ = service;
            _ = testContext;
            throw new InvalidOperationException("activation failure");
        }
    }

    private sealed class DualDisposableTestClass : IAsyncDisposable, IDisposable
    {
        private readonly TrackedScopedService _service;
        private readonly DisposalTracker _tracker;

        public DualDisposableTestClass(TrackedScopedService service, DisposalTracker tracker)
        {
            _service = service;
            _tracker = tracker;
        }

        public ValueTask DisposeAsync()
        {
            _ = _service;
            _tracker.Events.Add("test-async");
            _tracker.TestAsyncDisposeCount++;
            return default;
        }

        public void Dispose()
        {
            _tracker.Events.Add("test-sync");
            _tracker.TestDisposeCount++;
        }
    }

    private sealed class TrackedScopedService : IAsyncDisposable
    {
        private readonly DisposalTracker _tracker;

        public TrackedScopedService(DisposalTracker tracker) => _tracker = tracker;

        public ValueTask DisposeAsync()
        {
            _tracker.Events.Add("scope-async");
            _tracker.ScopeDisposeCount++;
            return default;
        }
    }

    private sealed class DisposalTracker
    {
        public List<string> Events { get; } = [];

        public int TestAsyncDisposeCount { get; set; }

        public int TestDisposeCount { get; set; }

        public int ScopeDisposeCount { get; set; }
    }

    private sealed class StubFactory : ITestClassInstanceFactory
    {
        public ITestClassInstanceLease CreateInstance(Type testClassType, TestContext testContext)
            => throw new NotSupportedException();
    }
}
