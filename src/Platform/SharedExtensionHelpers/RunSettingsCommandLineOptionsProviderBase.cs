// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Resource-independent base for the command-line provider that supports the VSTest <c>--settings</c> (.runsettings)
/// option. It owns the option registration and the file existence/readability validation; the concrete providers only
/// supply the localized option description and error messages. Shared by the VSTest bridge and the MSTest adapter's
/// native Microsoft.Testing.Platform integration so both surface an identical <c>--settings</c> option.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "The shared helper is linked into projects that are allowed to use MTP APIs")]
internal abstract class RunSettingsCommandLineOptionsProviderBase : CommandLineOptionsProviderBase
{
    public const string RunSettingsOptionName = "settings";

    private readonly IFileSystem _fileSystem;
    private readonly string _fileDoesNotExistErrorFormat;
    private readonly string _fileCannotBeReadErrorFormat;

    protected RunSettingsCommandLineOptionsProviderBase(
        IExtension extension,
        IFileSystem fileSystem,
        string optionDescription,
        string fileDoesNotExistErrorFormat,
        string fileCannotBeReadErrorFormat)
        : base(extension, [new CommandLineOption(RunSettingsOptionName, optionDescription, ArgumentArity.ExactlyOne, false)])
    {
        _fileSystem = fileSystem;
        _fileDoesNotExistErrorFormat = fileDoesNotExistErrorFormat;
        _fileCannotBeReadErrorFormat = fileCannotBeReadErrorFormat;
    }

    /// <inheritdoc />
    public sealed override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        string filePath = arguments[0];

        return !_fileSystem.ExistFile(filePath)
            ? ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, _fileDoesNotExistErrorFormat, filePath))
            : !RunSettingsProviderHelper.CanReadFile(_fileSystem, filePath)
                ? ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, _fileCannotBeReadErrorFormat, filePath))
                : ValidationResult.ValidTask;
    }
}
#endif
