// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.UnitTests.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestClass]
public sealed class MaxFailedTestsCommandLineOptionsProviderTests
{
    [TestMethod]
    [DataRow("1")]
    [DataRow("2147483647")]
    [DataRow(" +2 ")]
    public async Task ValidateOptionArgumentsAsync_PositiveIntegerWithCapability_IsValid(string threshold)
    {
        var services = new ServiceProvider();
        services.AddService(new TestFrameworkCapabilities(Mock.Of<IGracefulStopTestExecutionCapability>()));
        var provider = new MaxFailedTestsCommandLineOptionsProvider(new TestExtension(), services);
        CommandLineOption option = provider.GetCommandLineOptions().Single();

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [threshold]);

        Assert.IsTrue(result.IsValid);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("")]
    [DataRow("not-a-number")]
    [DataRow("2147483648")]
    public async Task ValidateOptionArgumentsAsync_InvalidThreshold_ExplainsRejectedValue(string threshold)
    {
        // No capabilities are registered: invalid input must be rejected before
        // looking up the framework's graceful-stop capability.
        var provider = new MaxFailedTestsCommandLineOptionsProvider(new TestExtension(), new ServiceProvider());

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(provider.GetCommandLineOptions().Single(), [threshold]);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(
            string.Format(CultureInfo.InvariantCulture, PlatformResources.GetResourceString("MaxFailedTestsMustBePositive"), threshold),
            result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ValidateOptionArgumentsAsync_WithoutGracefulStopCapability_IsInvalid(bool hasUnrelatedCapability)
    {
        var services = new ServiceProvider();
        ITestFrameworkCapability[] capabilities = hasUnrelatedCapability ? [Mock.Of<ITestFrameworkCapability>()] : [];
        services.AddService(new TestFrameworkCapabilities(capabilities));
        var provider = new MaxFailedTestsCommandLineOptionsProvider(new TestExtension(), services);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(provider.GetCommandLineOptions().Single(), ["1"]);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(PlatformResources.GetResourceString("AbortForMaxFailedTestsCapabilityNotAvailable"), result.ErrorMessage);
    }
}
