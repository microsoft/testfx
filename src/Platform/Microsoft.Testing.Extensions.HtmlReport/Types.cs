// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal sealed class Test
{
    [JsonPropertyName("outcome")]
    public string? Outcome { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("exception")]
    public string? Exception { get; set; }

    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; set; }

    [JsonPropertyName("expected")]
    public string? Expected { get; set; }

    [JsonPropertyName("actual")]
    public string? Actual { get; set; }

    [JsonPropertyName("startTime")]
    public string? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public string? EndTime { get; set; }

    [JsonPropertyName("testOwner")]
    public string? TestOwner { get; set; }

    [JsonPropertyName("testDescription")]
    public string? TestDescription { get; set; }

    [JsonPropertyName("testGroup")]
    public string? TestGroup { get; set; }
}

internal sealed class Project
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("os")]
    public string? Os { get; set; }

    [JsonPropertyName("tfm")]
    public string? Tfm { get; set; }

    [JsonPropertyName("arch")]
    public string? Arch { get; set; }

    [JsonPropertyName("outcome")]
    public string? Outcome { get; set; }

    [JsonPropertyName("failed")]
    public int? Failed { get; set; }

    [JsonPropertyName("passed")]
    public int? Passed { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("skipped")]
    public int? Skipped { get; set; }

    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("tests")]
    public List<Test> Tests { get; } = [];
}

internal sealed class Run
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("labels")]
    public List<string> Labels { get; } = [];

    [JsonPropertyName("outcome")]
    public string? Outcome { get; set; }

    [JsonPropertyName("failed")]
    public int? Failed { get; set; }

    [JsonPropertyName("passed")]
    public int? Passed { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("skipped")]
    public int? Skipped { get; set; }

    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("projects")]
    public List<Project> Projects { get; } = [];
}
