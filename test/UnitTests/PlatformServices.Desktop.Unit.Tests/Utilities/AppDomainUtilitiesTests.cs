// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Utilities
{
    extern alias FrameworkV1;

    using System;
    using System.IO;
    using System.Xml;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class AppDomainUtilitiesTests
    {
        private TestableXmlUtilities testableXmlUtilities;

        [TestInitialize]
        public void TestInit()
        {
            this.testableXmlUtilities = new TestableXmlUtilities();
            AppDomainUtilities.XmlUtilities = this.testableXmlUtilities;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AppDomainUtilities.XmlUtilities = null;
        }

        [TestMethod]
        public void SetConfigurationFileShouldSetOMRedirectionIfConfigFileIsPresent()
        {
            AppDomainSetup setup = new AppDomainSetup();
            var configFile = @"C:\temp\foo.dll.config";

            // Setup mocks.
            this.testableXmlUtilities.ConfigXml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
</configuration>";

            AppDomainUtilities.SetConfigurationFile(setup, configFile);

            // Assert Config file being set.
            Assert.AreEqual(configFile, setup.ConfigurationFile);

            // Assert Config Bytes.
            var expectedRedir = "<dependentAssembly><assemblyIdentity name=\"Microsoft.VisualStudio.TestPlatform.ObjectModel\" publicKeyToken=\"b03f5f7f11d50a3a\" culture=\"neutral\" /><bindingRedirect oldVersion=\"11.0.0.0\" newVersion=\"15.0.0.0\" />";

            var observedConfigBytes = setup.GetConfigurationBytes();
            var observedXml = System.Text.Encoding.UTF8.GetString(observedConfigBytes);

            Assert.IsTrue(observedXml.Replace("\r\n", string.Empty).Replace(" ", string.Empty).Contains(expectedRedir.Replace(" ", string.Empty)), "Config must have OM redirection");
        }

        [TestMethod]
        public void SetConfigurationFileShouldSetToCurrentDomainsConfigFileIfSourceDoesNotHaveAConfig()
        {
            AppDomainSetup setup = new AppDomainSetup();

            AppDomainUtilities.SetConfigurationFile(setup, null);

            // Assert Config file being set.
            Assert.AreEqual(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile, setup.ConfigurationFile);

            Assert.IsNull(setup.GetConfigurationBytes());
        }

        [TestMethod]
        public void GetTargetFrameworkVersionFromVersionStringShouldReturnDefaultVersionIfversionIsPortable()
        {
            var expected = new Version();

            var version = AppDomainUtilities.GetTargetFrameworkVersionFromVersionString(".NETPortable,Version=v4.5,Profile=Profile259");

            Assert.AreEqual(expected.Major, version.Major);
            Assert.AreEqual(expected.Minor, version.Minor);
        }

        [TestMethod]
        public void GetTargetFrameworkVersionFromVersionStringShouldReturnCurrectVersion()
        {
            var expected = new Version("4.5");

            var version = AppDomainUtilities.GetTargetFrameworkVersionFromVersionString(".NETFramework,Version=v4.5");

            Assert.AreEqual(expected.Major, version.Major);
            Assert.AreEqual(expected.Minor, version.Minor);
        }

        #region Testable Implementations

        internal class TestableXmlUtilities : XmlUtilities
        {
            internal string ConfigXml { get; set; }

            internal override XmlDocument GetXmlDocument(string configFile)
            {
                if (!string.IsNullOrEmpty(this.ConfigXml))
                {
                    var doc = new XmlDocument();
                    try
                    {
                        doc.LoadXml(this.ConfigXml);
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
}
