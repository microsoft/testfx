// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using FrameworkTestResult = Microsoft.VisualStudio.TestTools.UnitTesting.TestResult;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// Platform-agnostic sink for reporting the lifecycle and results of test execution to whichever
/// test host is running the tests.
/// </summary>
/// <remarks>
/// This abstraction lets the platform services layer report test start/end and results without taking a
/// dependency on a specific test platform's result object model (for example the VSTest <c>TestResult</c>,
/// <c>TestOutcome</c> and attachment types). The concrete recorder is provided at the platform boundary by a
/// wrapper over the host's result recorder (currently <c>TestResultRecorderExtensions</c>, which wraps the
/// VSTest <c>ITestExecutionRecorder</c>), and is expected to move fully out of the platform services layer in
/// a later phase.
/// </remarks>
internal interface ITestResultRecorder
{
    /// <summary>
    /// Signals that execution of the given test has started.
    /// </summary>
    /// <param name="testElement">The test whose execution is starting.</param>
    void RecordStart(UnitTestElement testElement);

    /// <summary>
    /// Signals that execution of the given test ended without producing any result.
    /// </summary>
    /// <param name="testElement">The test whose execution ended.</param>
    void RecordEmptyResult(UnitTestElement testElement);

    /// <summary>
    /// Reports a single framework <see cref="FrameworkTestResult"/> for the given test to the test host.
    /// </summary>
    /// <param name="testElement">The test the result belongs to.</param>
    /// <param name="unitTestResult">The framework result produced by executing the test.</param>
    /// <param name="startTime">The time at which the test started executing.</param>
    /// <param name="endTime">The time at which the test finished executing.</param>
    /// <returns><see langword="true"/> if the result was reported as a failure; otherwise, <see langword="false"/>.</returns>
    bool RecordResult(UnitTestElement testElement, FrameworkTestResult unitTestResult, DateTimeOffset startTime, DateTimeOffset endTime);
}
