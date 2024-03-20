// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.TestInfrastructure;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class ConfigurationExtensionsTests : TestBase
{
    public ConfigurationExtensionsTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    private string GetActualValueFromConfiguration(IConfiguration configuration, string key) => key switch
    {
        PlatformConfigurationConstants.PlatformResultDirectory => configuration.GetTestResultDirectory(),
        PlatformConfigurationConstants.PlatformCurrentWorkingDirectory => configuration.GetCurrentWorkingDirectory(),
        PlatformConfigurationConstants.PlatformTestHostWorkingDirectory => configuration.GetTestHostWorkingDirectory(),
        _ => throw new ArgumentException("Unsupported key."),
    };

    [Arguments(PlatformConfigurationConstants.PlatformResultDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)]
    public void ConfigurationExtensions_TestedMethod_ReturnsExpectedPath(string key)
    {
        string expectedPath = Path.Combine("a", "b", "c");

        Mock<IConfiguration> configuration = new();
        configuration
            .Setup(configuration => configuration[key])
            .Returns(expectedPath);

        Assert.AreEqual(expectedPath, GetActualValueFromConfiguration(configuration.Object, key));
    }

    [Arguments(PlatformConfigurationConstants.PlatformResultDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformCurrentWorkingDirectory)]
    [Arguments(PlatformConfigurationConstants.PlatformTestHostWorkingDirectory)]
    public void ConfigurationExtensions_TestedMethod_ThrowsArgumentNullException(string key)
    {
        Mock<IConfiguration> configuration = new();
        configuration
            .Setup(configuration => configuration[key])
            .Returns(value: null);

        Assert.Throws<ArgumentNullException>(() => GetActualValueFromConfiguration(configuration.Object, key));
    }
}
