﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

using System;
using System.Xml;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

using TestFramework.ForTestingMSTest;

public class AppDomainUtilitiesTests : TestContainer
{
    private readonly TestableXmlUtilities _testableXmlUtilities;

    public AppDomainUtilitiesTests()
    {
        _testableXmlUtilities = new TestableXmlUtilities();
        AppDomainUtilities.XmlUtilities = _testableXmlUtilities;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            AppDomainUtilities.XmlUtilities = null;
        }
    }

    public void SetConfigurationFileShouldSetOMRedirectionIfConfigFileIsPresent()
    {
        AppDomainSetup setup = new();
        var configFile = @"C:\temp\foo.dll.config";

        // Setup mocks.
        _testableXmlUtilities.ConfigXml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
</configuration>";

        AppDomainUtilities.SetConfigurationFile(setup, configFile);

        // Assert Config file being set.
        Verify(configFile == setup.ConfigurationFile);

        // Assert Config Bytes.
        var expectedRedir = "<dependentAssembly><assemblyIdentity name=\"Microsoft.VisualStudio.TestPlatform.ObjectModel\" publicKeyToken=\"b03f5f7f11d50a3a\" culture=\"neutral\" /><bindingRedirect oldVersion=\"11.0.0.0\" newVersion=\"15.0.0.0\" />";

        var observedConfigBytes = setup.GetConfigurationBytes();
        var observedXml = System.Text.Encoding.UTF8.GetString(observedConfigBytes);

        Verify(observedXml.Replace("\r\n", string.Empty).Replace(" ", string.Empty).Contains(expectedRedir.Replace(" ", string.Empty)), "Config must have OM redirection");
    }

    public void SetConfigurationFileShouldSetToCurrentDomainsConfigFileIfSourceDoesNotHaveAConfig()
    {
        AppDomainSetup setup = new();

        AppDomainUtilities.SetConfigurationFile(setup, null);

        // Assert Config file being set.
        Verify(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile == setup.ConfigurationFile);

        Verify(setup.GetConfigurationBytes() is null);
    }

    public void GetTargetFrameworkVersionFromVersionStringShouldReturnDefaultVersionIfversionIsPortable()
    {
        var expected = new Version();

        var version = AppDomainUtilities.GetTargetFrameworkVersionFromVersionString(".NETPortable,Version=v4.5,Profile=Profile259");

        Verify(expected.Major == version.Major);
        Verify(expected.Minor == version.Minor);
    }

    public void GetTargetFrameworkVersionFromVersionStringShouldReturnCurrectVersion()
    {
        var expected = new Version("4.5");

        var version = AppDomainUtilities.GetTargetFrameworkVersionFromVersionString(".NETFramework,Version=v4.5");

        Verify(expected.Major == version.Major);
        Verify(expected.Minor == version.Minor);
    }

    #region Testable Implementations

    internal class TestableXmlUtilities : XmlUtilities
    {
        internal string ConfigXml { get; set; }

        internal override XmlDocument GetXmlDocument(string configFile)
        {
            if (!string.IsNullOrEmpty(ConfigXml))
            {
                var doc = new XmlDocument();
                try
                {
                    doc.LoadXml(ConfigXml);
                }
                catch (XmlException)
                {
                    // Eating any exceptions while loading. Just return the empty doc in this case.
                    // We need this to simulate an empty config file.
                }

                return doc;
            }
            else
            {
                return base.GetXmlDocument(configFile);
            }
        }
    }

#endregion
}
#endif
