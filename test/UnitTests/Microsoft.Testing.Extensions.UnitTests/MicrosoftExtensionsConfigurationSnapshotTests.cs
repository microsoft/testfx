// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Testing.Extensions.Configuration;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Configurations;

using MelIConfiguration = Microsoft.Extensions.Configuration.IConfiguration;
using MtpIConfigurationProvider = Microsoft.Testing.Platform.Configurations.IConfigurationProvider;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class MicrosoftExtensionsConfigurationSnapshotTests
{
    [TestMethod]
    public void AddMicrosoftExtensionsConfigurationSnapshot_WithNullBuilder_Throws()
    {
        MelIConfiguration configuration = CreateConfiguration(("key", "value"));

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => ((ITestApplicationBuilder)null!).AddMicrosoftExtensionsConfigurationSnapshot(configuration));

        Assert.AreEqual("builder", exception.ParamName);
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsConfigurationSnapshot_WithNullConfiguration_Throws()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => builder.AddMicrosoftExtensionsConfigurationSnapshot(null!));

        Assert.AreEqual("configuration", exception.ParamName);
    }

    [TestMethod]
    public async Task AddMicrosoftExtensionsConfigurationSnapshot_ReturnsSameBuilder()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        MelIConfiguration configuration = CreateConfiguration(("key", "value"));

        ITestApplicationBuilder result = builder.AddMicrosoftExtensionsConfigurationSnapshot(configuration);

        Assert.AreSame(builder, result);
    }

    [TestMethod]
    public async Task Provider_LoadAsync_CapturesValuesAtLoadTime()
    {
        MelIConfiguration configuration = CreateConfiguration(("root:value", "before"));
        var provider = new MicrosoftExtensionsConfigurationSnapshotProvider(configuration);

        await provider.LoadAsync();
        configuration["root:value"] = "after";

        Assert.IsTrue(provider.TryGet("root:value", out string? value));
        Assert.AreEqual("before", value);
    }

    [TestMethod]
    public async Task Provider_PreservesHierarchyAndNullLeafSemantics()
    {
        MelIConfiguration configuration = CreateConfiguration(
            ("root:value", "scalar"),
            ("root:child:leaf", "nested"),
            ("root:nullLeaf", null));
        var provider = new MicrosoftExtensionsConfigurationSnapshotProvider(configuration);

        await provider.LoadAsync();

        Assert.AreSequenceEqual(new[] { "root" }, provider.GetChildKeys(null).OrderBy(static key => key).ToArray());
        Assert.AreSequenceEqual(new[] { "child", "nullLeaf", "value" }, provider.GetChildKeys("root").OrderBy(static key => key).ToArray());
        Assert.AreSequenceEqual(new[] { "leaf" }, provider.GetChildKeys("root:child").OrderBy(static key => key).ToArray());
        Assert.IsFalse(provider.TryGetScalar("root", out _));
        Assert.IsTrue(provider.TryGetScalar("root:value", out string? scalar));
        Assert.AreEqual("scalar", scalar);
        Assert.IsTrue(provider.TryGetScalar("root:nullLeaf", out string? nullLeaf));
        Assert.IsNull(nullLeaf);
    }

    [TestMethod]
    public async Task Provider_WithConfigurationSection_UsesSectionRelativeKeys()
    {
        MelIConfiguration configuration = CreateConfiguration(
            ("root:value", "scalar"),
            ("root:child:leaf", "nested"),
            ("other:value", "ignored"));
        var provider = new MicrosoftExtensionsConfigurationSnapshotProvider(configuration.GetSection("root"));

        await provider.LoadAsync();

        Assert.AreSequenceEqual(new[] { "child", "value" }, provider.GetChildKeys(null).OrderBy(static key => key).ToArray());
        Assert.IsTrue(provider.TryGet("value", out string? value));
        Assert.AreEqual("scalar", value);
        Assert.IsTrue(provider.TryGet("child:leaf", out string? leaf));
        Assert.AreEqual("nested", leaf);
        Assert.IsFalse(provider.TryGet("root:value", out _));
        Assert.IsFalse(provider.TryGet("other:value", out _));
    }

    [TestMethod]
    public async Task Source_UsesConfiguredOrderAndBuildsSnapshotProvider()
    {
        MelIConfiguration configuration = CreateConfiguration(("key", "value"));
        var source = new MicrosoftExtensionsConfigurationSnapshotSource(configuration, order: 7);

        MtpIConfigurationProvider provider = await source.BuildAsync(null!);

        Assert.AreEqual("MicrosoftExtensionsConfigurationSnapshot", source.Uid);
        Assert.AreEqual(7, source.Order);
        Assert.IsTrue(await source.IsEnabledAsync());
        Assert.IsInstanceOfType<MicrosoftExtensionsConfigurationSnapshotProvider>(provider);
    }

    private static MelIConfiguration CreateConfiguration(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
}
