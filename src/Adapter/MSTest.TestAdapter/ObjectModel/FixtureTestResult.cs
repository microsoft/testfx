// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

[Serializable]
internal sealed class FixtureTestResult
{
    internal FixtureTestResult(bool isExecuted, UnitTestOutcome outcome, string? exceptionMessage)
    {
        IsExecuted = isExecuted;
        Outcome = outcome;
        ExceptionMessage = exceptionMessage;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the test is executed or not.
    /// </summary>
    public bool IsExecuted { get; }

    /// <summary>
    /// Gets or sets the outcome of the test.
    /// </summary>
    public UnitTestOutcome Outcome { get; }

    /// <summary>
    /// Gets or sets the exception message if any.
    /// </summary>
    public string? ExceptionMessage { get; }
}
