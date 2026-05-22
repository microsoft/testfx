// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.TestHostControllers;

/// <summary>
/// Built-in <see cref="ITestHostEnvironmentVariableProvider"/> that reads the
/// <c>environmentVariables</c> section from the loaded <c>testconfig.json</c> file and applies the
/// entries to the test host child process.
/// </summary>
/// <remarks>
/// When the section is present and non-empty, this provider opts in to the test host controller
/// process model: the current process becomes the controller and spawns the actual test host as a
/// child process with the configured environment variables set on <c>ProcessStartInfo</c>.
/// </remarks>
[UnsupportedOSPlatform("browser")]
internal sealed class TestConfigurationEnvironmentVariableProvider : ITestHostEnvironmentVariableProvider
{
    internal const string EnvironmentVariablesSectionName = "environmentVariables";

    private readonly AggregatedConfiguration _configuration;
    private IReadOnlyList<KeyValuePair<string, string?>>? _entries;

    public TestConfigurationEnvironmentVariableProvider(IConfiguration configuration)
        => _configuration = (AggregatedConfiguration)configuration;

    public string Uid => nameof(TestConfigurationEnvironmentVariableProvider);

    public string Version => PlatformVersion.Version;

    public string DisplayName => PlatformResources.TestConfigurationEnvironmentVariableProviderDisplayName;

    public string Description => PlatformResources.TestConfigurationEnvironmentVariableProviderDescription;

    public Task<bool> IsEnabledAsync()
    {
        IReadOnlyList<KeyValuePair<string, string?>> entries = _configuration.GetTestConfigJsonSection(EnvironmentVariablesSectionName);
        if (entries.Count == 0)
        {
            // Clear any cached entries from a previous call so we never apply stale state if the
            // method is invoked more than once on the same instance.
            _entries = null;
            return Task.FromResult(false);
        }

        // Validate names upfront to surface configuration errors before the test host process is
        // launched. ProcessStartInfo.EnvironmentVariables would otherwise throw later with a
        // less actionable error.
        foreach (KeyValuePair<string, string?> entry in entries)
        {
            ValidateName(entry.Key);
        }

        _entries = entries;
        return Task.FromResult(true);
    }

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        if (_entries is null)
        {
            return Task.CompletedTask;
        }

        foreach (KeyValuePair<string, string?> entry in _entries)
        {
            // We deliberately leave the variable unlocked so other providers (e.g. runsettings
            // via the VSTest bridge, or user-registered providers) can still override the value
            // by registering after us in the test application builder.
            environmentVariables.SetVariable(new EnvironmentVariable(
                entry.Key,
                entry.Value ?? string.Empty,
                isSecret: false,
                isLocked: false));
        }

        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
        => ValidationResult.ValidTask;

    private static void ValidateName(string name)
    {
        if (RoslynString.IsNullOrEmpty(name))
        {
            throw new FormatException(PlatformResources.TestConfigurationEnvironmentVariableNameCannotBeEmptyErrorMessage);
        }

        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c is '=' or '\0')
            {
                throw new FormatException(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.TestConfigurationEnvironmentVariableInvalidNameErrorMessage,
                    name));
            }
        }
    }
}
