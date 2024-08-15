// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Configurations;

// Taken and adapted from https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Configuration.EnvironmentVariables/src/EnvironmentVariablesConfigurationProvider.cs
[ExcludeFromCodeCoverage]
internal sealed class EnvironmentVariablesConfigurationProvider : IConfigurationProvider
{
    public static readonly string KeyDelimiter = ":";
    private const string MySqlServerPrefix = "MYSQLCONNSTR_";
    private const string SqlAzureServerPrefix = "SQLAZURECONNSTR_";
    private const string SqlServerPrefix = "SQLCONNSTR_";
    private const string CustomConnectionStringPrefix = "CUSTOMCONNSTR_";

    private readonly string _prefix;
    private readonly string _normalizedPrefix;

    private readonly Dictionary<string, string?> _data = new(StringComparer.OrdinalIgnoreCase);

    private readonly IEnvironment _environmentVariables;

    public EnvironmentVariablesConfigurationProvider(IEnvironment environmentVariables)
        : this(environmentVariables, string.Empty)
    {
    }

    public EnvironmentVariablesConfigurationProvider(IEnvironment environmentVariables, string prefix)
    {
        _prefix = prefix;
        _normalizedPrefix = Normalize(_prefix);
        _environmentVariables = environmentVariables;
    }

    public Task LoadAsync()
    {
        IDictionaryEnumerator e = _environmentVariables.GetEnvironmentVariables().GetEnumerator();
        try
        {
            while (e.MoveNext())
            {
                string key = (string)e.Entry.Key;
                string? value = (string?)e.Entry.Value;

                if (key.StartsWith(MySqlServerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    HandleMatchedConnectionStringPrefix(_data, MySqlServerPrefix, "MySql.Data.MySqlClient", key, value);
                }
                else if (key.StartsWith(SqlAzureServerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    HandleMatchedConnectionStringPrefix(_data, SqlAzureServerPrefix, "System.Data.SqlClient", key, value);
                }
                else if (key.StartsWith(SqlServerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    HandleMatchedConnectionStringPrefix(_data, SqlServerPrefix, "System.Data.SqlClient", key, value);
                }
                else if (key.StartsWith(CustomConnectionStringPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    HandleMatchedConnectionStringPrefix(_data, CustomConnectionStringPrefix, null, key, value);
                }
                else
                {
                    AddIfNormalizedKeyMatchesPrefix(_data, Normalize(key), value);
                }
            }
        }
        finally
        {
            (e as IDisposable)?.Dispose();
        }

        return Task.CompletedTask;
    }

    public bool TryGet(string key, out string? value)
    {
        Guard.NotNullOrEmpty(key, nameof(key));
        return _data.TryGetValue(key, out value);
    }

    private void HandleMatchedConnectionStringPrefix(Dictionary<string, string?> data, string connectionStringPrefix, string? provider, string fullKey, string? value)
    {
        string normalizedKeyWithoutConnectionStringPrefix = Normalize(fullKey[connectionStringPrefix.Length..]);

        // Add the key-value pair for connection string, and optionally provider name
        AddIfNormalizedKeyMatchesPrefix(data, $"ConnectionStrings:{normalizedKeyWithoutConnectionStringPrefix}", value);
        if (provider != null)
        {
            AddIfNormalizedKeyMatchesPrefix(data, $"ConnectionStrings:{normalizedKeyWithoutConnectionStringPrefix}_ProviderName", provider);
        }
    }

    private void AddIfNormalizedKeyMatchesPrefix(Dictionary<string, string?> data, string normalizedKey, string? value)
    {
        if (normalizedKey.StartsWith(_normalizedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            data[normalizedKey[_normalizedPrefix.Length..]] = value;
        }
    }

    private static string Normalize(string key) => key.Replace("__", PlatformConfigurationConstants.KeyDelimiter);
}
