// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Helpers;

public class RunSettingsUtilitiesTests : TestContainer
{
    public void GetTestRunParametersReturnsNullOnNullRunSettings()
    {
        Dictionary<string, object>? trp = RunSettingsUtilities.GetTestRunParameters(null);
        trp.Should().BeNull();
    }

    public void GetTestRunParametersReturnsNullWhenNoTestRunParameters()
    {
        string settingsXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
               <RunConfiguration>
                 <ResultsDirectory>.\TestResults</ResultsDirectory>
                 <TargetPlatform>x86</TargetPlatform>
                 <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
               </RunConfiguration>
            </RunSettings>
            """;

        Dictionary<string, object>? trp = RunSettingsUtilities.GetTestRunParameters(settingsXml);
        trp.Should().BeNull();
    }

    public void GetTestRunParametersReturnsEmptyDictionaryForEmptyTestRunParametersNode()
    {
        string settingsXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
              <RunConfiguration>
                <ResultsDirectory>.\TestResults</ResultsDirectory>
                <TargetPlatform>x86</TargetPlatform>
                <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
              </RunConfiguration>
              <TestRunParameters>
              </TestRunParameters>
            </RunSettings>
            """;

        Dictionary<string, object>? trp = RunSettingsUtilities.GetTestRunParameters(settingsXml);
        trp.Should().BeEmpty();
    }

    public void GetTestRunParametersReturns1EntryOn1TestRunParameter()
    {
        string settingsXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
               <RunConfiguration>
                 <ResultsDirectory>.\TestResults</ResultsDirectory>
                 <TargetPlatform>x86</TargetPlatform>
                 <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
               </RunConfiguration>
               <TestRunParameters>
                 <Parameter name="webAppUrl" value="http://localhost" />
               </TestRunParameters>
            </RunSettings>
            """;

        Dictionary<string, object>? trp = RunSettingsUtilities.GetTestRunParameters(settingsXml);
        trp.Should().NotBeNull();
        trp.Count.Should().Be(1);

        // Verify Parameter Values.
        trp.Should().ContainKey("webAppUrl");
        trp["webAppUrl"].Should().Be("http://localhost");
    }

    public void GetTestRunParametersReturns3EntryOn3TestRunParameter()
    {
        string settingsXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
              <RunConfiguration>
                <ResultsDirectory>.\TestResults</ResultsDirectory>
                <TargetPlatform>x86</TargetPlatform>
                <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
              </RunConfiguration>
              <TestRunParameters>
                <Parameter name="webAppUrl" value="http://localhost" />
                <Parameter name="webAppUserName" value="Admin" />
                <Parameter name="webAppPassword" value="Password" />
              </TestRunParameters>
            </RunSettings>
            """;

        Dictionary<string, object>? trp = RunSettingsUtilities.GetTestRunParameters(settingsXml);
        trp.Should().NotBeNull();
        trp.Count.Should().Be(3);

        // Verify Parameter Values.
        trp.Should().ContainKey("webAppUrl");
        trp["webAppUrl"].Should().Be("http://localhost");
        trp.Should().ContainKey("webAppUserName");
        trp["webAppUserName"].Should().Be("Admin");
        trp.Should().ContainKey("webAppPassword");
        trp["webAppPassword"].Should().Be("Password");
    }

    public void GetTestRunParametersThrowsWhenTRPNodeHasAttributes()
    {
        string settingsXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
               <RunConfiguration>
                 <ResultsDirectory>.\TestResults</ResultsDirectory>
                 <TargetPlatform>x86</TargetPlatform>
                 <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
               </RunConfiguration>
               <TestRunParameters count="1">
                 <Parameter name="webAppUrl" value="http://localhost" />
               </TestRunParameters>
            </RunSettings>
            """;

        new Action(() => RunSettingsUtilities.GetTestRunParameters(settingsXml)).Should().Throw<SettingsException>();
    }

    public void GetTestRunParametersThrowsWhenTRPNodeHasNonParameterTypeChildNodes()
    {
        string settingsXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
              <RunConfiguration>
                <ResultsDirectory>.\TestResults</ResultsDirectory>
                <TargetPlatform>x86</TargetPlatform>
                <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
              </RunConfiguration>
              <TestRunParameters>
                <Parameter name="webAppUrl" value="http://localhost" />
                <TargetPlatform>x86</TargetPlatform>
              </TestRunParameters>
            </RunSettings>
            """;

        new Action(() => RunSettingsUtilities.GetTestRunParameters(settingsXml)).Should().Throw<SettingsException>();
    }

    public void GetTestRunParametersIgnoresMalformedKeyValues()
    {
        string settingsXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
               <RunConfiguration>
                 <ResultsDirectory>.\TestResults</ResultsDirectory>
                 <TargetPlatform>x86</TargetPlatform>
                 <TargetFrameworkVersion>Framework40</TargetFrameworkVersion>
               </RunConfiguration>
               <TestRunParameters>
                 <Parameter name="webAppUrl" values="http://localhost" />
               </TestRunParameters>
            </RunSettings>
            """;

        Dictionary<string, object>? trp = RunSettingsUtilities.GetTestRunParameters(settingsXml);
        trp.Should().BeEmpty();
    }
}
