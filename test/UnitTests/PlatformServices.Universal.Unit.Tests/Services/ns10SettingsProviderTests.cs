// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Services;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Moq;

using TestFramework.ForTestingMSTest;

public class SettingsProviderTests : TestContainer
{
    public void GetPropertiesShouldReturnEmptyDictionary()
    {
        MSTestSettingsProvider settings = new();

        Verify(0 == settings.GetProperties(It.IsAny<string>()).Count);
    }
}
