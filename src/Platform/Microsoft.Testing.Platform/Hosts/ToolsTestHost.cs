// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed class ToolsHost(
    IReadOnlyList<ITool> toolsInformation,
    ServiceProvider serviceProvider,
    CommandLineHandler commandLineHandler,
    IOutputDevice outputDevice) : IHost, IOutputDeviceDataProducer
{
    private readonly IReadOnlyList<ITool> _toolsInformation = toolsInformation;
    private readonly ServiceProvider _serviceProvider = serviceProvider;
    private readonly CommandLineHandler _commandLineHandler = commandLineHandler;
    private readonly IOutputDevice _outputDevice = outputDevice;

    /// <inheritdoc />
    public string Uid => nameof(ToolsHost);

    /// <inheritdoc />
    public string Version => PlatformVersion.Version;

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
        IPushOnlyProtocol? protocol = _serviceProvider.GetServiceInternal<IPushOnlyProtocol>();
        string toolNameToRun = _commandLineHandler.ParseResult.ToolName
            ?? throw new InvalidOperationException("Tool name is null.");

        try
        {
            if (protocol?.IsServerMode == true
                && toolNameToRun == ArtifactPostProcessingDispatcherTool.ToolName
                && !await protocol.IsCompatibleProtocolAsync(
                    HandshakeMessageHostTypes.ArtifactPostProcessor,
                    ArtifactPostProcessingHandshakeProperties.Create(_serviceProvider.GetServicesInternal<IArtifactPostProcessor>())).ConfigureAwait(false))
            {
                return (int)ExitCode.IncompatibleProtocolVersion;
            }

            // It is unclear whether the override should be applied or simply not supported for Tools.
            // Reserved tool names may also need to be verified.
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
                        return (int)ExitCode.InvalidCommandLine;
                    }

                    if (ExtensionArgumentArityAreInvalid(out string? arityErrors, tool))
                    {
                        await _outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(arityErrors), cancellationToken).ConfigureAwait(false);
                        return (int)ExitCode.InvalidCommandLine;
                    }

                    ValidationResult optionsArgumentsValidationResult = await ValidateOptionsArgumentsAsync(tool).ConfigureAwait(false);
                    if (!optionsArgumentsValidationResult.IsValid)
                    {
                        await _outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(optionsArgumentsValidationResult.ErrorMessage), cancellationToken).ConfigureAwait(false);
                        return (int)ExitCode.InvalidCommandLine;
                    }

                    return await tool.RunAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            await _outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData($"Tool '{toolNameToRun}' not found in the list of registered tools."), cancellationToken).ConfigureAwait(false);
            await _commandLineHandler.PrintHelpAsync(_outputDevice, null, cancellationToken).ConfigureAwait(false);
            return (int)ExitCode.InvalidCommandLine;
        }
        finally
        {
            if (protocol?.IsServerMode == true)
            {
                await protocol.OnExitAsync().ConfigureAwait(false);
            }
        }
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
            if (!GetCommandLineOptionsProviders(tool).Any(provider => provider.GetCommandLineOptions().Any(option => option.Name == optionRecord.Name)))
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
            int arity = AggregateArguments(optionRecord).Length;
            ICommandLineOptionsProvider extension = GetCommandLineOptionsProviders(tool)
                .Single(provider => provider.GetCommandLineOptions().Any(option => option.Name == optionName));
            CommandLineOption option = extension.GetCommandLineOptions().Single(x => x.Name == optionName);
            if (arity < option.Arity.Min || arity > option.Arity.Max)
            {
                stringBuilder.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"The option '--{optionName}' can be specified between {option.Arity.Min} and {option.Arity.Max} times for tool '{tool.DisplayName}'.");
            }
        }

        foreach (ICommandLineOptionsProvider extension in GetToolCommandLineOptionsProviders(tool))
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
        foreach (IGrouping<string, CommandLineParseOption> optionRecords in _commandLineHandler.ParseResult.Options.GroupBy(record => record.Name))
        {
            string optionName = optionRecords.Key;
            string[] arguments = AggregateArguments(optionRecords);
            ICommandLineOptionsProvider extension = GetCommandLineOptionsProviders(tool)
                .Single(provider => provider.GetCommandLineOptions().Any(option => option.Name == optionName));
            ValidationResult result = await extension.ValidateOptionArgumentsAsync(
                extension.GetCommandLineOptions().Single(option => option.Name == optionName),
                arguments).ConfigureAwait(false);
            if (!result.IsValid)
            {
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"Invalid arguments for option '--{optionName}': {result.ErrorMessage}, tool {tool.DisplayName}");
            }
        }

        return stringBuilder.Length > 0
            ? ValidationResult.Invalid(stringBuilder.ToString())
            : ValidationResult.Valid();
    }

    private IEnumerable<ICommandLineOptionsProvider> GetCommandLineOptionsProviders(ITool tool)
        => _commandLineHandler.SystemCommandLineOptionsProviders.Concat(GetToolCommandLineOptionsProviders(tool));

    private IEnumerable<IToolCommandLineOptionsProvider> GetToolCommandLineOptionsProviders(ITool tool)
        => _commandLineHandler.ExtensionsCommandLineOptionsProviders
            .OfType<IToolCommandLineOptionsProvider>()
            .Where(provider => provider.ToolName == tool.Name);

    internal static string[] AggregateArguments(IEnumerable<CommandLineParseOption> optionRecords)
        => [.. optionRecords.SelectMany(record => record.Arguments)];
}
