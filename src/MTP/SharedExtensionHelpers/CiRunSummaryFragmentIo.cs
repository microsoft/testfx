// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Shared implementation details are compiled into multiple extension assemblies.

internal static partial class CiRunSummaryAggregation
{
    private const string FragmentDirectoryName = ".ci-summary-fragments";
    private const string MergedDirectoryName = "merged";

    public static async Task<string> WriteFragmentAsync(
        string resultsDirectory,
        string provider,
        string providerSlug,
        CiRunSummaryModule module)
    {
        string fragmentDirectory = Path.Combine(resultsDirectory, FragmentDirectoryName);
        Directory.CreateDirectory(fragmentDirectory);
        string identity = $"{provider}\0{module.ModulePath}\0{module.TargetFramework}\0{module.Architecture}\0{module.ExecutionId}\0{module.SessionUid}";
        string fileName = $"{providerSlug}-{HashIdentity(identity).Substring(0, 32)}.json";
        string path = Path.Combine(fragmentDirectory, fileName);
        var fragment = new CiRunSummaryFragment
        {
            SchemaVersion = SchemaVersion,
            Provider = provider,
            Module = module,
        };
        string json = JsonSerializer.Serialize(fragment, CiRunSummaryJsonContext.Default.CiRunSummaryFragment);

        await WriteAtomicAsync(path, json).ConfigureAwait(false);
        return path;
    }

    public static string CreateAggregationId(IReadOnlyList<InputArtifact> inputs)
    {
        string identity = string.Join(
            "\0",
            inputs.Select(input => $"{Path.GetFullPath(input.Path)}\0{input.ExecutionId}")
                .OrderBy(value => value, StringComparer.Ordinal));
        return HashIdentity(identity).Substring(0, 32);
    }

    public static string GetMergedOutputPath(string outputDirectory, string providerSlug, string aggregationId)
    {
        string mergedDirectory = Path.Combine(outputDirectory, MergedDirectoryName);
        Directory.CreateDirectory(mergedDirectory);
        return (File.GetAttributes(mergedDirectory) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint
            ? throw new IOException($"The merged summary directory '{mergedDirectory}' cannot be a reparse point.")
            : Path.Combine(mergedDirectory, $"{providerSlug}-summary-{aggregationId}.md");
    }

    public static Task WriteOutputAsync(string path, string content)
        => WriteAtomicAsync(path, content);

    private static string HashIdentity(string identity)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(identity);
#if NETCOREAPP
        byte[] hash = SHA256.HashData(bytes);
#else
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(bytes);
#endif
        var builder = new StringBuilder(capacity: hash.Length * 2);
        for (int i = 0; i < hash.Length; i++)
        {
            builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static async Task WriteTextAsync(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(content).ConfigureAwait(false);
    }

    private static async Task WriteAtomicAsync(string path, string content)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!RoslynString.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await WriteTextAsync(tempPath, content).ConfigureAwait(false);
#if NETCOREAPP
            File.Move(tempPath, fullPath, overwrite: true);
#else
            File.Delete(fullPath);
            File.Move(tempPath, fullPath);
#endif
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup must not hide a successful write or its primary failure.
            }
        }
    }

    private sealed class CiRunSummaryFragment
    {
        public int SchemaVersion { get; set; }

        public string Provider { get; set; } = string.Empty;

        public CiRunSummaryModule? Module { get; set; }
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
    [JsonSerializable(typeof(CiRunSummaryFragment))]
    private sealed partial class CiRunSummaryJsonContext : JsonSerializerContext;
}

#pragma warning restore RS0051
