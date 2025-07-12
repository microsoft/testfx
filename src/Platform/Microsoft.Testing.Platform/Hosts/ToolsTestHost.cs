// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed class ToolsTestHost(
    IReadOnlyList<ITool> toolsInformation,
    ServiceProvider serviceProvider,
    CommandLineHandler commandLineHandler,
    IOutputDevice outputDevice) : ITestHost, IOutputDeviceDataProducer
{
    private readonly IReadOnlyList<ITool> _toolsInformation = toolsInformation;
    private readonly ServiceProvider _serviceProvider = serviceProvider;
    private readonly CommandLineHandler _commandLineHandler = commandLineHandler;
    private readonly IOutputDevice _outputDevice = outputDevice;

    /// <inheritdoc />
    public string Uid => nameof(ToolsTestHost);

    /// <inheritdoc />
    public string Version => AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName => string.Empty;

    /// <inheritdoc />
    public string Description => string.Empty;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task<int> RunAsync()
    {
        CancellationToken cancellationToken = _serviceProvider.GetTestApplicationCancellationTokenSource().CancellationToken;
        IConsole console = _serviceProvider.GetConsole();

        if (_commandLineHandler.ParseResult.ToolName is null)
        {
            throw new InvalidOperationException("Tool name is null.");
        }

        string toolNameToRun = _commandLineHandler.ParseResult.ToolName;

        // TODO: Apply the override or do not support it for Tools?
        // TODO: Verify reserved tool names?
        _toolsInformation.GroupBy(x => x.Name).Where(x => x.Count() > 1).ToList()
            .ForEach(x => throw new InvalidOperationException($"Tool '{x.Key}' is registered more than once."));

        foreach (ITool tool in _toolsInformation)
        {
            if (tool.Name == toolNameToRun)
            {
                if (UnknownOptions(out string? unknownOptionsError, tool))
                {
                    await _outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(unknownOptionsError), cancellationToken).ConfigureAwait(false);
                    console.WriteLine();
                    return ExitCodes.InvalidCommandLine;
                }

                if (ExtensionArgumentArityAreInvalid(out string? arityErrors, tool))
                {
                    await _outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(arityErrors), cancellationToken).ConfigureAwait(false);
                    return ExitCodes.InvalidCommandLine;
                }

                ValidationResult optionsArgumentsValidationResult = await ValidateOptionsArgumentsAsync(tool).ConfigureAwait(false);
                if (!optionsArgumentsValidationResult.IsValid)
                {
                    await _outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(optionsArgumentsValidationResult.ErrorMessage), cancellationToken).ConfigureAwait(false);
                    return ExitCodes.InvalidCommandLine;
                }

                return await tool.RunAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await _outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData($"Tool '{toolNameToRun}' not found in the list of registered tools."), cancellationToken).ConfigureAwait(false);
        await _commandLineHandler.PrintHelpAsync(_outputDevice, null, cancellationToken).ConfigureAwait(false);
        return ExitCodes.InvalidCommandLine;
    }

    private bool UnknownOptions([NotNullWhen(true)] out string? error, ITool tool)
    {
        error = null;

        // This is unexpected
        if (_commandLineHandler.ParseResult is null)
        {
            return false;
        }

        StringBuilder stringBuilder = new();
        foreach (CommandLineParseOption optionRecord in _commandLineHandler.ParseResult.Options)
        {
            if (!GetAllCommandLineOptionsProviderByOptionName(optionRecord.Name).Any())
            {
                stringBuilder.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"Unknown option '{optionRecord.Name}' for tool '{tool.DisplayName}'.");
            }
        }

        if (stringBuilder.Length > 0)
        {
            error = stringBuilder.ToString();
            return true;
        }

        return false;
    }

    private bool ExtensionArgumentArityAreInvalid([NotNullWhen(true)] out string? error, ITool tool)
    {
        error = null;

        StringBuilder stringBuilder = new();
        foreach (IGrouping<string, CommandLineParseOption> optionRecord in _commandLineHandler.ParseResult.Options.GroupBy(x => x.Name))
        {
            string optionName = optionRecord.Key;
            int arity = optionRecord.Count();
            ICommandLineOptionsProvider extension = GetAllCommandLineOptionsProviderByOptionName(optionName).Single();
            CommandLineOption option = extension.GetCommandLineOptions().Single(x => x.Name == optionName);
            if (arity < option.Arity.Min || arity > option.Arity.Max)
            {
                stringBuilder.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"The option '--{optionName}' can be specified between {option.Arity.Min} and {option.Arity.Max} times for tool '{tool.DisplayName}'.");
            }
        }

        foreach (ICommandLineOptionsProvider extension in _commandLineHandler.ExtensionsCommandLineOptionsProviders)
        {
            foreach (CommandLineOption option in extension.GetCommandLineOptions())
            {
                if (_commandLineHandler.ParseResult.Options.Count(x => x.Name == option.Name) < option.Arity.Min)
                {
                    stringBuilder.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"The option '--{option.Name}' must be specified at least {option.Arity.Min} times for tool '{extension.DisplayName}'.");
                }
            }
        }

        if (stringBuilder.Length > 0)
        {
            error = stringBuilder.ToString();
            return true;
        }

        return false;
    }

    private async Task<ValidationResult> ValidateOptionsArgumentsAsync(ITool tool)
    {
        StringBuilder stringBuilder = new();
        foreach (CommandLineParseOption optionRecord in _commandLineHandler.ParseResult.Options)
        {
            ICommandLineOptionsProvider extension = GetAllCommandLineOptionsProviderByOptionName(optionRecord.Name).Single();
            ValidationResult result = await extension.ValidateOptionArgumentsAsync(extension.GetCommandLineOptions().Single(x => x.Name == optionRecord.Name), optionRecord.Arguments).ConfigureAwait(false);
            if (!result.IsValid)
            {
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"Invalid arguments for option '--{optionRecord.Name}': {result.ErrorMessage}, tool {tool.DisplayName}");
            }
        }

        return stringBuilder.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToString())
            : ValidationResult.Valid();
    }

    private IEnumerable<ICommandLineOptionsProvider> GetAllCommandLineOptionsProviderByOptionName(string optionName)
    {
        foreach (ICommandLineOptionsProvider commandLineOptionsProvider in _commandLineHandler.ExtensionsCommandLineOptionsProviders)
        {
            if (commandLineOptionsProvider.GetCommandLineOptions().Any(option => option.Name == optionName))
            {
                yield return commandLineOptionsProvider;
            }
        }
    }
}
