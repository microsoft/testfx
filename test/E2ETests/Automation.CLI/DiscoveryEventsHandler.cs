// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.CLIAutomation;

using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

public class DiscoveryEventsHandler : ITestDiscoveryEventsHandler
{
    /// <summary>
    /// Gets a list of Discovered tests.
    /// </summary>
    public IList<string> Tests { get; private set; }

    public DiscoveryEventsHandler()
    {
        this.Tests = new List<string>();
    }

    public void HandleDiscoveredTests(IEnumerable<TestCase> discoveredTestCases)
    {
        if (discoveredTestCases != null)
        {
            foreach (TestCase testCase in discoveredTestCases)
            {
                this.Tests.Add(testCase.FullyQualifiedName);
            }
        }
    }

    public void HandleDiscoveryComplete(long totalTests, IEnumerable<TestCase> lastChunk, bool isAborted)
    {
        if (lastChunk != null)
        {
            foreach (TestCase testCase in lastChunk)
            {
                this.Tests.Add(testCase.FullyQualifiedName);
            }
        }
    }

    public void HandleLogMessage(TestMessageLevel level, string message)
    {
        switch ((TestMessageLevel)level)
        {
            case TestMessageLevel.Informational:
                EqtTrace.Info(message);
                break;
            case TestMessageLevel.Warning:
                EqtTrace.Warning(message);
                break;
            case TestMessageLevel.Error:
                EqtTrace.Error(message);
                break;
            default:
                EqtTrace.Info(message);
                break;
        }
    }

    public void HandleRawMessage(string rawMessage)
    {
    }
}
