// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// Internal class to indicate Test Execution failure.
/// </summary>
[Serializable]
internal sealed class TestFailedException : Exception
{
    public TestFailedException(UTF.UnitTestOutcome outcome, string errorMessage)
        : this(outcome, errorMessage, null, null)
    {
    }

    public TestFailedException(UTF.UnitTestOutcome outcome, string errorMessage, StackTraceInformation? stackTraceInformation)
        : this(outcome, errorMessage, stackTraceInformation, null)
    {
    }

    public TestFailedException(UTF.UnitTestOutcome outcome, string errorMessage, Exception? realException)
        : this(outcome, errorMessage, null, realException)
    {
    }

    public TestFailedException(UTF.UnitTestOutcome outcome, string errorMessage, StackTraceInformation? stackTraceInformation, Exception? realException)
        : base(errorMessage, realException)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(errorMessage), "ErrorMessage should not be empty");

        Outcome = outcome;
        StackTraceInformation = stackTraceInformation;

        // Copy Data from the real exception to preserve assertion metadata
        if (realException?.Data is not null)
        {
            foreach (object key in realException.Data.Keys)
            {
                Data[key] = realException.Data[key];
            }
        }
    }

    /// <summary>
    /// Gets stack trace information associated with the test failure.
    /// </summary>
    public StackTraceInformation? StackTraceInformation { get; private set; }

    /// <summary>
    /// Gets outcome of the test case.
    /// </summary>
    public UTF.UnitTestOutcome Outcome { get; private set; }

    public override string? StackTrace
        => StackTraceInformation is null ? base.StackTrace : StackTraceInformation.ErrorStackTrace;
}
