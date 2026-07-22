// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestCoverageCapabilitiesTests
{
    [TestMethod]
    public void RegisterProducer_CoverageDataProducer_ExposesDistinctUid()
    {
        TestCoverageCapabilities capabilities = new();

        capabilities.RegisterProducer(new MockDataProducer("coverage", typeof(TestCoverageMessage)));
        capabilities.RegisterProducer(new MockDataProducer("coverage", typeof(TestCoverageThresholdMessage)));
        capabilities.RegisterProducer(new MockDataProducer("other", typeof(TestNodeUpdateMessage)));

        Assert.IsTrue(capabilities.SupportsTestCoverageMessages);
        Assert.HasCount(1, capabilities.EnabledProducerUids);
        Assert.Contains("coverage", capabilities.EnabledProducerUids);
    }

    [TestMethod]
    public void ServiceProvider_ProducerRegistrationOrder_DoesNotAffectCapabilities()
    {
        var producerBeforeCapabilities = new MockDataProducer("before", typeof(TestCoverageReportMessage));
        var producerAfterCapabilities = new MockDataProducer("after", typeof(TestCoverageMessage));
        ServiceProvider serviceProvider = new();

        serviceProvider.AddService(producerBeforeCapabilities);
        TestCoverageCapabilities capabilities = new();
        serviceProvider.AddService(capabilities);
        serviceProvider.AddService(producerAfterCapabilities);

        Assert.AreSequenceEqual(new[] { "before", "after" }, capabilities.EnabledProducerUids);
        Assert.AreSame(capabilities, serviceProvider.GetRequiredService<ITestCoverageCapabilities>());
    }

    [TestMethod]
    public void RegisterProducers_ControllerDataConsumerProducer_ExposesUid()
    {
        TestCoverageCapabilities capabilities = new();

        capabilities.RegisterProducers(
            [new MockDataConsumerProducer("controller", typeof(TestCoverageThresholdMessage))]);

        Assert.HasCount(1, capabilities.EnabledProducerUids);
        Assert.Contains("controller", capabilities.EnabledProducerUids);
    }

    [TestMethod]
    public void ServiceProviderClone_CreatesPerRequestCapabilitiesSnapshot()
    {
        ServiceProvider serviceProvider = new();
        TestCoverageCapabilities applicationCapabilities = new();
        serviceProvider.AddService(applicationCapabilities);
        serviceProvider.AddService(new MockDataProducer("application", typeof(TestCoverageMessage)));

        var perRequestServiceProvider = (ServiceProvider)serviceProvider.Clone();
        TestCoverageCapabilities perRequestCapabilities = perRequestServiceProvider.GetRequiredService<TestCoverageCapabilities>();
        perRequestServiceProvider.AddService(new MockDataProducer("request", typeof(TestCoverageMessage)));

        Assert.AreNotSame(applicationCapabilities, perRequestCapabilities);
        Assert.AreSequenceEqual(new[] { "application" }, applicationCapabilities.EnabledProducerUids);
        Assert.AreSequenceEqual(new[] { "application", "request" }, perRequestCapabilities.EnabledProducerUids);
    }

    private sealed class MockDataProducer(string uid, params Type[] dataTypesProduced) : IDataProducer
    {
        public Type[] DataTypesProduced { get; } = dataTypesProduced;

        public string Uid { get; } = uid;

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed class MockDataConsumerProducer(string uid, params Type[] dataTypesProduced)
        : IDataConsumer, IDataProducer
    {
        public Type[] DataTypesConsumed => [];

        public Type[] DataTypesProduced { get; } = dataTypesProduced;

        public string Uid { get; } = uid;

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
