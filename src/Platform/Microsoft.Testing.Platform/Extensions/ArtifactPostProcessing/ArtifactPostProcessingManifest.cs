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

internal sealed class ArtifactPostProcessingManifest(string outputDirectory, IReadOnlyList<InputArtifact> inputs)
{
    public string OutputDirectory { get; } = outputDirectory;

    public IReadOnlyList<InputArtifact> Inputs { get; } = inputs;

    public static ArtifactPostProcessingManifest Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Dictionary<string, string?> values;
        try
        {
            (values, _) = JsonConfigurationFileParser.Parse(stream);
        }
        catch (JsonException ex)
        {
            throw new FormatException(ex.Message, ex);
        }
        catch (InvalidCastException ex)
        {
            throw new FormatException(ex.Message, ex);
        }

        if (!values.TryGetValue("schemaVersion", out string? schemaVersion)
            || schemaVersion != "1"
            || !values.TryGetValue("outputDirectory", out string? outputDirectory)
            || RoslynString.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new FormatException(PlatformResources.ArtifactPostProcessingManifestInvalid);
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
            if (!values.TryGetValue(prefix + "path", out string? inputPath) || RoslynString.IsNullOrWhiteSpace(inputPath))
            {
                throw new FormatException(string.Format(CultureInfo.CurrentCulture, PlatformResources.ArtifactPostProcessingManifestInputMissingPath, index));
            }

            inputs.Add(new InputArtifact(
                inputPath,
                GetValue(values, prefix + "kind"),
                GetValue(values, prefix + "producingTestModule"),
                GetValue(values, prefix + "targetFramework"),
                GetValue(values, prefix + "architecture"),
                GetValue(values, prefix + "executionId")));
        }

        return new ArtifactPostProcessingManifest(outputDirectory, inputs);
    }

    private static string? GetValue(Dictionary<string, string?> values, string key)
        => values.TryGetValue(key, out string? value)
            && !RoslynString.IsNullOrEmpty(value)
            && value != "null"
                ? value
                : null;
}
