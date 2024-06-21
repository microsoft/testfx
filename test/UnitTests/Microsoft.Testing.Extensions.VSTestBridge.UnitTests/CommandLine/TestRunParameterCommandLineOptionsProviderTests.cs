// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.CommandLine;

[TestGroup]
public sealed class TestRunParameterCommandLineOptionsProviderTests(ITestExecutionContext testExecutionContext)
    : TestBase(testExecutionContext)
{
    public async Task TestRunParameterOption_WhenArgumentDoesNotContainEqual_IsNotValid()
    {
        // Arrange
        var provider = new TestRunParametersCommandLineOptionsProvider(new TestExtension());
        CommandLineOption option = provider.GetCommandLineOptions().Single();

        // Act
        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, new[] { "something" });

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.CurrentCulture, ExtensionResources.TestRunParameterOptionArgumentIsNotParameter, "something"), result.ErrorMessage);
    }

    public async Task TestRunParameterOption_WhenArgumentContainsEqual_IsValid()
    {
        // Arrange
        var provider = new TestRunParametersCommandLineOptionsProvider(new TestExtension());
        CommandLineOption option = provider.GetCommandLineOptions().Single();

        // Act
        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, new[] { "a=b" });

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsNull(result.ErrorMessage);
    }
}
