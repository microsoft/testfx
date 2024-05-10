// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.TestInfrastructure;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestGroup]
public class TreeNodeFilterCommandLineOptionsProviderTests : TestBase
{
    public TreeNodeFilterCommandLineOptionsProviderTests(ITestExecutionContext testExecutionContext)
    : base(testExecutionContext)
    {
    }

    private static TreeNodeFilterCommandLineOptionsProvider GetProvider()
    {
        var extension = new Mock<IExtension>();
        _ = extension.Setup(x => x.Uid).Returns("Uid");
        _ = extension.Setup(x => x.Version).Returns("Version");
        _ = extension.Setup(x => x.DisplayName).Returns("DisplayName");
        _ = extension.Setup(x => x.Description).Returns("Description");
        return new TreeNodeFilterCommandLineOptionsProvider(extension.Object);
    }

    public async Task TreenodeFilter_AlwaysValid()
    {
        TreeNodeFilterCommandLineOptionsProvider provider = GetProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, []).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
    }

    public async Task CommandLineOptions_AlwaysValid()
    {
        TreeNodeFilterCommandLineOptionsProvider provider = GetProvider();

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new Mock<ICommandLineOptions>().Object).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
    }
}
