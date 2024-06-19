﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.Testing.Extensions.VSTestBridge.Requests;

/// <summary>
/// A specialized test execution filter for VSTest. It contains the VSTest specific properties.
/// </summary>
public sealed class VSTestTestExecutionFilter : ITestExecutionFilter
{
    internal VSTestTestExecutionFilter()
    {
    }

    internal VSTestTestExecutionFilter(ImmutableArray<TestCase> testCases)
    {
        TestCases = testCases;
    }

    public ImmutableArray<TestCase>? TestCases { get; }
}
