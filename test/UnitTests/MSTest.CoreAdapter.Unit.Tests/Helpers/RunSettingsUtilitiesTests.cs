// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Helpers;

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestFramework.ForTestingMSTest;

public class RunSettingsUtilitiesTests : TestContainer
{
    public void GetTestRunParametersReturnsEmptyDictionaryOnNullRunSettings()
    {
        Dictionary<string, object> trp = RunSettingsUtilities.GetTestRunParameters(null);
        Verify(trp is not null);
        Verify(0 == trp.Count);
    }

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
        Verify(trp is not null);
        Verify(0 == trp.Count);
    }

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
        Verify(trp is not null);
        Verify(0 == trp.Count);
    }

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
        Verify(trp is not null);
        Verify(1 == trp.Count);

        // Verify Parameter Values.
        Verify(trp.ContainsKey("webAppUrl"));
        Verify("http://localhost".Equals(trp["webAppUrl"]));
    }

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
        Verify(trp is not null);
        Verify(3 == trp.Count);

        // Verify Parameter Values.
        Verify(trp.ContainsKey("webAppUrl"));
        Verify("http://localhost".Equals(trp["webAppUrl"]));
        Verify(trp.ContainsKey("webAppUserName"));
        Verify("Admin".Equals(trp["webAppUserName"]));
        Verify(trp.ContainsKey("webAppPassword"));
        Verify("Password".Equals(trp["webAppPassword"]));
    }

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

        var ex = VerifyThrows(() => RunSettingsUtilities.GetTestRunParameters(settingsXml));
        Verify(ex.GetType() == typeof(SettingsException));
    }

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

        var ex = VerifyThrows(() => RunSettingsUtilities.GetTestRunParameters(settingsXml));
        Verify(ex.GetType() == typeof(SettingsException));
    }

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
        Verify(trp is not null);
        Verify(0 == trp.Count);
    }
}
