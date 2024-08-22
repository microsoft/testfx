// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.DotnetTest;

internal sealed class DotnetTestConnection :
#if NETCOREAPP
    IAsyncDisposable,
#endif
    IDisposable
{
    private readonly CommandLineHandler _commandLineHandler;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly CTRLPlusCCancellationTokenSource _cancellationTokenSource;

    private NamedPipeClient? _namedPipeClient;

    public DotnetTestConnection(CommandLineHandler commandLineHandler, ITestApplicationModuleInfo testApplicationModuleInfo, CTRLPlusCCancellationTokenSource cancellationTokenSource)
    {
        _commandLineHandler = commandLineHandler;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _cancellationTokenSource = cancellationTokenSource;
    }

    public async Task<(NamedPipeClient? NamedPipeClient, string? ExecutionId)> ConnectToDotnetTestPipeIfAvailableAsync()
    {
        string executionId = string.Empty;

        // If we are in server mode and the pipe name is provided
        // then, we need to connect to the pipe server.
        if (DotnetTestHelper.HasDotnetTestServerOption(_commandLineHandler) &&
            _commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.DotNetTestPipeOptionKey, out string[]? arguments))
        {
            executionId = Guid.NewGuid().ToString("N");

            _namedPipeClient = new(arguments[0]);
            _namedPipeClient.RegisterAllSerializers();

            await _namedPipeClient.ConnectAsync(_cancellationTokenSource.CancellationToken);
        }

        return (_namedPipeClient, executionId);
    }

    public async Task SendCommandLineOptionsToDotnetTestPipeAsync()
    {
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

        await _namedPipeClient!.RequestReplyAsync<CommandLineOptionMessages, VoidResponse>(new CommandLineOptionMessages(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath(), commandLineHelpOptions.OrderBy(option => option.Name).ToArray()), _cancellationTokenSource.CancellationToken);
    }

    public void Dispose() => _namedPipeClient?.Dispose();

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (_namedPipeClient is not null)
        {
            await _namedPipeClient.DisposeAsync();
        }
    }
#endif
}
