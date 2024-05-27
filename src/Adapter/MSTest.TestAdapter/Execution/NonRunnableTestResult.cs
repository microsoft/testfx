// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal class NonRunnableTestResult
{
    public NonRunnableTestResult(bool reportTest, ObjectModel.UnitTestOutcome outcome, string? exceptionMessage)
    {
        ReportTest = reportTest;
        Outcome = outcome;
        ExceptionMessage = exceptionMessage;
    }

    /// <summary>
    /// Gets or sets a value indicating whether to report the test or not.
    /// </summary>
    public bool ReportTest { get; set; }

    /// <summary>
    /// Gets or sets the outcome of the test.
    /// </summary>
    public ObjectModel.UnitTestOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the exception message if any.
    /// </summary>
    public string? ExceptionMessage { get; set; }
}
