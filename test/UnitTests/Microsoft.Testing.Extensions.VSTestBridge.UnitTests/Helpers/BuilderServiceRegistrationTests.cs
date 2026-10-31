// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Helpers;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;

[TestClass]
public sealed class BuilderServiceRegistrationTests
{
    [TestMethod]
    public async Task AddCommandLineServices_MaterializesUsableOwnedProviders()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        var owner = new TestExtension();
        builder.AddRunSettingsService(owner);
        builder.AddTestCaseFilterService(owner);
        builder.AddTestRunParametersService(owner);

        CommandLineHandler handler = await ((CommandLineManager)builder.CommandLine).BuildAsync(
            null!, new ServiceProvider(), Mock.Of<IConfiguration>());
        ICommandLineOptionsProvider[] providers = handler.ExtensionsCommandLineOptionsProviders.ToArray();

        Assert.AreSequenceEqual(
            new[] { "manifest", "settings", "filter", "test-parameter" },
            providers.SelectMany(provider => provider.GetCommandLineOptions()).Select(option => option.Name));
        Assert.HasCount(3, providers.Where(provider => provider.Uid == "Uid"));
        foreach (ICommandLineOptionsProvider provider in providers.Where(provider => provider.Uid == "Uid"))
        {
            Assert.AreEqual("Uid", provider.Uid);
            Assert.AreEqual("DisplayName", provider.DisplayName);
            Assert.IsTrue(await provider.IsEnabledAsync());
            CommandLineOption option = provider.GetCommandLineOptions().Single();
            Assert.IsFalse(option.IsHidden);
            Assert.IsTrue((await provider.ValidateCommandLineOptionsAsync(Mock.Of<ICommandLineOptions>())).IsValid);
            if (option.Name is "settings" or "filter")
            {
                Assert.AreEqual(ArgumentArity.ExactlyOne, option.Arity);
            }
        }
    }

    [TestMethod]
    public async Task AddRunSettingsEnvironmentVariableProvider_AppliesConfiguredChildVariables()
    {
        var builder = new Mock<ITestApplicationBuilder>();
        var controllers = new Mock<ITestHostControllersManager>();
        builder.SetupGet(value => value.TestHostControllers).Returns(controllers.Object);
        Func<IServiceProvider, ITestHostEnvironmentVariableProvider>? factory = null;
        controllers.Setup(value => value.AddEnvironmentVariableProvider(
                It.IsAny<Func<IServiceProvider, ITestHostEnvironmentVariableProvider>>()))
            .Callback<Func<IServiceProvider, ITestHostEnvironmentVariableProvider>>(value => factory = value);
        var fixture = new BridgeTestFixture();
        fixture.FileSystem.Setup(value => value.NewFileStream("tests.runsettings", FileMode.Open, FileAccess.Read))
            .Returns(() =>
            {
                var stream = new Mock<IFileStream>();
                stream.SetupGet(value => value.Stream).Returns(new MemoryStream(Encoding.UTF8.GetBytes(
                    "<RunSettings><RunConfiguration><EnvironmentVariables><X>one</X></EnvironmentVariables></RunConfiguration></RunSettings>")));
                return stream.Object;
            });
        var services = new ServiceProvider();
        services.AddServices([fixture.CommandLineOptions.Object, fixture.FileSystem.Object, Mock.Of<IEnvironment>()]);
        var variables = new Mock<IEnvironmentVariables>(MockBehavior.Strict);
        variables.Setup(value => value.SetVariable(It.IsAny<EnvironmentVariable>()));

        builder.Object.AddRunSettingsEnvironmentVariableProvider(new TestExtension());
        Assert.IsNotNull(factory);
        ITestHostEnvironmentVariableProvider provider = factory(services);
        Assert.IsTrue(await provider.IsEnabledAsync());
        await provider.UpdateAsync(variables.Object);
        ValidationResult validation = await provider.ValidateTestHostEnvironmentVariablesAsync(Mock.Of<IReadOnlyEnvironmentVariables>());

        variables.Verify(
            value => value.SetVariable(It.Is<EnvironmentVariable>(variable => variable.Variable == "X"
                && variable.Value == "one" && variable.IsSecret && variable.IsLocked)),
            Times.Once);
        Assert.AreEqual("Uid", provider.Uid);
        Assert.AreEqual("Version", provider.Version);
        Assert.AreEqual("DisplayName", provider.DisplayName);
        Assert.AreEqual("Description", provider.Description);
        Assert.IsTrue(validation.IsValid);
        Assert.IsNull(validation.ErrorMessage);
    }
}
