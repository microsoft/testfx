// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.PlatformServices.ObjectModel;

[Serializable]
internal readonly struct FixtureTestResult
{
    internal FixtureTestResult(bool isExecuted, UTF.UnitTestOutcome outcome)
    {
        IsExecuted = isExecuted;
        Outcome = outcome;
    }

    /// <summary>
    /// Gets a value indicating whether the test is executed or not.
    /// </summary>
    public bool IsExecuted { get; }

    /// <summary>
    /// Gets the outcome of the test.
    /// </summary>
    public UTF.UnitTestOutcome Outcome { get; }
}
