// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
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
        var properties = _settingsProvider.GetProperties(null);

        Verify(properties is not null);
        Verify(properties.Count > 0);
    }

    public void SettingsShouldReturnDefaultSettingsIfNotInitialized()
    {
        var settings = MSTestSettingsProvider.Settings;

        Verify(settings is not null);
        Verify(settings.DeploymentEnabled);
    }

    public void SettingsShouldReturnInitializedSettings()
    {
        string runSettingxml =
            @"<MSTestV2>
                        <DeploymentEnabled>False</DeploymentEnabled>
                  </MSTestV2>";
        StringReader stringReader = new(runSettingxml);
        XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();
        _settingsProvider.Load(reader);
        Verify(!MSTestSettingsProvider.Settings.DeploymentEnabled);
    }

    public void LoadShouldThrowIfReaderIsNull()
    {
        var exception = VerifyThrows(() => _settingsProvider.Load(null));
        Verify(exception is ArgumentNullException);
    }

    public void LoadShouldReadAndFillInSettings()
    {
        string runSettingxml =
            @"<MSTestV2>
                        <DeploymentEnabled>False</DeploymentEnabled>
                  </MSTestV2>";
        StringReader stringReader = new(runSettingxml);
        XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);
        reader.Read();
        _settingsProvider.Load(reader);
        Verify(!MSTestSettingsProvider.Settings.DeploymentEnabled);
    }
}
