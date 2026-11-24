// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class SettingsProviderMutationTests : TestContainer
{
    private const string UnreachableMessage = "This program location is thought to be unreachable.*";

    private readonly IConfiguration? _previousConfiguration = MSTestAdapterSettings.Configuration;

    public SettingsProviderMutationTests()
    {
        MSTestSettingsProvider.Reset();
        MSTestAdapterSettings.Configuration = null;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            MSTestSettingsProvider.Reset();
            MSTestAdapterSettings.Configuration = _previousConfiguration;
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public void LoadFromConfigurationShouldFailWhenTheSettingsInstanceIsReplacedWhileParsing()
    {
        Action action = () => MSTestSettingsProvider.Load(new ResettingConfiguration());

        action.Should().Throw<Exception>().WithMessage(UnreachableMessage);
    }

    public void LoadFromXmlReaderShouldFailWhenTheSettingsInstanceIsReplacedWhileParsing()
    {
        var document = new XmlDocument { XmlResolver = null };
        document.LoadXml("<MSTest><Unknown /></MSTest>");
        using var reader = new ResettingXmlReader(document);
        reader.Read();
        reader.Armed = true;

        Action action = () => new MSTestSettingsProvider().Load(reader);

        action.Should().Throw<Exception>().WithMessage(UnreachableMessage);
    }

    private sealed class ResettingConfiguration : IConfiguration
    {
        public string? this[string key]
        {
            get
            {
                MSTestSettingsProvider.Reset();
                return null;
            }
        }
    }

    private sealed class ResettingXmlReader(XmlDocument document) : XmlNodeReader(document)
    {
        public bool Armed { get; set; }

        public override bool Read()
        {
            if (Armed)
            {
                Armed = false;
                MSTestSettingsProvider.Reset();
            }

            return base.Read();
        }
    }
}
