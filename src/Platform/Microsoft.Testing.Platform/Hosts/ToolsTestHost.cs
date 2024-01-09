// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed class ToolsTestHost(
    ToolsInformation toolsInformation,
    ServiceProvider serviceProvider,
    CommandLineParseResult parseResult,
    ICommandLineOptionsProvider[] extensionsCommandLineOptionsProviders,
    ICommandLineHandler commandLineHandler,
    IPlatformOutputDevice platformOutputDevice)
    : ITestHost, IOutputDeviceDataProducer
{
    private readonly ToolsInformation _toolsInformation = toolsInformation;
    private readonly ServiceProvider _serviceProvider = serviceProvider;
    private readonly CommandLineParseResult _parseResult = parseResult;
    private readonly ICommandLineOptionsProvider[] _extensionsCommandLineOptionsProviders = extensionsCommandLineOptionsProviders;
    private readonly ICommandLineHandler _commandLineHandler = commandLineHandler;
    private readonly IPlatformOutputDevice _platformOutputDevice = platformOutputDevice;

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
        IConsole console = _serviceProvider.GetConsole();

        if (_parseResult.ToolName is null)
        {
            throw new InvalidOperationException("Tool name is null.");
        }

        string toolNameToRun = _parseResult.ToolName;

        // TODO: Apply the override or do not support it for Tools?
        // TODO: Verify reserved tool names?
        _toolsInformation.Tools.GroupBy(x => x.Name).Where(x => x.Count() > 1).ToList()
            .ForEach(x => throw new InvalidOperationException($"Tool '{x.Key}' is registered more than once."));

        foreach (ITool tool in _toolsInformation.Tools)
        {
            if (tool.Name == toolNameToRun)
            {
                if (UnknownOptions(out string? unknownOptionsError, tool))
                {
                    await _platformOutputDevice.DisplayAsync(this, FormattedTextOutputDeviceDataHelper.CreateRedConsoleColorText(unknownOptionsError));
                    console.WriteLine();
                    return ExitCodes.InvalidCommandLine;
                }

                if (ExtensionArgumentArityAreInvalid(out string? arityErrors, tool))
                {
                    await _platformOutputDevice.DisplayAsync(this, FormattedTextOutputDeviceDataHelper.CreateRedConsoleColorText(arityErrors));
                    return ExitCodes.InvalidCommandLine;
                }

                if (InvalidOptionsArguments(out string? invalidOptionsArguments, tool))
                {
                    await _platformOutputDevice.DisplayAsync(this, FormattedTextOutputDeviceDataHelper.CreateRedConsoleColorText(invalidOptionsArguments));
                    return ExitCodes.InvalidCommandLine;
                }

                return await tool.RunAsync();
            }
        }

        await _platformOutputDevice.DisplayAsync(this, FormattedTextOutputDeviceDataHelper.CreateRedConsoleColorText($"Tool '{toolNameToRun}' not found in the list of registered tools."));
        await _commandLineHandler.PrintHelpAsync();
        return ExitCodes.InvalidCommandLine;
    }

    private bool UnknownOptions([NotNullWhen(true)] out string? error, ITool tool)
    {
        error = null;

        // This is unexpected
        if (_parseResult is null)
        {
            return false;
        }

        StringBuilder stringBuilder = new();
        foreach (OptionRecord optionRecord in _parseResult.Options)
        {
            if (!GetAllCommandLineOptionsProviderByOptionName(optionRecord.Option).Any())
            {
                stringBuilder.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"Unknown option '{optionRecord.Option}' for tool '{tool.DisplayName}'.");
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

        // This is unexpected
        if (_parseResult is null)
        {
            return false;
        }

        StringBuilder stringBuilder = new();
        foreach (IGrouping<string, OptionRecord> optionRecord in _parseResult.Options.GroupBy(x => x.Option))
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

        foreach (ICommandLineOptionsProvider extension in _extensionsCommandLineOptionsProviders)
        {
            foreach (CommandLineOption option in extension.GetCommandLineOptions())
            {
                if (_parseResult.Options.Count(x => x.Option == option.Name) < option.Arity.Min)
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

    private bool InvalidOptionsArguments([NotNullWhen(true)] out string? error, ITool tool)
    {
        error = null;

        // This is unexpected
        if (_parseResult is null)
        {
            return false;
        }

        StringBuilder stringBuilder = new();
        foreach (OptionRecord optionRecord in _parseResult.Options)
        {
            ICommandLineOptionsProvider extension = GetAllCommandLineOptionsProviderByOptionName(optionRecord.Option).Single();
            if (!extension.OptionArgumentsAreValid(extension.GetCommandLineOptions().Single(x => x.Name == optionRecord.Option), optionRecord.Arguments, out string? argumentsError))
            {
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"Invalid arguments for option '--{optionRecord.Option}': {argumentsError}, tool {tool.DisplayName}");
            }
        }

        if (stringBuilder.Length > 0)
        {
            error = stringBuilder.ToString();
            return true;
        }

        return false;
    }

    private IEnumerable<ICommandLineOptionsProvider> GetAllCommandLineOptionsProviderByOptionName(string optionName)
    {
        foreach (ICommandLineOptionsProvider commandLineOptionsProvider in _extensionsCommandLineOptionsProviders)
        {
            if (commandLineOptionsProvider.GetCommandLineOptions().Any(option => option.Name == optionName))
            {
                yield return commandLineOptionsProvider;
            }
        }
    }
}
