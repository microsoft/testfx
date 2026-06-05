// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ConfigurationSourceDefaultsTests
{
    [TestMethod]
    public async Task BuiltInSources_UseExpectedCommonDefaults()
    {
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());

        IConfigurationSource[] sources =
        [
            new CommandLineConfigurationSource(),
            new EnvironmentVariablesConfigurationSource(new SystemEnvironment()),
            new JsonConfigurationSource(testApplicationModuleInfo, new SystemFileSystem(), null),
        ];
        string expectedVersion = sources[0].Version;

        foreach (IConfigurationSource source in sources)
        {
            Assert.AreEqual(source.GetType().Name, source.Uid);
            Assert.AreEqual(expectedVersion, source.Version);
            Assert.AreEqual(string.Empty, source.DisplayName);
            Assert.AreEqual(string.Empty, source.Description);
            Assert.IsTrue(await source.IsEnabledAsync());
        }
    }
}
