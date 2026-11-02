// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// The discovery sink used internally by execution to collect the tests discovered from a source.
/// </summary>
/// <remarks>
/// It collects the neutral <see cref="UnitTestElement"/> model directly, so the execution pipeline can flow
/// discovered tests without round-tripping them through a VSTest <c>TestCase</c>.
/// </remarks>
internal sealed class TestCaseDiscoverySink : IUnitTestElementSink
{
    /// <summary>
    /// Gets the discovered tests.
    /// </summary>
    public ICollection<UnitTestElement> TestElements { get; } = [];

    /// <summary>
    /// Collects the discovered test element.
    /// </summary>
    /// <param name="testElement"> The discovered test element. </param>
    public Task SendTestElementAsync(UnitTestElement testElement)
    {
        TestElements.Add(testElement);
        return Task.CompletedTask;
    }
}
