// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
internal sealed class RunSettingsCommandLineOptionsProvider : CommandLineOptionsProviderBase
{
    public const string RunSettingsOptionName = "settings";
    private readonly IFileSystem _fileSystem;

    public RunSettingsCommandLineOptionsProvider(IExtension extension)
        : this(extension, new SystemFileSystem())
    {
    }

    internal /* for testing purposes */ RunSettingsCommandLineOptionsProvider(IExtension extension, IFileSystem fileSystem)
        : base(extension, [new CommandLineOption(RunSettingsOptionName, ExtensionResources.RunSettingsOptionDescription, ArgumentArity.ExactlyOne, false)])
    {
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        RoslynDebug.Assert(commandOption.Name == RunSettingsOptionName);
        string filePath = arguments[0];

        if (!_fileSystem.ExistFile(filePath))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, ExtensionResources.RunsettingsFileDoesNotExist, filePath));
        }

        // Even if the file exists, we want to validate we can open/read it.
        if (!RunSettingsProviderHelper.CanReadFile(_fileSystem, filePath))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, ExtensionResources.RunsettingsFileCannotBeRead, filePath));
        }

        // No problem found
        return ValidationResult.ValidTask;
    }
}
