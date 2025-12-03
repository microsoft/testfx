// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

#pragma warning disable SA1649 // File name must match first type name
public class DesktopSettingsProviderTests : TestContainer
#pragma warning restore SA1649 // File name must match first type name
{
    private readonly MSTestSettingsProvider _settingsProvider;

    public DesktopSettingsProviderTests()
    {
        _settingsProvider = new MSTestSettingsProvider();
        MSTestSettingsProvider.Reset();
    }

    public void GetPropertiesShouldReturnDeploymentInformation()
    {
        // this is a base case and we just validating that properties does not remain un-initialized,
        // so passing 'null' source will also suffice.
        IDictionary<string, object> properties = _settingsProvider.GetProperties(null);

        properties.Should().NotBeNull();
#if !WINDOWS_UWP && !WIN_UI
        properties.Count.Should().BeGreaterThan(0);
#endif
    }

    public void SettingsShouldReturnDefaultSettingsIfNotInitialized()
    {
        MSTestAdapterSettings settings = MSTestSettingsProvider.Settings;

        settings.Should().NotBeNull();
        settings.DeploymentEnabled.Should().BeTrue();
    }

    public void SettingsShouldReturnInitializedSettings()
    {
        string runSettingsXml =
            """
            <MSTestV2>
              <DeploymentEnabled>False</DeploymentEnabled>
            </MSTestV2>
            """;
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();
        _settingsProvider.Load(reader);
        MSTestSettingsProvider.Settings.DeploymentEnabled.Should().BeFalse();
    }

    public void LoadShouldThrowIfReaderIsNull() =>
        new Action(() => _settingsProvider.Load(null!)).Should().Throw<ArgumentNullException>();

    public void LoadShouldReadAndFillInSettings()
    {
        string runSettingsXml =
            """
            <MSTestV2>
              <DeploymentEnabled>False</DeploymentEnabled>
            </MSTestV2>
            """;
        StringReader stringReader = new(runSettingsXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();
        _settingsProvider.Load(reader);
        MSTestSettingsProvider.Settings.DeploymentEnabled.Should().BeFalse();
    }

    public void LoadShouldReadAndFillInSettingsFromIConfiguration()
    {
        MSTestSettingsProvider.Settings.DeploymentEnabled.Should().BeTrue();

        MSTestSettingsProvider.Load(new MockConfiguration(
            new Dictionary<string, string?>()
            {
                ["mstest:deployment:enabled"] = "false",
            }, null));

        MSTestSettingsProvider.Settings.DeploymentEnabled.Should().BeFalse();
    }

    private sealed class MockConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _values;
        private readonly string? _defaultValue;

        public MockConfiguration(Dictionary<string, string?> values, string? defaultValue)
        {
            _values = values;
            _defaultValue = defaultValue;
        }

        public string? this[string key]
            => _values.TryGetValue(key, out string? value) ? value : _defaultValue;
    }
}
