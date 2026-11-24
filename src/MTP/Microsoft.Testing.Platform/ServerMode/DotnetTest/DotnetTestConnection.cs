// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform;

internal sealed partial class DotnetTestConnection : IPushOnlyProtocol, IDisposable
{
    private readonly CommandLineHandler _commandLineHandler;
    private readonly IEnvironment _environment;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ITestApplicationCancellationTokenSource _cancellationTokenSource;
    private readonly ILogger _logger;

    private IClient? _transportClient;
    private DotnetTestTransportKind _transportKind;

    private NamedPipeClient? _serverControlPipeClient;
    private Task? _serverControlListenerTask;
    private CancellationTokenSource? _serverControlListenerCts;
    private string? _serverControlPipeName;
    private int _cancelRequested;
    private bool _isTestInProgressSupported;

    public static string InstanceId { get; } = Guid.NewGuid().ToString("N");

    string IPushOnlyProtocol.InstanceId => InstanceId;

    public DotnetTestConnection(CommandLineHandler commandLineHandler, IEnvironment environment, ITestApplicationModuleInfo testApplicationModuleInfo, ITestApplicationCancellationTokenSource cancellationTokenSource)
        : this(commandLineHandler, environment, testApplicationModuleInfo, cancellationTokenSource, new NopLogger())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DotnetTestConnection"/> class with a logger for low-noise
    /// diagnostics of the control-pipe connect/listen/cancel/exit paths.
    /// </summary>
    public DotnetTestConnection(CommandLineHandler commandLineHandler, IEnvironment environment, ITestApplicationModuleInfo testApplicationModuleInfo, ITestApplicationCancellationTokenSource cancellationTokenSource, ILogger logger)
    {
        _commandLineHandler = commandLineHandler;
        _environment = environment;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _cancellationTokenSource = cancellationTokenSource;
        _logger = logger;
    }

    public bool IsServerMode => _transportClient?.IsConnected == true;

    public Task<IPushOnlyProtocolConsumer> GetDataConsumerAsync()
        => Task.FromResult((IPushOnlyProtocolConsumer)new DotnetTestDataConsumer(this, _environment));

    public async Task AfterCommonServiceSetupAsync()
    {
        if (_commandLineHandler.HasDotnetTestServerOption()
            && _commandLineHandler.TryGetDotnetTestTransport(out DotnetTestTransportKind transport))
        {
            // The execution id is used to identify the test execution
            // We are storing it as an env var so that it can be read by the test host, test host controller and the test host orchestrator
            // If it already exists, we don't overwrite it
            if (RoslynString.IsNullOrEmpty(_environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID)))
            {
                _environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID, Guid.NewGuid().ToString("N"));
            }

            _transportKind = transport;
            _transportClient = transport switch
            {
                DotnetTestTransportKind.Http => CreateHttpClient(),
                DotnetTestTransportKind.NamedPipe when !OperatingSystem.IsBrowser() && !OperatingSystem.IsWasi() => CreateNamedPipeClient(),
                DotnetTestTransportKind.NamedPipe => throw new PlatformNotSupportedException("The dotnet test named-pipe transport is not supported on WebAssembly."),
                _ => throw ApplicationStateGuard.Unreachable(),
            };

            ((NamedPipeBase)_transportClient).RegisterAllSerializers();

            await _transportClient.ConnectAsync(_cancellationTokenSource.CancellationToken).ConfigureAwait(false);
        }
    }

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("wasi")]
    private NamedPipeClient CreateNamedPipeClient()
    {
        ApplicationStateGuard.Ensure(_commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.DotNetTestPipeOptionKey, out string[]? arguments));
        return new NamedPipeClient(arguments[0], _environment);
    }

    private DotnetTestHttpClient CreateHttpClient()
    {
        ApplicationStateGuard.Ensure(_commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.DotNetTestHttpEndpointOptionKey, out string[]? endpointArguments));
        ApplicationStateGuard.Ensure(_commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.DotNetTestHttpTokenOptionKey, out string[]? tokenArguments));
        return new DotnetTestHttpClient(new Uri(endpointArguments[0], UriKind.Absolute), tokenArguments[0]);
    }

    public async Task HelpInvokedAsync()
    {
        IClient transportClient = _transportClient
            ?? throw new InvalidOperationException("The dotnet test transport client is not connected.");

        List<CommandLineOptionMessage> commandLineHelpOptions = [];
        foreach (ICommandLineOptionsProvider commandLineOptionProvider in _commandLineHandler.CommandLineOptionsProviders)
        {
            if (commandLineOptionProvider is IToolCommandLineOptionsProvider)
            {
                continue;
            }

            foreach (CommandLineOption commandLineOption in commandLineOptionProvider.GetCommandLineOptions())
            {
                commandLineHelpOptions.Add(new CommandLineOptionMessage(
                    commandLineOption.Name,
                    commandLineOption.Description,
                    commandLineOption.IsHidden,
                    commandLineOption.IsBuiltIn,
                    commandLineOptionProvider.Uid,
                    commandLineOption.Arity.Min,
                    commandLineOption.Arity.Max));
            }
        }

        await transportClient.RequestReplyAsync<CommandLineOptionMessages, VoidResponse>(new CommandLineOptionMessages(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath(), [.. commandLineHelpOptions.OrderBy(option => option.Name)]), _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
    }

    public bool IsIDE { get; private set; }

    // True once the handshake negotiated protocol version 1.2.0 or later, which is when the SDK is
    // able to receive AzureDevOpsLogMessage forwards. The host gates forwarding on this so an older
    // SDK (1.0.0/1.1.0) never receives an unknown message id.
    public bool IsLogForwardingSupported { get; private set; }

    // True once the handshake negotiated protocol version 1.3.0 or later, which is when the SDK is able to
    // receive generic DisplayMessage forwards (warning/error host diagnostics). The host gates forwarding on this
    // so an older SDK (<= 1.2.0) never receives an unknown message id.
    public bool IsDisplayMessageForwardingSupported { get; private set; }

    // True once the SDK advertised a reverse "server control" pipe name in its handshake reply. When set, the
    // test host opens a NamedPipeClient to that pipe and parks a long-poll so the SDK can push a
    // ServerControlMessage (e.g. CancelSession) at any time. The feature is gated on the presence of the handshake
    // property (a capability), not on the negotiated version string, so an older SDK simply never enables it.
    public bool IsServerControlChannelSupported { get; private set; }

    public bool IsRequiredArtifactPostProcessingSupported { get; private set; }

    public bool IsCoverageSummarySupported { get; private set; }

    public async Task<bool> IsCompatibleProtocolAsync(string hostType, IReadOnlyDictionary<byte, string>? additionalHandshakeProperties = null)
    {
        IClient transportClient = _transportClient
            ?? throw new InvalidOperationException("The dotnet test transport client is not connected.");

        string supportedProtocolVersions = ProtocolConstants.SupportedVersions;
        Dictionary<byte, string> properties = new()
        {
            { HandshakeMessagePropertyNames.PID, _environment.ProcessId.ToString(CultureInfo.InvariantCulture) },
            { HandshakeMessagePropertyNames.Architecture, RuntimeInformation.ProcessArchitecture.ToString() },
            { HandshakeMessagePropertyNames.Framework, RuntimeInformation.FrameworkDescription },
            { HandshakeMessagePropertyNames.OS, RuntimeInformation.OSDescription },
            { HandshakeMessagePropertyNames.SupportedProtocolVersions, supportedProtocolVersions },
            { HandshakeMessagePropertyNames.HostType, hostType },
            { HandshakeMessagePropertyNames.ModulePath, _testApplicationModuleInfo?.GetCurrentTestApplicationFullPath() ?? string.Empty },
            { HandshakeMessagePropertyNames.ExecutionId,  _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID) ?? string.Empty },
            { HandshakeMessagePropertyNames.InstanceId, InstanceId },
            { HandshakeMessagePropertyNames.ExecutionMode, GetExecutionMode() },
            { HandshakeMessagePropertyNames.CoverageSummaryVersion, ProtocolConstants.CoverageSummaryVersion },
        };

        if (hostType is HandshakeMessageHostTypes.TestHost or HandshakeMessageHostTypes.ServerTestHost)
        {
            properties.Add(HandshakeMessagePropertyNames.AttemptNumber, GetAttemptNumber());
        }

        if (hostType is HandshakeMessageHostTypes.TestHost or HandshakeMessageHostTypes.ServerTestHost or HandshakeMessageHostTypes.TestHostOrchestrator
            && TargetFrameworkParser.GetTargetFrameworkFromAssembly(Assembly.GetEntryAssembly()) is { } targetFramework)
        {
            properties.Add(HandshakeMessagePropertyNames.TargetFramework, targetFramework);
        }

        if (additionalHandshakeProperties is not null)
        {
            foreach (KeyValuePair<byte, string> property in additionalHandshakeProperties)
            {
                properties[property.Key] = property.Value;
            }
        }

        HandshakeMessage handshakeMessage = new(properties);

        IsCoverageSummarySupported = false;
        _isTestInProgressSupported = false;
        HandshakeMessage response = await transportClient.RequestReplyAsync<HandshakeMessage, HandshakeMessage>(handshakeMessage, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);

        IsIDE = response.Properties?.TryGetValue(HandshakeMessagePropertyNames.IsIDE, out string? isIDEValue) == true &&
            bool.TryParse(isIDEValue, out bool isIDE) &&
            isIDE;
        IsRequiredArtifactPostProcessingSupported =
            response.Properties?.TryGetValue(HandshakeMessagePropertyNames.RequiredPostProcessingSupported, out string? requiredPostProcessingValue) is true
            && bool.TryParse(requiredPostProcessingValue, out bool requiredPostProcessingSupported)
            && requiredPostProcessingSupported;

        if (_transportKind == DotnetTestTransportKind.NamedPipe
            && response.Properties?.TryGetValue(HandshakeMessagePropertyNames.ServerControlPipeName, out string? serverControlPipeName) is true &&
            !RoslynString.IsNullOrEmpty(serverControlPipeName))
        {
            _serverControlPipeName = serverControlPipeName;
            IsServerControlChannelSupported = true;
        }

        if (response.Properties?.TryGetValue(HandshakeMessagePropertyNames.SupportedProtocolVersions, out string? protocolVersion) is true)
        {
            bool isCompatible = IsVersionCompatible(protocolVersion, supportedProtocolVersions);
            bool versionParsed = Version.TryParse(protocolVersion, out Version? negotiatedVersion);
            IsLogForwardingSupported = isCompatible && versionParsed && negotiatedVersion >= new Version(1, 2, 0);
            IsDisplayMessageForwardingSupported = isCompatible && versionParsed && negotiatedVersion >= new Version(1, 3, 0);
            IsCoverageSummarySupported = isCompatible
                && properties.TryGetValue(HandshakeMessagePropertyNames.CoverageSummaryVersion, out string? advertisedCoverageVersion)
                && advertisedCoverageVersion == ProtocolConstants.CoverageSummaryVersion
                && response.Properties.TryGetValue(HandshakeMessagePropertyNames.CoverageSummaryVersion, out string? coverageVersion)
                && coverageVersion == ProtocolConstants.CoverageSummaryVersion;
            _isTestInProgressSupported = isCompatible
                && response.Properties.TryGetValue(HandshakeMessagePropertyNames.TestInProgressSupported, out string? progressSupported)
                && bool.TryParse(progressSupported, out bool supportsProgress)
                && supportsProgress;
            return isCompatible;
        }

        return false;
    }

    private string GetExecutionMode()
        => _commandLineHandler.ParseResult.HasTool
            ? HandshakeMessageExecutionModes.Tool
            : _commandLineHandler.IsHelpInvoked()
            ? HandshakeMessageExecutionModes.Help
            : _commandLineHandler.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
                ? HandshakeMessageExecutionModes.Discover
                : HandshakeMessageExecutionModes.Run;

    private string GetAttemptNumber()
    {
        string? value = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER);
        return RoslynString.IsNullOrEmpty(value)
            ? "1"
            : int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int attemptNumber) && attemptNumber >= 1
                ? attemptNumber.ToString(CultureInfo.InvariantCulture)
                : throw new InvalidOperationException($"Environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER}' must contain a positive integer.");
    }

    public static bool IsVersionCompatible(string protocolVersion, string supportedProtocolVersions) => supportedProtocolVersions.Split(';').Contains(protocolVersion);
}
