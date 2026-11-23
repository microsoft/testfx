// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ServiceProviderCoverageProducerTests
{
    [TestMethod]
    public void TryAddService_CoverageProducerAddedAfterTheCapabilities_IsRegisteredAsEnabledProducer()
    {
        var provider = new ServiceProvider();
        var capabilities = new TestCoverageCapabilities();
        provider.AddService(capabilities);

        bool added = provider.TryAddService(CreateProducer("coverage-producer", typeof(TestCoverageMessage)).Object);

        Assert.IsTrue(added);
        Assert.AreSequenceEqual(["coverage-producer"], capabilities.EnabledProducerUids.ToArray());
    }

    [TestMethod]
    public void TryAddService_CapabilitiesAddedAfterAProducer_RegistersTheExistingProducer()
    {
        var provider = new ServiceProvider();
        provider.AddService(CreateProducer("early-producer", typeof(TestCoverageReportMessage)).Object);
        var capabilities = new TestCoverageCapabilities();

        Assert.IsTrue(provider.TryAddService(capabilities));

        Assert.AreSequenceEqual(["early-producer"], capabilities.EnabledProducerUids.ToArray());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task Authorization_NullIdentityIsReportedAsNullPlaceholderInTheRejectionMessage()
    {
        var provider = new ServiceProvider();
        Mock<ITestHostLauncher> launcher = new();
        launcher.SetupGet(l => l.Uid).Returns("launcher");
        launcher.SetupGet(l => l.DisplayName).Returns("Launcher display");
        launcher.As<ITestHostControllerConnectionAuthorizer>()
            .Setup(a => a.GetAuthorizedSecurityIdentitiesAsync("host.exe", CancellationToken.None))
            .ReturnsAsync(new string[] { null! });
        Mock<ILogger> logger = new();

        InvalidOperationException failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            provider.ResolveTestHostControllerAuthorizedSecurityIdentitiesAsync(launcher.Object, "host.exe", logger.Object, CancellationToken.None));

        Assert.Contains("security identity '<null>'", failure.Message);
    }

    private static Mock<IDataProducer> CreateProducer(string uid, params Type[] dataTypesProduced)
    {
        Mock<IDataProducer> producer = new();
        producer.SetupGet(p => p.Uid).Returns(uid);
        producer.SetupGet(p => p.DataTypesProduced).Returns(dataTypesProduced);
        return producer;
    }
}
