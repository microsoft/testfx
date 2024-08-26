// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform;

internal sealed class DotnetTestConnection :
#if NETCOREAPP
    IAsyncDisposable,
#endif
    IDisposable
{
    private readonly CommandLineHandler _commandLineHandler;
    private readonly IProcessHandler _processHandler;
    private readonly IEnvironment _environment;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ITestApplicationCancellationTokenSource _cancellationTokenSource;

    private NamedPipeClient? _dotnetTestPipeClient;

    public DotnetTestConnection(CommandLineHandler commandLineHandler, IProcessHandler processHandler, IEnvironment environment, ITestApplicationModuleInfo testApplicationModuleInfo, ITestApplicationCancellationTokenSource cancellationTokenSource)
    {
        _commandLineHandler = commandLineHandler;
        _processHandler = processHandler;
        _environment = environment;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _cancellationTokenSource = cancellationTokenSource;
    }

    public async Task<bool> TryConnectToDotnetTestPipeIfAvailableAsync()
    {
        // If we are in server mode and the pipe name is provided
        // then, we need to connect to the pipe server.
        if (_commandLineHandler.HasDotnetTestServerOption() &&
            _commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.DotNetTestPipeOptionKey, out string[]? arguments))
        {
            // The execution id is used to identify the test execution
            // We are storing it as an env var so that it can be read by the test host, test host controller and the test host orchestrator
            // If it already exists, we don't overwrite it
            if (RoslynString.IsNullOrEmpty(_environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID)))
            {
                _environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID, Guid.NewGuid().ToString("N"));
            }

            _dotnetTestPipeClient = new(arguments[0]);
            _dotnetTestPipeClient.RegisterAllSerializers();

            await _dotnetTestPipeClient.ConnectAsync(_cancellationTokenSource.CancellationToken);

            return _dotnetTestPipeClient.IsConnected;
        }

        return false;
    }

    public async Task SendCommandLineOptionsToDotnetTestPipeAsync()
    {
        RoslynDebug.Assert(_dotnetTestPipeClient is not null);

        List<CommandLineOptionMessage> commandLineHelpOptions = new();
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
                    commandLineOption.IsBuiltIn));
            }
        }

        await _dotnetTestPipeClient.RequestReplyAsync<CommandLineOptionMessages, VoidResponse>(new CommandLineOptionMessages(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath(), commandLineHelpOptions.OrderBy(option => option.Name).ToArray()), _cancellationTokenSource.CancellationToken);
    }

    public async Task<bool> DoHandshakeAsync(string hostType)
    {
        RoslynDebug.Assert(_dotnetTestPipeClient is not null);

        string supportedProtocolVersions = ProtocolConstants.Version;
        HandshakeInfo handshakeInfo = new(new Dictionary<byte, string>()
        {
            { HandshakeInfoPropertyNames.PID, _processHandler.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) },
            { HandshakeInfoPropertyNames.Architecture, RuntimeInformation.OSArchitecture.ToString() },
            { HandshakeInfoPropertyNames.Framework, RuntimeInformation.FrameworkDescription },
            { HandshakeInfoPropertyNames.OS, RuntimeInformation.OSDescription },
            { HandshakeInfoPropertyNames.SupportedProtocolVersions, supportedProtocolVersions },
            { HandshakeInfoPropertyNames.HostType, hostType },
            { HandshakeInfoPropertyNames.ModulePath, _testApplicationModuleInfo?.GetCurrentTestApplicationFullPath() ?? string.Empty },
            { HandshakeInfoPropertyNames.ExecutionId,  _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID) ?? string.Empty },
        });

        HandshakeInfo response = await _dotnetTestPipeClient.RequestReplyAsync<HandshakeInfo, HandshakeInfo>(handshakeInfo, _cancellationTokenSource.CancellationToken);

        return response.Properties?.TryGetValue(HandshakeInfoPropertyNames.SupportedProtocolVersions, out string? protocolVersion) == true &&
            IsVersionCompatible(protocolVersion, supportedProtocolVersions);
    }

    public static bool IsVersionCompatible(string protocolVersion, string supportedProtocolVersions) => supportedProtocolVersions.Split(';').Contains(protocolVersion);

    public async Task SendMessageAsync(IRequest message)
    {
        RoslynDebug.Assert(_dotnetTestPipeClient is not null);

        switch (message)
        {
            case SuccessfulTestResultMessage successfulTestResultMessage:
                await _dotnetTestPipeClient.RequestReplyAsync<SuccessfulTestResultMessage, VoidResponse>(successfulTestResultMessage, _cancellationTokenSource.CancellationToken);
                break;

            case FailedTestResultMessage failedTestResultMessage:
                await _dotnetTestPipeClient.RequestReplyAsync<FailedTestResultMessage, VoidResponse>(failedTestResultMessage, _cancellationTokenSource.CancellationToken);
                break;

            case FileArtifactInfo fileArtifactInfo:
                await _dotnetTestPipeClient.RequestReplyAsync<FileArtifactInfo, VoidResponse>(fileArtifactInfo, _cancellationTokenSource.CancellationToken);
                break;

            case TestSessionEvent testSessionEvent:
                await _dotnetTestPipeClient.RequestReplyAsync<TestSessionEvent, VoidResponse>(testSessionEvent, _cancellationTokenSource.CancellationToken);
                break;
        }
    }

    public bool IsConnected => _dotnetTestPipeClient?.IsConnected ?? false;

    public void Dispose() => _dotnetTestPipeClient?.Dispose();

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (_dotnetTestPipeClient is not null)
        {
            await _dotnetTestPipeClient.DisposeAsync();
        }
    }
#endif

}
