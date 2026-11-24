// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Configurations;

internal sealed class ConfigurationSection : IConfigurationSection
{
    private readonly AggregatedConfiguration _root;

    public ConfigurationSection(AggregatedConfiguration root, string path)
    {
        _root = root;
        Path = path;

        int delimiterIndex = path.LastIndexOf(PlatformConfigurationConstants.KeyDelimiter, StringComparison.Ordinal);
        Key = delimiterIndex < 0 ? path : path.Substring(delimiterIndex + 1);
    }

    public string Key { get; }

    public string Path { get; }

    public bool HasValue => _root.TryGetSectionValue(Path, out _);

    public string? Value => _root.TryGetSectionValue(Path, out string? value) ? value : null;

    public string? this[string key]
        => _root.TryGetSectionValue(CombinePath(Path, key), out string? value) ? value : null;

    public IConfigurationSection GetSection(string key)
    {
        _ = key ?? throw new ArgumentNullException(nameof(key));
        return new ConfigurationSection(_root, CombinePath(Path, key));
    }

    public IEnumerable<IConfigurationSection> GetChildren() => _root.GetChildren(Path);

    private static string CombinePath(string path, string key)
        => path + PlatformConfigurationConstants.KeyDelimiter + key;
}
