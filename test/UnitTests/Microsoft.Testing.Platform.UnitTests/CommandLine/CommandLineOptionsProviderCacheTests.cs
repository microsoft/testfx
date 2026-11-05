// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CommandLineOptionsProviderCacheTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Cache_PreservesProviderIdentityAndDelegatesValidationWithoutCachingResults(bool tool)
    {
        CommandLineOption option = new("select", "Select an artifact", ArgumentArity.ExactlyOne, false);
        CommandLineOption[] definitions = [option];
        string[] arguments = ["artifact-a"];
        Mock<ICommandLineOptions> options = new(MockBehavior.Strict);
        Task<ValidationResult> argumentResult = ValidationResult.InvalidTask("bad artifact");
        Task<ValidationResult> aggregateResult = ValidationResult.InvalidTask("incompatible selection");
        Mock<IToolCommandLineOptionsProvider> provider = new(MockBehavior.Strict);
        provider.SetupGet(p => p.Uid).Returns("provider-uid");
        provider.SetupGet(p => p.Version).Returns("3.2.1");
        provider.SetupGet(p => p.DisplayName).Returns("Artifact selector");
        provider.SetupGet(p => p.Description).Returns("Selects post-processing inputs");
        provider.SetupGet(p => p.ToolName).Returns("artifact-tool");
        provider.Setup(p => p.GetCommandLineOptions()).Returns(definitions);
        provider.Setup(p => p.IsEnabledAsync()).ReturnsAsync(false);
        provider.Setup(p => p.ValidateOptionArgumentsAsync(option, arguments)).Returns(argumentResult);
        provider.Setup(p => p.ValidateCommandLineOptionsAsync(options.Object)).Returns(aggregateResult);
        ICommandLineOptionsProvider cache = tool
            ? new ToolCommandLineOptionsProviderCache(provider.Object)
            : new CommandLineOptionsProviderCache(provider.Object);

        Assert.AreEqual("provider-uid", cache.Uid);
        Assert.AreEqual("3.2.1", cache.Version);
        Assert.AreEqual("Artifact selector", cache.DisplayName);
        Assert.AreEqual("Selects post-processing inputs", cache.Description);
        if (tool)
        {
            Assert.AreEqual("artifact-tool", ((IToolCommandLineOptionsProvider)cache).ToolName);
        }

        Assert.AreSame(definitions, cache.GetCommandLineOptions());
        Assert.AreSame(definitions, cache.GetCommandLineOptions());
        Assert.IsFalse(await cache.IsEnabledAsync());
        Assert.AreSame(argumentResult, cache.ValidateOptionArgumentsAsync(option, arguments));
        Assert.AreSame(aggregateResult, cache.ValidateCommandLineOptionsAsync(options.Object));
        Assert.AreEqual("bad artifact", (await argumentResult).ErrorMessage);
        Assert.AreEqual("incompatible selection", (await aggregateResult).ErrorMessage);
        provider.Verify(p => p.GetCommandLineOptions(), Times.Once);
        provider.Verify(p => p.IsEnabledAsync(), Times.Once);
        provider.Verify(p => p.ValidateOptionArgumentsAsync(option, arguments), Times.Once);
        provider.Verify(p => p.ValidateCommandLineOptionsAsync(options.Object), Times.Once);
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(42, true)]
    public async Task InformativeHost_ReturnsExitCodeAndDisposesOnlyItsProtocol(int exitCode, bool withProtocol)
    {
        Mock<IPushOnlyProtocol> protocol = new(MockBehavior.Strict);
        protocol.Setup(p => p.Dispose());
        ServiceProvider services = new();
        if (withProtocol)
        {
            services.AddService(protocol.Object);
        }

        InformativeCommandLineHost host = new(exitCode, services);
        Assert.AreEqual(exitCode, await host.RunAsync());
        protocol.Verify(p => p.Dispose(), Times.Never);
        host.Dispose();
        protocol.Verify(p => p.Dispose(), withProtocol ? Times.Once() : Times.Never());
    }
}
