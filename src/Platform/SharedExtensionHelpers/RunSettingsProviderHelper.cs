// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Resource-independent logic shared by the VSTest bridge and the MSTest adapter's native Microsoft.Testing.Platform
/// integration for the <c>--settings</c> (.runsettings) and <c>--test-parameter</c> (TestRunParameters) providers.
/// The per-package concerns (resource strings, option registration, UWP guards) stay in the provider classes; only
/// the shared logic lives here.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "The shared helper is linked into projects that are allowed to use MTP APIs")]
internal static class RunSettingsProviderHelper
{
    /// <summary>
    /// Attempts to open the file for reading to validate it can be read (independently of its existence check).
    /// </summary>
    internal static bool CanReadFile(IFileSystem fileSystem, string filePath)
    {
        try
        {
            using IFileStream stream = fileSystem.NewFileStream(filePath, FileMode.Open, FileAccess.Read);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves and parses the .runsettings from (in order) the command-line option, the
    /// <c>TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS</c> content environment variable, or the
    /// <c>TESTINGPLATFORM_VSTESTBRIDGE_RUNSETTINGS_FILE</c> file-path environment variable. Returns the parsed
    /// <see cref="XDocument"/>, or <see langword="null"/> when no runsettings could be resolved.
    /// </summary>
    internal static async Task<XDocument?> TryLoadRunSettingsAsync(
        ICommandLineOptions commandLineOptions,
        IFileSystem fileSystem,
        IEnvironment environment,
        string runSettingsOptionName)
    {
        string? runSettingsFilePath = null;
        string? runSettingsContent = null;

        // Try to get runsettings from command line.
        if (commandLineOptions.TryGetOptionArgumentList(runSettingsOptionName, out string[]? runsettings)
            && runsettings.Length > 0
            && fileSystem.ExistFile(runsettings[0]))
        {
            runSettingsFilePath = runsettings[0];
        }

        // If not from command line, try environment variable with content.
        if (runSettingsFilePath is null)
        {
            runSettingsContent = environment.GetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS");
        }

        // If not from content env var, try environment variable with file path.
        if (runSettingsFilePath is null && string.IsNullOrEmpty(runSettingsContent))
        {
            string? envVarFilePath = environment.GetEnvironmentVariable("TESTINGPLATFORM_VSTESTBRIDGE_RUNSETTINGS_FILE");
            if (!string.IsNullOrEmpty(envVarFilePath) && fileSystem.ExistFile(envVarFilePath!))
            {
                runSettingsFilePath = envVarFilePath;
            }
        }

        // If we have a file path, read from file.
        if (runSettingsFilePath is not null)
        {
            using IFileStream fileStream = fileSystem.NewFileStream(runSettingsFilePath, FileMode.Open, FileAccess.Read);
#if NETCOREAPP
            return await XDocument.LoadAsync(fileStream.Stream, LoadOptions.None, CancellationToken.None).ConfigureAwait(false);
#else
            using StreamReader streamReader = new(fileStream.Stream);
            return XDocument.Parse(await streamReader.ReadToEndAsync().ConfigureAwait(false));
#endif
        }

        // If we have content, parse it directly.
        return !string.IsNullOrEmpty(runSettingsContent)
            ? XDocument.Parse(runSettingsContent!)
            : null;
    }

    /// <summary>
    /// Returns a value indicating whether the runsettings declares a <c>&lt;EnvironmentVariables&gt;</c> section.
    /// </summary>
    internal static bool HasEnvironmentVariables(XDocument runSettings)
        => runSettings.Element("RunSettings")?.Element("RunConfiguration")?.Element("EnvironmentVariables") is not null;

    /// <summary>
    /// Applies every <c>&lt;EnvironmentVariables&gt;</c> entry from the runsettings to the test host.
    /// </summary>
    internal static void ApplyEnvironmentVariables(XDocument runSettings, IEnvironmentVariables environmentVariables)
    {
        foreach (XElement element in runSettings.Element("RunSettings")!.Element("RunConfiguration")!.Element("EnvironmentVariables")!.Elements())
        {
            environmentVariables.SetVariable(new(element.Name.ToString(), element.Value, true, true));
        }
    }

    /// <summary>
    /// Returns the first argument that is not a <c>name=value</c> pair (i.e. does not contain <c>'='</c>), or
    /// <see langword="null"/> when all arguments are valid.
    /// </summary>
    internal static string? FindInvalidTestParameter(string[] arguments)
        => arguments.FirstOrDefault(argument => !argument.Contains('='));
}
#endif
