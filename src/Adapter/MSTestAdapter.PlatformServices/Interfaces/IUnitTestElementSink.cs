// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// Platform-agnostic sink that receives the tests discovered by the adapter and hands them to the
/// discovery consumer (a test host during discovery, or the execution pipeline during a run).
/// </summary>
/// <remarks>
/// This abstraction lets the platform services discovery pipeline emit the neutral
/// <see cref="UnitTestElement"/> model without taking a dependency on a specific test platform's
/// discovery object model (for example the VSTest <c>TestCase</c> and <c>ITestCaseDiscoverySink</c>
/// types). The concrete sink is produced at the platform boundary by a wrapper over the host's
/// discovery sink (currently <c>UnitTestElementSinkExtensions</c>, which wraps the VSTest
/// <c>ITestCaseDiscoverySink</c> and materializes a <c>TestCase</c> for each element), and is expected
/// to move fully out of the platform services layer in a later phase.
/// </remarks>
internal interface IUnitTestElementSink
{
    /// <summary>
    /// Reports a discovered <paramref name="testElement"/> to the running test host.
    /// </summary>
    /// <param name="testElement">The discovered test element.</param>
    /// <returns>A task that completes once the element has been reported.</returns>
    Task SendTestElementAsync(UnitTestElement testElement);
}
