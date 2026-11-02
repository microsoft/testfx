// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Configuration;
using Microsoft.Testing.Platform.Configurations;

using MelConfigurationBuilder = Microsoft.Extensions.Configuration.ConfigurationBuilder;
using MelConfigurationExtensions = Microsoft.Extensions.Configuration.MemoryConfigurationBuilderExtensions;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class ConfigurationSnapshotLifecycleBehaviorTests
{
    [TestMethod]
    public async Task Source_BuildsIndependentLoadTimeSnapshotsAndDescriptor()
    {
        Microsoft.Extensions.Configuration.IConfiguration configuration = MelConfigurationExtensions.AddInMemoryCollection(
            new MelConfigurationBuilder(), new Dictionary<string, string?> { ["Root:Value"] = "before" }).Build();
        var source = new MicrosoftExtensionsConfigurationSnapshotSource(configuration, -7);
        IConfigurationProvider first = await source.BuildAsync(null!);
        IConfigurationProvider second = await source.BuildAsync(null!);
        await first.LoadAsync();
        configuration["Root:Value"] = "after";
        await second.LoadAsync();

        Assert.IsTrue(first.TryGet("ROOT:VALUE", out string? original));
        Assert.AreEqual("before", original);
        Assert.IsTrue(second.TryGet("root:value", out string? updated));
        Assert.AreEqual("after", updated);
        await first.LoadAsync();
        Assert.IsTrue(first.TryGet("root:value", out string? reloaded));
        Assert.AreEqual("after", reloaded);
        Assert.AreNotSame(first, second);
        Assert.AreEqual(-7, source.Order);
        Assert.AreEqual("MicrosoftExtensionsConfigurationSnapshot", source.Uid);
        Assert.AreEqual("Microsoft.Extensions.Configuration snapshot", source.DisplayName);
        Assert.AreEqual("Imports a snapshot of an externally owned Microsoft.Extensions.Configuration configuration.", source.Description);
        Assert.IsTrue(Version.TryParse(source.Version.Split(['-', '+'])[0], out _));
    }
}
