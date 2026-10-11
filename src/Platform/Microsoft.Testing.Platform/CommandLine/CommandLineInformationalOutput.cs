// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

[UnsupportedOSPlatform("browser")]
internal sealed class CommandLineInformationalOutput : IAsyncDisposable
{
    private static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(10);

    private readonly string _fileName;
    private readonly bool _isHelp;
    private readonly List<TestNode>? _jsonTests;
    private readonly TextWriter _output;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly NamedPipeServer _server;
    private readonly Task _connection;
    private bool _handshakeReceived;
    private bool _helpReceived;
    private string? _sessionUid;
    private bool _sessionCompleted;
    private int _discoveredCount;
    private bool _invalidResponse;

    public CommandLineInformationalOutput(string fileName, string[] arguments, TextWriter output)
    {
        _fileName = fileName;
        _output = output;
        CommandLineParseResult parseResult = CommandLineParser.Parse(arguments, new SystemEnvironment());
        _isHelp = parseResult.IsOptionSet("help") || parseResult.IsOptionSet("?");
        if (!_isHelp && parseResult.TryGetOptionArgumentList("list-tests", out string[]? discoveryArguments)
            && discoveryArguments is [string format] && format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            _jsonTests = [];
        }

        try
        {
            _server = new(
                $"PACKAGEDAPP_INFO_{Guid.NewGuid():N}",
                HandleRequestAsync,
                new SystemEnvironment(),
                new NopLogger(),
                new SystemTask(),
                _lifetime.Token);
            _server.RegisterAllSerializers();
            _connection = _server.WaitConnectionAsync(_lifetime.Token);
        }
        catch
        {
            _lifetime.Dispose();
            throw;
        }
    }

    public string PipeName => _server.PipeName.Name;

    public async Task CompleteAsync()
    {
        if (!_connection.IsCompletedSuccessfully
            || !await _server.WaitForDisconnectAsync(DisconnectTimeout).ConfigureAwait(false))
        {
            throw new InvalidOperationException(PlatformResources.IncompleteInformationalResponse);
        }

        ValidateCompletion();
        if (_jsonTests is not null)
        {
            await _output.WriteLineAsync(DiscoveredTestsJsonSerializer.Serialize(_jsonTests)).ConfigureAwait(false);
        }
        else if (!_isHelp)
        {
            await _output.WriteLineAsync(string.Format(
                CultureInfo.CurrentCulture, PlatformResources.DiscoveredTestsInAssembly, _discoveredCount)).ConfigureAwait(false);
        }
    }

    internal void ValidateCompletion()
    {
        if (_invalidResponse || !_handshakeReceived || (_isHelp ? !_helpReceived : !_sessionCompleted))
        {
            throw new InvalidOperationException(PlatformResources.IncompleteInformationalResponse);
        }
    }

    private async Task<IResponse> HandleRequestAsync(IRequest request)
    {
        if (request is HandshakeMessage handshake)
        {
            return AcceptHandshake(handshake);
        }

        if (!_handshakeReceived)
        {
            _invalidResponse = true;
            return VoidResponse.CachedInstance;
        }

        switch (request)
        {
            case CommandLineOptionMessages options when options.CommandLineOptionMessageList is { } commandLineOptions:
                await WriteHelpAsync(commandLineOptions).ConfigureAwait(false);
                break;

            case TestSessionEvent session:
                ObserveSession(session);
                break;

            case DiscoveredTestMessages discovered:
                await WriteDiscoveredTestsAsync(discovered).ConfigureAwait(false);
                break;

            case DisplayMessage display:
                await _output.WriteLineAsync(display.Text).ConfigureAwait(false);
                break;

            default:
                _invalidResponse = true;
                break;
        }

        return VoidResponse.CachedInstance;
    }

    internal HandshakeMessage AcceptHandshake(HandshakeMessage handshake)
    {
        string version = string.Empty;
        if (!_handshakeReceived
            && handshake.Properties is { } properties
            && properties.TryGetValue(HandshakeMessagePropertyNames.SupportedProtocolVersions, out string? supportedVersions)
            && properties.TryGetValue(HandshakeMessagePropertyNames.ExecutionMode, out string? mode)
            && mode == (_isHelp ? HandshakeMessageExecutionModes.Help : HandshakeMessageExecutionModes.Discover))
        {
            string[] clientVersions = supportedVersions.Split(';');
            version = ProtocolConstants.SupportedVersions.Split(';')
                .LastOrDefault(candidate => clientVersions.Contains(candidate, StringComparer.Ordinal)) ?? string.Empty;
        }

        _handshakeReceived = version.Length > 0;
        _invalidResponse |= !_handshakeReceived;
        return new(new()
        {
            [HandshakeMessagePropertyNames.SupportedProtocolVersions] = version,
            [HandshakeMessagePropertyNames.IsIDE] = (_jsonTests is not null).ToString(),
        });
    }

    internal void ObserveSession(TestSessionEvent session)
    {
        if (_handshakeReceived && !_isHelp && !_sessionCompleted)
        {
            if (session is { SessionType: SessionEventTypes.TestSessionStart, SessionUid: { } uid } && _sessionUid is null)
            {
                _sessionUid = uid;
                return;
            }

            if (session.SessionType == SessionEventTypes.TestSessionEnd && _sessionUid is not null && session.SessionUid == _sessionUid)
            {
                _sessionCompleted = true;
                return;
            }
        }

        _invalidResponse = true;
    }

    internal async Task WriteDiscoveredTestsAsync(DiscoveredTestMessages discovered)
    {
        if (!_handshakeReceived || _isHelp || _sessionUid is null || _sessionCompleted)
        {
            _invalidResponse = true;
            return;
        }

        foreach (DiscoveredTestMessage test in discovered.DiscoveredMessages)
        {
            if (_jsonTests is not null)
            {
                if (test.Uid is null || test.DisplayName is null)
                {
                    _invalidResponse = true;
                    continue;
                }

                TestNode node = new() { Uid = test.Uid, DisplayName = test.DisplayName };
                foreach (TraitMessage trait in test.Traits)
                {
                    node.Properties.Add(new TestMetadataProperty(trait.Key, trait.Value));
                }

                _jsonTests.Add(node);
            }
            else
            {
                await _output.WriteLineAsync($"  {test.DisplayName}").ConfigureAwait(false);
            }

            _discoveredCount++;
        }
    }

    internal async Task WriteHelpAsync(CommandLineOptionMessage[] options)
    {
        if (!_handshakeReceived || !_isHelp || _helpReceived)
        {
            _invalidResponse = true;
            return;
        }

        _helpReceived = true;
        await _output.WriteLineAsync(string.Format(
            CultureInfo.CurrentCulture, PlatformResources.HelpApplicationUsage, Path.GetFileName(_fileName))).ConfigureAwait(false);
        await _output.WriteLineAsync().ConfigureAwait(false);
        await _output.WriteLineAsync(PlatformResources.HelpExecuteTestApplication).ConfigureAwait(false);
        await _output.WriteLineAsync().ConfigureAwait(false);
        await _output.WriteLineAsync(PlatformResources.HelpOptions).ConfigureAwait(false);
        await WriteOptionsAsync(options, builtIn: true).ConfigureAwait(false);
        await _output.WriteLineAsync().ConfigureAwait(false);
        await _output.WriteLineAsync(PlatformResources.HelpExtensionOptions).ConfigureAwait(false);
        if (!await WriteOptionsAsync(options, builtIn: false).ConfigureAwait(false))
        {
            await _output.WriteLineAsync(PlatformResources.HelpNoExtensionRegistered).ConfigureAwait(false);
        }

        await _output.WriteLineAsync().ConfigureAwait(false);
    }

    private async Task<bool> WriteOptionsAsync(CommandLineOptionMessage[] options, bool builtIn)
    {
        bool written = false;
        foreach (CommandLineOptionMessage option in options
            .Where(option => option.IsHidden is not true && (option.IsBuiltIn is true) == builtIn)
            .OrderBy(option => option.Name, StringComparer.Ordinal))
        {
            written = true;
            await _output.WriteLineAsync($"    --{option.Name}").ConfigureAwait(false);
            foreach (string line in (option.Description ?? string.Empty).ReplaceLineEndings("\n").Split('\n'))
            {
                await _output.WriteLineAsync($"        {line}").ConfigureAwait(false);
            }

            await _output.WriteLineAsync().ConfigureAwait(false);
        }

        return written;
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        try
        {
            await _connection.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            try
            {
                await _server.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                _lifetime.Dispose();
            }
        }
    }
}

#endif
