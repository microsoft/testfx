// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.UnitTests.Helpers;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestClass]
public sealed class CommandLineOptionsProxyTests
{
    [TestMethod]
    public void IsOptionSet_BeforeOptionsAreSet_Throws()
    {
        var proxy = new CommandLineOptionsProxy();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => proxy.IsOptionSet("opt"));

        Assert.AreEqual(PlatformResources.GetResourceString("CommandLineOptionsNotReady"), exception.Message);
    }

    [TestMethod]
    public void TryGetOptionArgumentList_BeforeOptionsAreSet_Throws()
    {
        var proxy = new CommandLineOptionsProxy();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => proxy.TryGetOptionArgumentList("opt", out _));

        Assert.AreEqual(PlatformResources.GetResourceString("CommandLineOptionsNotReady"), exception.Message);
    }

    [TestMethod]
    public void TryGetOptionArgumentListOrDefault_BeforeOptionsAreSet_Throws()
    {
        ICommandLineOptionsWithDefaults proxy = new CommandLineOptionsProxy();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => proxy.TryGetOptionArgumentListOrDefault("opt", out _));

        Assert.AreEqual(PlatformResources.GetResourceString("CommandLineOptionsNotReady"), exception.Message);
    }

    [TestMethod]
    public void AfterOptionsAreSet_IsOptionSetAndTryGetOptionArgumentListDelegate()
    {
        var proxy = new CommandLineOptionsProxy();
        proxy.SetCommandLineOptions(new TestCommandLineOptions(new() { ["opt"] = ["a", "b"] }));

        Assert.IsTrue(proxy.IsOptionSet("opt"));
        Assert.IsFalse(proxy.IsOptionSet("missing"));
        Assert.IsTrue(proxy.TryGetOptionArgumentList("opt", out string[]? arguments));
        Assert.AreSequenceEqual(["a", "b"], arguments);
        Assert.IsFalse(proxy.TryGetOptionArgumentList("missing", out arguments));
        Assert.IsNull(arguments);
    }

    [TestMethod]
    public void TryGetOptionArgumentListOrDefault_UnderlyingSupportsDefaults_UsesTheDefaults()
    {
        ICommandLineOptionsWithDefaults proxy = new CommandLineOptionsProxy();
        ((CommandLineOptionsProxy)proxy).SetCommandLineOptions(
            new TestCommandLineOptions(new() { ["opt"] = ["explicit"] }, new() { ["defaulted"] = ["default"] }));

        Assert.IsTrue(proxy.TryGetOptionArgumentListOrDefault("opt", out string[]? arguments));
        Assert.AreSequenceEqual(["explicit"], arguments);
        Assert.IsTrue(proxy.TryGetOptionArgumentListOrDefault("defaulted", out arguments));
        Assert.AreSequenceEqual(["default"], arguments);
        Assert.IsFalse(proxy.TryGetOptionArgumentListOrDefault("missing", out arguments));
        Assert.IsNull(arguments);
    }

    [TestMethod]
    public void TryGetOptionArgumentListOrDefault_UnderlyingWithoutDefaults_FallsBackToExplicitArguments()
    {
        ICommandLineOptionsWithDefaults proxy = new CommandLineOptionsProxy();
        ((CommandLineOptionsProxy)proxy).SetCommandLineOptions(new PlainCommandLineOptions());

        Assert.IsTrue(proxy.TryGetOptionArgumentListOrDefault("opt", out string[]? arguments));
        Assert.AreSequenceEqual(["plain"], arguments);
        Assert.IsFalse(proxy.TryGetOptionArgumentListOrDefault("missing", out arguments));
        Assert.IsNull(arguments);
    }

    private sealed class PlainCommandLineOptions : ICommandLineOptions
    {
        public bool IsOptionSet(string optionName) => optionName == "opt";

        public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments)
        {
            arguments = optionName == "opt" ? ["plain"] : null;
            return arguments is not null;
        }
    }
}
