// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Configuration;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Support for configuration settings for Tests.
/// </summary>
public static class TestConfiguration
{
    /// <summary>
    /// Gets the configuration section for tests.
    /// </summary>
    public static TestConfigurationSection ConfigurationSection { get; } = LoadConfiguration();

    private static TestConfigurationSection LoadConfiguration()
        => (TestConfigurationSection)ConfigurationManager.GetSection(ConfigurationNames.SectionName)
        ?? new TestConfigurationSection();
}
#endif
