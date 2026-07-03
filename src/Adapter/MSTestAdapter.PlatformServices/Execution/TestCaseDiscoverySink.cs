// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// The test case discovery sink used internally by execution to collect the discovered tests.
/// </summary>
/// <remarks>
/// It implements the platform-agnostic <see cref="IUnitTestElementSink"/> but still materializes a VSTest
/// <see cref="TestCase"/> for every discovered element, because the execution pipeline currently consumes
/// <see cref="TestCase"/> instances. That materialization is expected to disappear once execution flows the
/// neutral <see cref="UnitTestElement"/> model end-to-end.
/// </remarks>
internal sealed class TestCaseDiscoverySink : IUnitTestElementSink
{
    /// <summary>
    /// Gets the tests.
    /// </summary>
    public ICollection<TestCase> Tests { get; } = [];

    /// <summary>
    /// Collects the discovered test, materializing it as a VSTest <see cref="TestCase"/>.
    /// </summary>
    /// <param name="testElement"> The discovered test element. </param>
    public void SendTestElement(UnitTestElement testElement)
        => Tests.Add(testElement.ToTestCase());
}
