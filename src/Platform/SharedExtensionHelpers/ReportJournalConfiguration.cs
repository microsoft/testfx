// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

#if !NETCOREAPP
using Polyfills;
#endif

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Recovery infrastructure is shared-source implementation detail, not package API.

internal static class ReportControllerMode
{
    [UnsupportedOSPlatformGuard("android")]
    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("ios")]
    [UnsupportedOSPlatformGuard("tvos")]
    [UnsupportedOSPlatformGuard("wasi")]
    public static bool IsSupported { get; } =
        !OperatingSystem.IsAndroid()
        && !OperatingSystem.IsBrowser()
        && !OperatingSystem.IsIOS()
        && !OperatingSystem.IsTvOS()
        && !OperatingSystem.IsWasi();
}

internal sealed class ReportJournalConfiguration(string environmentVariableName)
{
    private string? _path;

    public string EnvironmentVariableName { get; } = environmentVariableName;

    public string GetOrCreatePath(IConfiguration configuration, IFileSystem fileSystem)
    {
        if (_path is null)
        {
            string directory = fileSystem.CreateDirectory(configuration.GetTestResultDirectory());
            _path = Path.Combine(directory, $"report-recovery-{Guid.NewGuid():N}.jsonl");
        }

        return _path;
    }
}

internal sealed class ReportJournalEnvironmentVariableProvider(
    ICommandLineOptions commandLineOptions,
    IConfiguration configuration,
    IFileSystem fileSystem,
    string optionName,
    ReportJournalConfiguration journal) : ITestHostEnvironmentVariableProvider
{
    public string Uid => $"{nameof(ReportJournalEnvironmentVariableProvider)}.{journal.EnvironmentVariableName}";

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => Uid;

    public string Description => Uid;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(commandLineOptions.IsOptionSet(optionName) && ReportControllerMode.IsSupported);

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        string path = journal.GetOrCreatePath(configuration, fileSystem);
        using (fileSystem.NewFileStream(path, FileMode.Create))
        {
        }

        environmentVariables.SetVariable(new EnvironmentVariable(
            journal.EnvironmentVariableName,
            path,
            isSecret: false,
            isLocked: true));
        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
        => environmentVariables.TryGetVariable(journal.EnvironmentVariableName, out OwnedEnvironmentVariable? variable)
            && variable.Value == journal.GetOrCreatePath(configuration, fileSystem)
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask($"The report recovery environment variable '{journal.EnvironmentVariableName}' is missing or invalid.");
}

#pragma warning restore RS0051
