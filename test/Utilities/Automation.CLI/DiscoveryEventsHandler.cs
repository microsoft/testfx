// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Collections.Immutable;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.MSTestV2.CLIAutomation;

public class DiscoveryEventsHandler : ITestDiscoveryEventsHandler
{
    private readonly ImmutableArray<string>.Builder _testsBuilder = ImmutableArray.CreateBuilder<string>();
    private readonly ImmutableDictionary<TestMessageLevel, ImmutableArray<string?>.Builder>.Builder _messagesBuilder = ImmutableDictionary.CreateBuilder<TestMessageLevel, ImmutableArray<string?>.Builder>();
    private ImmutableArray<string>? _tests;
    private ImmutableDictionary<TestMessageLevel, ImmutableArray<string?>>? _messages;

    /// <summary>
    /// Gets a list of Discovered tests.
    /// </summary>
    public ImmutableArray<string> Tests => _tests ??= _testsBuilder.ToImmutable();

    /// <summary>
    /// Gets the list of messages received from the discovery process.
    /// </summary>
    public ImmutableDictionary<TestMessageLevel, ImmutableArray<string?>> Messages => _messages ??= _messagesBuilder.ToImmutableDictionary(x => x.Key, x => x.Value.ToImmutable());

    public void HandleDiscoveredTests(IEnumerable<TestCase>? discoveredTestCases)
    {
        if (discoveredTestCases != null)
        {
            foreach (TestCase testCase in discoveredTestCases)
            {
                _testsBuilder.Add(testCase.FullyQualifiedName);
            }
        }
    }

    public void HandleDiscoveryComplete(long totalTests, IEnumerable<TestCase>? lastChunk, bool isAborted)
    {
        if (lastChunk != null)
        {
            foreach (TestCase testCase in lastChunk)
            {
                _testsBuilder.Add(testCase.FullyQualifiedName);
            }
        }
    }

    public void HandleLogMessage(TestMessageLevel level, string? message)
    {
        if (!_messagesBuilder.TryGetValue(level, out ImmutableArray<string?>.Builder? value))
        {
            value = ImmutableArray.CreateBuilder<string?>();
            _messagesBuilder.Add(level, value);
        }

        value.Add(message);

        switch (level)
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
