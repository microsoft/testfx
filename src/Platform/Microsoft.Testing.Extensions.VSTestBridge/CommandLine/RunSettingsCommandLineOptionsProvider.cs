// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.VSTestBridge.CommandLine;

/// <summary>
/// A command line service provider to support VSTest .runsettings files.
/// </summary>
internal sealed class RunSettingsCommandLineOptionsProvider : ICommandLineOptionsProvider
{
    public const string RunSettingsOptionName = "settings";
    private readonly IFileSystem _fileSystem;

    public RunSettingsCommandLineOptionsProvider(IExtension extension)
        : this(extension, new SystemFileSystem())
    {
    }

    internal /* for testing purposes */ RunSettingsCommandLineOptionsProvider(IExtension extension, IFileSystem fileSystem)
    {
        Uid = extension.Uid;
        DisplayName = extension.DisplayName;
        Description = extension.Description;
        Version = extension.Version;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public string Uid { get; }

    /// <inheritdoc />
    public string Version { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        => new[] { new CommandLineOption(RunSettingsOptionName, ExtensionResources.RunSettingsOptionDescription, ArgumentArity.ExactlyOne, false) };

    /// <inheritdoc />
    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        RoslynDebug.Assert(commandOption.Name == RunSettingsOptionName);
        string filePath = arguments[0];

        if (!_fileSystem.Exists(filePath))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, ExtensionResources.RunsettingsFileDoesNotExist, filePath));
        }

        // Even if the file exists, we want to validate we can open/read it.
        if (!CanReadFile(filePath))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, ExtensionResources.RunsettingsFileCannotBeRead, filePath));
        }

        // No problem found
        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ValidationResult.ValidTask;

    private bool CanReadFile(string filePath)
    {
        try
        {
            using IFileStream stream = _fileSystem.NewFileStream(filePath, FileMode.Open, FileAccess.Read);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
