// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.UnitTests.Helpers;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestGroup]
public class TreeNodeFilterCommandLineOptionsProviderTests : TestBase
{
    public TreeNodeFilterCommandLineOptionsProviderTests(ITestExecutionContext testExecutionContext)
    : base(testExecutionContext)
    {
    }

    public async Task TreenodeFilter_AlwaysValid()
    {
        var provider = new TreeNodeFilterCommandLineOptionsProvider(new TestExtension());
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, []).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
    }

    public async Task CommandLineOptions_AlwaysValid()
    {
        var provider = new TreeNodeFilterCommandLineOptionsProvider(new TestExtension());

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions([])).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
    }
}
