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

    public async Task CreateInstanceChoosesResolvableServiceConstructorOverUnresolvableTestContextConstructor()
    {
        ServiceCollection services = [];
        services.AddScoped<ScopedService>();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());

        ITestClassInstanceLease lease = factory.CreateInstance(typeof(MixedResolvableTestClass), CreateTestContext());

        lease.Instance.Should().BeOfType<MixedResolvableTestClass>()
            .Which.SelectedConstructor.Should().Be("service-only");
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

    public async Task CreateInstanceResolvesKeyedServiceConstructor()
    {
        ServiceCollection services = [];
        services.AddKeyedScoped<ScopedService>("hosted");
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());

        ITestClassInstanceLease lease = factory.CreateInstance(typeof(KeyedServiceTestClass), CreateTestContext());

        lease.Instance.Should().BeOfType<KeyedServiceTestClass>()
            .Which.Service.Should().NotBeNull();
        await lease.DisposeAsync();
    }

    public async Task CreateInstanceWithoutServiceCheckerLetsActivatorUtilitiesResolveConstructor()
    {
        var service = new ScopedService();
        var scopeFactory = new StubScopeFactory(new StubServiceProvider(service));
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(scopeFactory);
        TestContext testContext = CreateTestContext();

        ITestClassInstanceLease lease = factory.CreateInstance(typeof(TestContextAndServiceTestClass), testContext);

        TestContextAndServiceTestClass instance = lease.Instance.Should().BeOfType<TestContextAndServiceTestClass>().Subject;
        instance.TestContext.Should().BeSameAs(testContext);
        instance.Service.Should().BeSameAs(service);
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
            .And.Contain("unique public constructor");
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

    public void CreateInstanceMultiplePreferredConstructorsReportsStableDiagnostic()
    {
        ServiceCollection services = [];
        services.AddSingleton<ServiceA>();
        services.AddSingleton<ServiceB>();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());

        Action action = () => factory.CreateInstance(typeof(MultiplePreferredConstructorsTestClass), CreateTestContext());

        action.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("multiple public constructors")
            .And.Contain(nameof(ActivatorUtilitiesConstructorAttribute));
    }

    public void CreateInstanceDerivedTestContextParameterReportsStableDiagnostic()
    {
        using ServiceProvider serviceProvider = new ServiceCollection().BuildServiceProvider();
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());

        Action action = () => factory.CreateInstance(typeof(DerivedTestContextTestClass), CreateTestContext());

        action.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("derives from TestContext")
            .And.Contain(nameof(DerivedTestContext));
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

    public void CreateInstanceFailureDisposesAsyncScopeOutsideCallerSynchronizationContext()
    {
        var scope = new AsyncOnlyStubScope(new StubServiceProvider(new ScopedService()));
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(new AsyncOnlyStubScopeFactory(scope));
        SynchronizationContext? originalSynchronizationContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

        try
        {
            Action action = () => factory.CreateInstance(typeof(ThrowingServiceConstructorTestClass), CreateTestContext());

            action.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalSynchronizationContext);
        }

        scope.Disposed.Should().BeTrue();
        scope.DisposalSynchronizationContext.Should().BeNull();
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

#if NET6_0_OR_GREATER
        tracker.Events.Should().Equal("test-async", "test-sync", "scope-async");
        tracker.TestAsyncDisposeCount.Should().Be(1);
#else
        tracker.Events.Should().Equal("test-sync", "scope-async");
        tracker.TestAsyncDisposeCount.Should().Be(0);
#endif
        tracker.TestDisposeCount.Should().Be(1);
        tracker.ScopeDisposeCount.Should().Be(1);
    }

    public async Task DisposeAsyncSupportsAsyncOnlyScopedService()
    {
        var tracker = new DisposalTracker();
        ServiceCollection services = [];
        services.AddSingleton(tracker);
        services.AddScoped<AsyncOnlyTrackedScopedService>();
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        var factory = new MicrosoftExtensionsTestClassInstanceFactory(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        ITestClassInstanceLease lease = factory.CreateInstance(typeof(AsyncOnlyScopedServiceTestClass), CreateTestContext());

        await lease.DisposeAsync();

        tracker.Events.Should().Equal("scope-async-only");
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

    private sealed class MixedResolvableTestClass
    {
        public MixedResolvableTestClass(ScopedService service)
        {
            _ = service;
            SelectedConstructor = "service-only";
        }

        public MixedResolvableTestClass(TestContext testContext, ServiceA missingService)
        {
            _ = testContext;
            _ = missingService;
            SelectedConstructor = "test-context";
        }

        public string SelectedConstructor { get; }
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

    private sealed class KeyedServiceTestClass
    {
        public KeyedServiceTestClass([FromKeyedServices("hosted")] ScopedService service) => Service = service;

        public ScopedService Service { get; }
    }

    private sealed class AmbiguousTestClass
    {
        public AmbiguousTestClass(ServiceA service) => _ = service;

        public AmbiguousTestClass(ServiceB service) => _ = service;
    }

    private sealed class MultiplePreferredConstructorsTestClass
    {
        [ActivatorUtilitiesConstructor]
        public MultiplePreferredConstructorsTestClass(ServiceA service) => _ = service;

        [ActivatorUtilitiesConstructor]
        public MultiplePreferredConstructorsTestClass(ServiceB service) => _ = service;
    }

    private sealed class DerivedTestContextTestClass
    {
        public DerivedTestContextTestClass(DerivedTestContext testContext) => _ = testContext;
    }

    private abstract class DerivedTestContext : TestContext
    {
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

    private sealed class ThrowingServiceConstructorTestClass
    {
        public ThrowingServiceConstructorTestClass(ScopedService service)
        {
            _ = service;
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

    private sealed class TrackedScopedService : IAsyncDisposable, IDisposable
    {
        private readonly DisposalTracker _tracker;

        public TrackedScopedService(DisposalTracker tracker) => _tracker = tracker;

        public ValueTask DisposeAsync()
        {
            _tracker.Events.Add("scope-async");
            _tracker.ScopeDisposeCount++;
            return default;
        }

        public void Dispose()
        {
            _tracker.Events.Add("scope-sync");
            _tracker.ScopeDisposeCount++;
        }
    }

    private sealed class AsyncOnlyScopedServiceTestClass
    {
        public AsyncOnlyScopedServiceTestClass(AsyncOnlyTrackedScopedService service) => _ = service;
    }

    private sealed class AsyncOnlyTrackedScopedService(DisposalTracker tracker) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            tracker.Events.Add("scope-async-only");
            tracker.ScopeDisposeCount++;
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

    private sealed class StubScopeFactory(IServiceProvider serviceProvider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new StubScope(serviceProvider);
    }

    private sealed class AsyncOnlyStubScopeFactory(AsyncOnlyStubScope scope) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => scope;
    }

    private sealed class StubScope(IServiceProvider serviceProvider) : IServiceScope
    {
        public IServiceProvider ServiceProvider => serviceProvider;

        public void Dispose()
        {
        }
    }

    private sealed class StubServiceProvider(ScopedService service) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(ScopedService) ? service : null;
    }

    private sealed class AsyncOnlyStubScope(IServiceProvider serviceProvider) : IServiceScope, IAsyncDisposable
    {
        public IServiceProvider ServiceProvider => serviceProvider;

        public bool Disposed { get; private set; }

        public SynchronizationContext? DisposalSynchronizationContext { get; private set; }

        public void Dispose() => throw new InvalidOperationException("Synchronous disposal should not be used.");

        public async ValueTask DisposeAsync()
        {
            DisposalSynchronizationContext = SynchronizationContext.Current;
            await Task.Yield();
            Disposed = true;
        }
    }
}
