// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

using MSTest.PlatformServices.Interface;

using TestFramework.ForTestingMSTest;

namespace MSTest.PlatformServices.UnitTests;

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

        Verify(properties is not null);
        Verify(properties.Count > 0);
    }

    public void SettingsShouldReturnDefaultSettingsIfNotInitialized()
    {
        MSTestAdapterSettings settings = MSTestSettingsProvider.Settings;

        Verify(settings is not null);
        Verify(settings.DeploymentEnabled);
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
        Verify(!MSTestSettingsProvider.Settings.DeploymentEnabled);
    }

    public void LoadShouldThrowIfReaderIsNull() =>
        VerifyThrows<ArgumentNullException>(() => _settingsProvider.Load(null!));

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
        Verify(!MSTestSettingsProvider.Settings.DeploymentEnabled);
    }

    public void LoadShouldReadAndFillInSettingsFromIConfiguration()
    {
        Verify(MSTestSettingsProvider.Settings.DeploymentEnabled);

        MSTestSettingsProvider.Load(new MockConfiguration(
            new Dictionary<string, string?>()
            {
                ["mstest:deployment:enabled"] = "false",
            }, null));

        Verify(!MSTestSettingsProvider.Settings.DeploymentEnabled);
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
