// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class SuiteSet
{
    public required string Name { get; set; }

    public required IReadOnlyList<Suite> Suites { get; set; }

    public required long TotalTests { get; set; }

    public required long TotalFailures { get; set; }

    public required long TotalErrors { get; set; }

    public required long TotalSkipped { get; set; }

    public required TimeSpan TotalDuration { get; set; }

    public required DateTimeOffset Timestamp { get; set; }
}

internal sealed class Suite
{
    public required string Name { get; set; }

    public required IReadOnlyList<TestCase> Tests { get; set; }

    public required int Failures { get; set; }

    public required int Errors { get; set; }

    public required int Skipped { get; set; }

    public required TimeSpan TotalDuration { get; set; }

    public required DateTimeOffset Timestamp { get; set; }
}

internal sealed class TestCase
{
    public required string ClassName { get; set; }

    public required string Name { get; set; }

    public required string OriginalName { get; set; }

    public required string TestPath { get; set; }

    public required CapturedTestResult Result { get; set; }

    public required int DuplicateIndex { get; set; }

    public required int DuplicateOf { get; set; }
}
