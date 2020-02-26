// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Helpers
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System.Collections.Generic;
    using global::MSTestAdapter.TestUtilities;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class RunSettingsUtilitiesTests
    {
        [TestMethod]
        public void GetTestRunParametersReturnsEmptyDictionaryOnNullRunSettings()
        {
            Dictionary<string, object> trp = RunSettingsUtilities.GetTestRunParameters(null);
            Assert.IsNotNull(trp);
            Assert.AreEqual(0, trp.Count);
        }

        [TestMethod]
        public void GetTestRunParametersReturnsEmptyDictionaryWhenNoTestRunParameters()
        {
            string settingsXml =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <RunSettings>
                     <RunConfiguration>
                       <ResultsDirectory>.\TestResults</ResultsDirectory>
                       <TargetPlatform>x86</TargetPlatform>
                       <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
                     </RunConfiguration>
                </RunSettings>";

            Dictionary<string, object> trp = RunSettingsUtilities.GetTestRunParameters(settingsXml);
            Assert.IsNotNull(trp);
            Assert.AreEqual(0, trp.Count);
        }

        [TestMethod]
        public void GetTestRunParametersReturnsEmptyDictionaryForEmptyTestRunParametersNode()
        {
            string settingsXml =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <RunSettings>
                     <RunConfiguration>
                       <ResultsDirectory>.\TestResults</ResultsDirectory>
                       <TargetPlatform>x86</TargetPlatform>
                       <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
                     </RunConfiguration>
                     <TestRunParameters>
                     </TestRunParameters>
                </RunSettings>";

            Dictionary<string, object> trp = RunSettingsUtilities.GetTestRunParameters(settingsXml);
            Assert.IsNotNull(trp);
            Assert.AreEqual(0, trp.Count);
        }

        [TestMethod]
        public void GetTestRunParametersReturns1EntryOn1TestRunParameter()
        {
            string settingsXml =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <RunSettings>
                     <RunConfiguration>
                       <ResultsDirectory>.\TestResults</ResultsDirectory>
                       <TargetPlatform>x86</TargetPlatform>
                       <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
                     </RunConfiguration>
                     <TestRunParameters>
                        <Parameter name=""webAppUrl"" value=""http://localhost"" />
                     </TestRunParameters>
                </RunSettings>";

            Dictionary<string, object> trp = RunSettingsUtilities.GetTestRunParameters(settingsXml);
            Assert.IsNotNull(trp);
            Assert.AreEqual(1, trp.Count);

            // Verify Parameter Values.
            Assert.IsTrue(trp.ContainsKey("webAppUrl"));
            Assert.AreEqual("http://localhost", trp["webAppUrl"]);
        }

        [TestMethod]
        public void GetTestRunParametersReturns3EntryOn3TestRunParameter()
        {
            string settingsXml =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <RunSettings>
                     <RunConfiguration>
                       <ResultsDirectory>.\TestResults</ResultsDirectory>
                       <TargetPlatform>x86</TargetPlatform>
                       <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
                     </RunConfiguration>
                     <TestRunParameters>
                        <Parameter name=""webAppUrl"" value=""http://localhost"" />
                        <Parameter name=""webAppUserName"" value=""Admin"" />
                        <Parameter name=""webAppPassword"" value=""Password"" />
                     </TestRunParameters>
                </RunSettings>";

            Dictionary<string, object> trp = RunSettingsUtilities.GetTestRunParameters(settingsXml);
            Assert.IsNotNull(trp);
            Assert.AreEqual(3, trp.Count);

            // Verify Parameter Values.
            Assert.IsTrue(trp.ContainsKey("webAppUrl"));
            Assert.AreEqual("http://localhost", trp["webAppUrl"]);
            Assert.IsTrue(trp.ContainsKey("webAppUserName"));
            Assert.AreEqual("Admin", trp["webAppUserName"]);
            Assert.IsTrue(trp.ContainsKey("webAppPassword"));
            Assert.AreEqual("Password", trp["webAppPassword"]);
        }

        [TestMethod]
        public void GetTestRunParametersThrowsWhenTRPNodeHasAttributes()
        {
            string settingsXml =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <RunSettings>
                     <RunConfiguration>
                       <ResultsDirectory>.\TestResults</ResultsDirectory>
                       <TargetPlatform>x86</TargetPlatform>
                       <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
                     </RunConfiguration>
                     <TestRunParameters count=""1"">
                        <Parameter name=""webAppUrl"" value=""http://localhost"" />
                     </TestRunParameters>
                </RunSettings>";

            ActionUtility.ActionShouldThrowExceptionOfType(() => RunSettingsUtilities.GetTestRunParameters(settingsXml), typeof(SettingsException));
        }

        [TestMethod]
        public void GetTestRunParametersThrowsWhenTRPNodeHasNonParameterTypeChildNodes()
        {
            string settingsXml =
               @"<?xml version=""1.0"" encoding=""utf-8""?>
                <RunSettings>
                     <RunConfiguration>
                       <ResultsDirectory>.\TestResults</ResultsDirectory>
                       <TargetPlatform>x86</TargetPlatform>
                       <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
                     </RunConfiguration>
                     <TestRunParameters>
                        <Parameter name=""webAppUrl"" value=""http://localhost"" />
                        <TargetPlatform>x86</TargetPlatform>
                     </TestRunParameters>
                </RunSettings>";

            ActionUtility.ActionShouldThrowExceptionOfType(() => RunSettingsUtilities.GetTestRunParameters(settingsXml), typeof(SettingsException));
        }

        [TestMethod]
        public void GetTestRunParametersIgnoresMalformedKeyValues()
        {
            string settingsXml =
               @"<?xml version=""1.0"" encoding=""utf-8""?>
                <RunSettings>
                     <RunConfiguration>
                       <ResultsDirectory>.\TestResults</ResultsDirectory>
                       <TargetPlatform>x86</TargetPlatform>
                       <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
                     </RunConfiguration>
                     <TestRunParameters>
                        <Parameter name=""webAppUrl"" values=""http://localhost"" />
                     </TestRunParameters>
                </RunSettings>";

            Dictionary<string, object> trp = RunSettingsUtilities.GetTestRunParameters(settingsXml);
            Assert.IsNotNull(trp);
            Assert.AreEqual(0, trp.Count);
        }
    }
}
