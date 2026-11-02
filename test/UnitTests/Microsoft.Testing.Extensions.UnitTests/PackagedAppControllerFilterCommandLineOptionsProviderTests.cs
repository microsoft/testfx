// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK

using Microsoft.Testing.Extensions.PackagedApp;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class PackagedAppControllerFilterCommandLineOptionsProviderTests
{
    [TestMethod]
    public async Task Provider_RegistersOnlyVisibleSingleFilterAndDefersGrammarToHost()
    {
        PackagedAppTestHostLauncher extension = new();
        PackagedAppControllerFilterCommandLineOptionsProvider provider = new(extension);
        CommandLineOption option = Assert.ContainsSingle(provider.GetCommandLineOptions());

        Assert.AreEqual("filter", option.Name);
        Assert.AreEqual(ArgumentArity.ExactlyOne, option.Arity);
        Assert.IsFalse(option.IsHidden);
        Assert.AreEqual(extension.Uid, provider.Uid);
        Assert.AreEqual(extension.DisplayName, provider.DisplayName);
        Assert.IsTrue(await provider.IsEnabledAsync());
        Assert.IsTrue((await provider.ValidateOptionArgumentsAsync(option, ["(invalid"])).IsValid);
    }
}

#endif
