// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MelConfigurationPath = Microsoft.Extensions.Configuration.ConfigurationPath;
using MelIConfiguration = Microsoft.Extensions.Configuration.IConfiguration;
using MelIConfigurationSection = Microsoft.Extensions.Configuration.IConfigurationSection;
using MtpIHierarchicalConfigurationProvider = Microsoft.Testing.Platform.Configurations.IHierarchicalConfigurationProvider;

namespace Microsoft.Testing.Extensions.Configuration;

internal sealed class MicrosoftExtensionsConfigurationSnapshotProvider(
    MelIConfiguration configuration) : MtpIHierarchicalConfigurationProvider
{
    private readonly MelIConfiguration _configuration = configuration;
    private Dictionary<string, string?> _data = [with(StringComparer.OrdinalIgnoreCase)];
    private HashSet<string> _containerKeys = [with(StringComparer.OrdinalIgnoreCase)];

    public Task LoadAsync()
    {
        Dictionary<string, string?> data = [with(StringComparer.OrdinalIgnoreCase)];
        HashSet<string> containerKeys = [with(StringComparer.OrdinalIgnoreCase)];

        AddChildren(_configuration, string.Empty, data, containerKeys);

        _data = data;
        _containerKeys = containerKeys;
        return Task.CompletedTask;
    }

    public bool TryGet(string key, out string? value)
    {
        _ = key ?? throw new ArgumentNullException(nameof(key));

        return _data.TryGetValue(key, out value)
            && (value is not null || !_containerKeys.Contains(key));
    }

    public bool TryGetScalar(string key, out string? value) => TryGet(key, out value);

    public IEnumerable<string> GetChildKeys(string? parentPath)
    {
        string prefix = parentPath is null
            ? string.Empty
            : parentPath + MelConfigurationPath.KeyDelimiter;
        HashSet<string> childKeys = [with(StringComparer.OrdinalIgnoreCase)];

        foreach (string key in _data.Keys)
        {
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string remainder = key.Substring(prefix.Length);
            if (remainder.Length == 0)
            {
                continue;
            }

            int delimiterIndex = remainder.IndexOf(MelConfigurationPath.KeyDelimiter, StringComparison.Ordinal);
            childKeys.Add(delimiterIndex < 0 ? remainder : remainder.Substring(0, delimiterIndex));
        }

        return childKeys;
    }

    private static void AddChildren(
        MelIConfiguration configuration,
        string prefix,
        Dictionary<string, string?> data,
        HashSet<string> containerKeys)
    {
        foreach (MelIConfigurationSection section in configuration.GetChildren())
        {
            string key = prefix.Length == 0
                ? section.Key
                : prefix + MelConfigurationPath.KeyDelimiter + section.Key;
            MelIConfigurationSection[] children = [.. section.GetChildren()];
            data[key] = section.Value;
            if (children.Length > 0)
            {
                containerKeys.Add(key);
                AddChildren(section, key, data, containerKeys);
            }
        }
    }
}
