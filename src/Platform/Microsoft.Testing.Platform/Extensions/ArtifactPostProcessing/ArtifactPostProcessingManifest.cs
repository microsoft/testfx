// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Resources;

#if NETCOREAPP
using System.Text.Json;
#else
using Jsonite;
#endif

namespace Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

internal sealed class ArtifactPostProcessingManifest(
    string outputDirectory,
    IReadOnlyList<InputArtifact> inputs,
    ArtifactPostProcessingContext context)
{
    public string OutputDirectory { get; } = outputDirectory;

    public IReadOnlyList<InputArtifact> Inputs { get; } = inputs;

    public ArtifactPostProcessingContext Context { get; } = context;

    public static ArtifactPostProcessingManifest Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Dictionary<string, string?> values;
        Dictionary<string, string?> propertiesWithChildren;
        try
        {
            (values, propertiesWithChildren) = JsonConfigurationFileParser.Parse(stream);
        }
        catch (JsonException ex)
        {
            throw new FormatException(ex.Message, ex);
        }
        catch (InvalidCastException ex)
        {
            throw new FormatException(ex.Message, ex);
        }

        if (!propertiesWithChildren.TryGetValue("schemaVersion", out string? schemaVersionJson)
            || schemaVersionJson?.Trim() != "1"
            || !values.TryGetValue("outputDirectory", out string? outputDirectory)
            || !propertiesWithChildren.TryGetValue("outputDirectory", out string? outputDirectoryJson)
            || !IsJsonString(outputDirectoryJson)
            || RoslynString.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new FormatException(PlatformResources.ArtifactPostProcessingManifestInvalid);
        }

        if (!propertiesWithChildren.TryGetValue("inputs", out string? inputsJson)
            || inputsJson is null
            || !inputsJson.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            throw new FormatException(PlatformResources.ArtifactPostProcessingManifestInputsMustBeArray);
        }

        int[] inputIndices = [.. values.Keys
            .Where(key => key.StartsWith("inputs:", StringComparison.Ordinal))
            .Select(key => key.Split(':'))
            .Where(parts => parts.Length >= 2 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out _))
            .Select(parts => int.Parse(parts[1], CultureInfo.InvariantCulture))
            .Distinct()
            .OrderBy(index => index)];

        List<InputArtifact> inputs = [];
        foreach (int index in inputIndices)
        {
            string prefix = $"inputs:{index}:";
            if (!values.TryGetValue(prefix + "path", out string? inputPath)
                || !propertiesWithChildren.TryGetValue(prefix + "path", out string? inputPathJson)
                || !IsJsonString(inputPathJson)
                || RoslynString.IsNullOrWhiteSpace(inputPath))
            {
                throw new FormatException(string.Format(CultureInfo.CurrentCulture, PlatformResources.ArtifactPostProcessingManifestInputMissingPath, index));
            }

            inputs.Add(new InputArtifact(
                inputPath,
                GetValue(values, propertiesWithChildren, prefix + "kind"),
                GetValue(values, propertiesWithChildren, prefix + "producingTestModule"),
                GetValue(values, propertiesWithChildren, prefix + "targetFramework"),
                GetValue(values, propertiesWithChildren, prefix + "architecture"),
                GetValue(values, propertiesWithChildren, prefix + "executionId")));
        }

        string? truncationReasonValue = null;
        if (propertiesWithChildren.ContainsKey("truncationReason"))
        {
            truncationReasonValue = GetValue(values, propertiesWithChildren, "truncationReason")
                ?? throw new FormatException(PlatformResources.ArtifactPostProcessingManifestInvalid);
        }

        ArtifactPostProcessingTruncationReason truncationReason =
            truncationReasonValue switch
            {
                null => ArtifactPostProcessingTruncationReason.None,
                "maximumFailedTests" => ArtifactPostProcessingTruncationReason.MaximumFailedTests,
                "timeout" => ArtifactPostProcessingTruncationReason.Timeout,
                _ => throw new FormatException(PlatformResources.ArtifactPostProcessingManifestInvalid),
            };

        ArtifactPostProcessingRunSummary? runSummary = LoadRunSummary(values, propertiesWithChildren);

        return new ArtifactPostProcessingManifest(
            outputDirectory,
            inputs,
            new ArtifactPostProcessingContext(truncationReason, runSummary));
    }

    private static ArtifactPostProcessingRunSummary? LoadRunSummary(
        Dictionary<string, string?> values,
        Dictionary<string, string?> propertiesWithChildren)
    {
        if (!propertiesWithChildren.TryGetValue("runSummary", out string? runSummaryJson))
        {
            return null;
        }

        if (runSummaryJson is null || !runSummaryJson.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            throw new FormatException(PlatformResources.ArtifactPostProcessingManifestRunSummaryInvalid);
        }

        try
        {
            long totalTests = GetIntegerValue(values, propertiesWithChildren, "runSummary:totalTests");
            long passedTests = GetIntegerValue(values, propertiesWithChildren, "runSummary:passedTests");
            long failedTests = GetIntegerValue(values, propertiesWithChildren, "runSummary:failedTests");
            long skippedTests = GetIntegerValue(values, propertiesWithChildren, "runSummary:skippedTests");
            long durationTicks = GetIntegerValue(values, propertiesWithChildren, "runSummary:durationTicks");
            long exitCode = GetIntegerValue(values, propertiesWithChildren, "runSummary:exitCode");
            long testModuleCount = GetIntegerValue(values, propertiesWithChildren, "runSummary:testModuleCount");

            return durationTicks < 0
                || exitCode is < int.MinValue or > int.MaxValue
                || testModuleCount is <= 0 or > int.MaxValue
                    ? throw new FormatException(PlatformResources.ArtifactPostProcessingManifestRunSummaryInvalid)
                    : new ArtifactPostProcessingRunSummary(
                        totalTests,
                        passedTests,
                        failedTests,
                        skippedTests,
                        TimeSpan.FromTicks(durationTicks),
                        (int)exitCode,
                        (int)testModuleCount);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            throw new FormatException(PlatformResources.ArtifactPostProcessingManifestRunSummaryInvalid, ex);
        }
    }

    private static long GetIntegerValue(
        Dictionary<string, string?> values,
        Dictionary<string, string?> propertiesWithChildren,
        string key)
        => !propertiesWithChildren.TryGetValue(key, out string? valueJson)
            || valueJson is null
            || valueJson.TrimStart().StartsWith("\"", StringComparison.Ordinal)
            || !values.TryGetValue(key, out string? value)
            || !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result)
                ? throw new FormatException(PlatformResources.ArtifactPostProcessingManifestRunSummaryInvalid)
                : result;

    private static string? GetValue(
        Dictionary<string, string?> values,
        Dictionary<string, string?> propertiesWithChildren,
        string key)
        => !propertiesWithChildren.TryGetValue(key, out string? valueJson)
            ? null
            : valueJson is null || valueJson.Trim() == "null"
                ? null
                : !IsJsonString(valueJson)
                    ? throw new FormatException(PlatformResources.ArtifactPostProcessingManifestInvalid)
                    : values.TryGetValue(key, out string? value)
                        ? value
                        : throw new FormatException(PlatformResources.ArtifactPostProcessingManifestInvalid);

    private static bool IsJsonString(string? json)
        => json?.TrimStart().StartsWith("\"", StringComparison.Ordinal) ?? false;
}
