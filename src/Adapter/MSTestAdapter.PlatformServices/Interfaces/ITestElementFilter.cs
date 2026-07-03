// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// Platform-agnostic predicate that decides whether a discovered or executed <see cref="UnitTestElement"/>
/// should be included in the current test run, based on the user-provided test-case filter.
/// </summary>
/// <remarks>
/// This abstraction lets the platform services discovery and execution pipelines apply filtering over the
/// neutral <see cref="UnitTestElement"/> model without taking a dependency on a specific test platform's
/// filter object model (for example the VSTest <c>ITestCaseFilterExpression</c>, <c>TestProperty</c> and
/// <c>IRunContext.GetTestCaseFilter</c> types). The concrete filter is produced at the platform boundary by a
/// wrapper that parses the host's filter (currently <c>TestMethodFilter</c>, which wraps the VSTest filter
/// expression), and is expected to move fully out of the platform services layer in a later phase.
/// A <see langword="null"/> filter means "no filter" and every element is included.
/// </remarks>
internal interface ITestElementFilter
{
    /// <summary>
    /// Determines whether the given <paramref name="testElement"/> matches the filter and should be included.
    /// </summary>
    /// <param name="testElement">The test element to evaluate.</param>
    /// <returns><see langword="true"/> if the element matches the filter; otherwise, <see langword="false"/>.</returns>
    bool Matches(UnitTestElement testElement);
}
