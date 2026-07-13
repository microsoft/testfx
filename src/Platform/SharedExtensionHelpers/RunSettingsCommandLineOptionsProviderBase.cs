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

    /// <summary>
    /// Gets the localized error surfaced when a browser/WebAssembly run supplies runsettings that declare an
    /// <c>&lt;EnvironmentVariables&gt;</c> section. Concrete providers supply it from their own resources so the
    /// diagnostic is localizable.
    /// </summary>
    protected abstract string EnvironmentVariablesNotSupportedOnBrowserError { get; }

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

    /// <inheritdoc />
    public sealed override async Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        // On browser/WebAssembly, applying a runsettings <EnvironmentVariables> section requires relaunching the
        // test host with those variables set — a test-host-controller feature that needs a process restart, which
        // the browser sandbox does not support (so the environment-variable provider is not registered there).
        // Rather than silently ignoring the section (which would run the tests with different semantics than
        // requested), fail with a clear unsupported-platform diagnostic. This validation runs regardless of how the
        // runsettings were supplied — the --settings option OR the TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS /
        // TESTINGPLATFORM_VSTESTBRIDGE_RUNSETTINGS_FILE environment variables — because TryLoadRunSettingsAsync
        // resolves all three sources. Guarded to NETCOREAPP because browser-wasm only runs there (and
        // OperatingSystem.IsBrowser is a built-in since .NET 8, so no polyfill is needed). See
        // https://github.com/microsoft/testfx/issues/2196.
#if NETCOREAPP
        if (OperatingSystem.IsBrowser())
        {
            XDocument? runSettings = await RunSettingsProviderHelper.TryLoadRunSettingsAsync(
                commandLineOptions, _fileSystem, new SystemEnvironment(), RunSettingsOptionName).ConfigureAwait(false);
            if (runSettings is not null && RunSettingsProviderHelper.HasEnvironmentVariables(runSettings))
            {
                return ValidationResult.Invalid(EnvironmentVariablesNotSupportedOnBrowserError);
            }
        }
#else
        await Task.CompletedTask.ConfigureAwait(false);
#endif

        return ValidationResult.Valid();
    }
}
#endif
