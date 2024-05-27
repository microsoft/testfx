// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

[Serializable]
internal class NonRunnableTestResult
{
    public NonRunnableTestResult(bool reportTest, UnitTestOutcome outcome, string? exceptionMessage)
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
    public UnitTestOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the exception message if any.
    /// </summary>
    public string? ExceptionMessage { get; set; }
}
