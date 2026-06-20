// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class SuiteSet
{
    public required string Name { get; init; }

    public required IReadOnlyList<Suite> Suites { get; init; }

    public required long TotalTests { get; init; }

    public required long TotalFailures { get; init; }

    public required long TotalErrors { get; init; }

    public required long TotalSkipped { get; init; }

    public required TimeSpan TotalDuration { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}

internal sealed class Suite
{
    public required string Name { get; init; }

    public required IReadOnlyList<TestCase> Tests { get; init; }

    public required int Failures { get; init; }

    public required int Errors { get; init; }

    public required int Skipped { get; init; }

    public required TimeSpan TotalDuration { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}

internal sealed class TestCase
{
    public required string ClassName { get; init; }

    public required string Name { get; set; }

    public required string OriginalName { get; init; }

    public required string TestPath { get; init; }

    public required CapturedTestResult Result { get; init; }

    public required int DuplicateIndex { get; set; }

    public required int DuplicateOf { get; set; }
}
