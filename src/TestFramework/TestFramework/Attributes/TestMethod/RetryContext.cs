﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Represents the context for a test retry.
/// </summary>
[Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
public readonly struct RetryContext
{
    internal RetryContext(Func<Task<TestResult[]>> executeTaskGetter)
        => ExecuteTaskGetter = executeTaskGetter;

    /// <summary>
    /// Gets the function that will execute the test asynchronously.
    /// </summary>
    public Func<Task<TestResult[]>> ExecuteTaskGetter { get; }
}
