// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class PlatformCommandLineProvider : CommandLineOptionsProviderBase
{
    public const string HelpOptionKey = "help";
    public const string HelpOptionQuestionMark = "?";
    public const string TimeoutOptionKey = "timeout";
    public const string InfoOptionKey = "info";
    public const string DiagnosticOptionKey = "diagnostic";
    public const string DiagnosticOutputFilePrefixOptionKey = "diagnostic-file-prefix";
    public const string DiagnosticOutputDirectoryOptionKey = "diagnostic-output-directory";
    public const string DiagnosticVerbosityOptionKey = "diagnostic-verbosity";
    public const string DiagnosticFileLoggerSynchronousWriteOptionKey = "diagnostic-synchronous-write";
    public const string NoBannerOptionKey = "no-banner";
    public const string SkipBuildersNumberCheckOptionKey = "internal-testingplatform-skipbuildercheck";
    public const string DiscoverTestsOptionKey = "list-tests";
    public const string DiscoverTestsJsonArgument = "json";
    public const string DiscoverTestsTextArgument = "text";

    private static readonly string SupportedDiscoverTestsValues = $"'{DiscoverTestsTextArgument}', '{DiscoverTestsJsonArgument}'";
    private static readonly string SupportedServerProtocolValues = $"'{JsonRpcProtocolName}', '{DotnetTestCliProtocolName}'";
    private static readonly string SupportedDotNetTestTransportValues = $"'{DotNetTestTransportPipeArgument}', '{DotNetTestTransportWebSocketArgument}'";
    public const string ResultDirectoryOptionKey = "results-directory";
    public const string IgnoreExitCodeOptionKey = "ignore-exit-code";
    public const string MinimumExpectedTestsOptionKey = "minimum-expected-tests";
    public const string ZeroTestsPolicyOptionKey = "zero-tests-policy";
    public const string ZeroTestsPolicyStrictArgument = "strict";
    public const string ZeroTestsPolicyAllowSkippedArgument = "allow-skipped";
    public const string TestHostControllerPIDOptionKey = "internal-testhostcontroller-pid";
    public const string ExitOnProcessExitOptionKey = "exit-on-process-exit";
    public const string ConfigFileOptionKey = "config-file";
    public const string FilterUidOptionKey = "filter-uid";
    public const string DebugAttachOptionKey = "debug";

    public const string ServerOptionKey = "server";
    public const string ClientPortOptionKey = "client-port";
    public const string ClientHostOptionKey = "client-host";
    public const string JsonRpcProtocolName = "jsonrpc";
    public const string DotNetTestPipeOptionKey = "dotnet-test-pipe";
    public const string DotnetTestCliProtocolName = "dotnettestcli";

    // Pre-launch transport selection for the 'dotnet test' pipe protocol (a.k.a. dotnettestcli). The wire
    // protocol (message/serializer/version contract) is transport-neutral; this option only selects which
    // duplex channel carries it. 'pipe' (the default, implied when only '--dotnet-test-pipe' is given) keeps
    // the existing System.IO.Pipes behavior byte-for-byte. 'websocket' is required on runtimes that cannot use
    // named pipes (browser-wasm) and is opt-in elsewhere.
    public const string DotNetTestTransportOptionKey = "dotnet-test-transport";
    public const string DotNetTestTransportPipeArgument = "pipe";
    public const string DotNetTestTransportWebSocketArgument = "websocket";

    // WebSocket-transport-specific endpoint/auth options. Both are required together with
    // '--dotnet-test-transport websocket' and mutually exclusive with '--dotnet-test-pipe'.
    public const string DotNetTestWebSocketEndpointOptionKey = "dotnet-test-websocket-endpoint";
    public const string DotNetTestWebSocketTokenOptionKey = "dotnet-test-websocket-token";

    private static readonly string[] VerbosityOptions = ["Trace", "Debug", "Information", "Warning", "Error", "Critical"];

    private static readonly string SupportedZeroTestsPolicyValues = $"'{ZeroTestsPolicyStrictArgument}', '{ZeroTestsPolicyAllowSkippedArgument}'";

    private static readonly CommandLineOption MinimumExpectedTests = new(MinimumExpectedTestsOptionKey, PlatformResources.PlatformCommandLineMinimumExpectedTestsOptionDescription, ArgumentArity.ZeroOrOne, false, isBuiltIn: true);

    private static readonly IReadOnlyCollection<CommandLineOption> PlatformCommandLineProviderCache =
    [
        // Visible options
        new(HelpOptionKey, PlatformResources.PlatformCommandLineHelpOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
        new(InfoOptionKey, PlatformResources.PlatformCommandLineInfoOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
        new(TimeoutOptionKey, PlatformResources.PlatformCommandLineTimeoutOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(ResultDirectoryOptionKey, PlatformResources.PlatformCommandLineResultDirectoryOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(DiagnosticOptionKey, PlatformResources.PlatformCommandLineDiagnosticOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
        new(DiagnosticOutputFilePrefixOptionKey, PlatformResources.PlatformCommandLineDiagnosticOutputFilePrefixOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(DiagnosticOutputDirectoryOptionKey, PlatformResources.PlatformCommandLineDiagnosticOutputDirectoryOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(DiagnosticVerbosityOptionKey, PlatformResources.PlatformCommandLineDiagnosticVerbosityOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(DiagnosticFileLoggerSynchronousWriteOptionKey, PlatformResources.PlatformCommandLineDiagnosticFileLoggerSynchronousWriteOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
        MinimumExpectedTests,
        new(ZeroTestsPolicyOptionKey, PlatformResources.PlatformCommandLineZeroTestsPolicyOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(DiscoverTestsOptionKey, PlatformResources.PlatformCommandLineDiscoverTestsOptionDescription, ArgumentArity.ZeroOrOne, false, isBuiltIn: true),
        new(IgnoreExitCodeOptionKey, PlatformResources.PlatformCommandLineIgnoreExitCodeOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(ExitOnProcessExitOptionKey, PlatformResources.PlatformCommandLineExitOnProcessExitOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(ConfigFileOptionKey, PlatformResources.PlatformCommandLineConfigFileOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(FilterUidOptionKey, PlatformResources.PlatformCommandLineFilterUidOptionDescription, ArgumentArity.OneOrMore, false, isBuiltIn: true),
        new(DebugAttachOptionKey, PlatformResources.PlatformCommandLineDebugAttachOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),

        // Hidden options
        new(HelpOptionQuestionMark, PlatformResources.PlatformCommandLineHelpOptionDescription, ArgumentArity.Zero, true, isBuiltIn: true),
        new(ServerOptionKey, PlatformResources.PlatformCommandLineServerOptionDescription, ArgumentArity.ZeroOrOne, true, isBuiltIn: true),
        new(ClientPortOptionKey, PlatformResources.PlatformCommandLineClientPortOptionDescription, ArgumentArity.ExactlyOne, true, isBuiltIn: true),
        new(ClientHostOptionKey, PlatformResources.PlatformCommandLineClientHostOptionDescription, ArgumentArity.ExactlyOne, true, isBuiltIn: true),
        new(SkipBuildersNumberCheckOptionKey, PlatformResources.PlatformCommandLineSkipBuildersNumberCheckOptionDescription, ArgumentArity.Zero, true, isBuiltIn: true),
        new(NoBannerOptionKey, PlatformResources.PlatformCommandLineNoBannerOptionDescription, ArgumentArity.ZeroOrOne, true, isBuiltIn: true),
        new(TestHostControllerPIDOptionKey, PlatformResources.PlatformCommandLineTestHostControllerPIDOptionDescription, ArgumentArity.ZeroOrOne, true, isBuiltIn: true),
        new(DotNetTestPipeOptionKey, PlatformResources.PlatformCommandLineDotnetTestPipe, ArgumentArity.ExactlyOne, true, isBuiltIn: true),
        new(DotNetTestTransportOptionKey, PlatformResources.PlatformCommandLineDotnetTestTransportOptionDescription, ArgumentArity.ExactlyOne, true, isBuiltIn: true),
        new(DotNetTestWebSocketEndpointOptionKey, PlatformResources.PlatformCommandLineDotnetTestWebSocketEndpointOptionDescription, ArgumentArity.ExactlyOne, true, isBuiltIn: true),
        new(DotNetTestWebSocketTokenOptionKey, PlatformResources.PlatformCommandLineDotnetTestWebSocketTokenOptionDescription, ArgumentArity.ExactlyOne, true, isBuiltIn: true),
    ];

    public PlatformCommandLineProvider()
        : base(
            // Stable extension UID. Do not change: it feeds telemetry, --info output, and artifact metadata.
            "PlatformCommandLineProvider",
            PlatformVersion.Version,
            PlatformResources.PlatformCommandLineProviderDisplayName,
            PlatformResources.PlatformCommandLineProviderDescription,
            PlatformCommandLineProviderCache)
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == DiagnosticVerbosityOptionKey)
        {
            if (!VerbosityOptions.Contains(arguments[0], StringComparer.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineDiagnosticOptionExpectsSingleArgumentErrorMessage);
            }
        }

        if (commandOption.Name == DiscoverTestsOptionKey
            && arguments.Length == 1
            && !DiscoverTestsJsonArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase)
            && !DiscoverTestsTextArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineDiscoverTestsInvalidArgument, arguments[0], SupportedDiscoverTestsValues));
        }

        if (commandOption.Name == ServerOptionKey
            && arguments.Length == 1
            && !JsonRpcProtocolName.Equals(arguments[0], StringComparison.OrdinalIgnoreCase)
            && !DotnetTestCliProtocolName.Equals(arguments[0], StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineServerInvalidArgument, arguments[0], SupportedServerProtocolValues));
        }

        if (commandOption.Name == DotNetTestTransportOptionKey
            && arguments.Length == 1
            && !DotNetTestTransportPipeArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase)
            && !DotNetTestTransportWebSocketArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineDotnetTestTransportInvalidArgument, arguments[0], SupportedDotNetTestTransportValues));
        }

        if (commandOption.Name == DotNetTestWebSocketEndpointOptionKey
            && arguments is [string endpoint]
            && (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? endpointUri)
                || endpointUri.Scheme is not ("ws" or "wss")
                || !RoslynString.IsNullOrEmpty(endpointUri.Fragment)))
        {
            return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineDotnetTestWebSocketEndpointInvalid);
        }

        if (commandOption.Name == DotNetTestWebSocketTokenOptionKey
            && arguments is [string token]
            && RoslynString.IsNullOrWhiteSpace(token))
        {
            return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineDotnetTestWebSocketTokenEmpty);
        }

        if (commandOption.Name == ClientPortOptionKey
            && (!int.TryParse(arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int port)
                || port < System.Net.IPEndPoint.MinPort
                || port > System.Net.IPEndPoint.MaxPort))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLinePortOptionSingleArgument, ClientPortOptionKey));
        }

        if (commandOption.Name == ExitOnProcessExitOptionKey && (!int.TryParse(arguments[0], out int _)))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineExitOnProcessExitSingleArgument, ExitOnProcessExitOptionKey));
        }

        // CancellationTokenSource.CancelAfter caps at Timer.MaxSupportedTimeout
        // (~49.7 days). Reject values above that range here so the user gets a friendly
        // CLI error instead of an ArgumentOutOfRangeException from CancelAfter when the
        // value is consumed later.
        if (commandOption.Name == TimeoutOptionKey
            && (!TimeSpanParser.TryParseRequireSuffix(arguments[0], out TimeSpan timeout)
                || timeout <= TimeSpan.Zero
                || timeout.TotalMilliseconds > Helpers.TaskExtensions.MaxSupportedTimeoutMs))
        {
            return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineTimeoutArgumentErrorMessage);
        }

        if (commandOption.Name == ConfigFileOptionKey)
        {
            string arg = arguments[0];
            if (!File.Exists(arg))
            {
                try
                {
                    // Get the full path for better error messages.
                    // As this is only for the purpose of throwing an exception, ignore any exceptions during the GetFullPath call.
                    arg = Path.GetFullPath(arg);
                }
                catch
                {
                }

                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.ConfigurationFileNotFound, arg));
            }
        }

        if (commandOption.Name == ZeroTestsPolicyOptionKey
            && arguments is [string zeroTestsPolicyArgument]
            && !ZeroTestsPolicyStrictArgument.Equals(zeroTestsPolicyArgument, StringComparison.OrdinalIgnoreCase)
            && !ZeroTestsPolicyAllowSkippedArgument.Equals(zeroTestsPolicyArgument, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineZeroTestsPolicyInvalidArgument, zeroTestsPolicyArgument, SupportedZeroTestsPolicyValues));
        }

        // Now validate the minimum expected tests option
        return IsMinimumExpectedTestsOptionValidAsync(commandOption, arguments);
    }

    public static int GetMinimumExpectedTests(ICommandLineOptions commandLineOptions)
    {
        bool hasMinimumExpectedTestsOptionKey = commandLineOptions.TryGetOptionArgumentList(MinimumExpectedTestsOptionKey, out string[]? minimumExpectedTests);
        if (!hasMinimumExpectedTestsOptionKey || !IsMinimumExpectedTestsOptionValidAsync(MinimumExpectedTests, minimumExpectedTests ?? []).Result.IsValid)
        {
            return 0;
        }

        ApplicationStateGuard.Ensure(commandLineOptions.TryGetOptionArgumentList(MinimumExpectedTestsOptionKey, out string[]? arguments));
        return int.Parse(arguments[0], CultureInfo.InvariantCulture);
    }

    public static ZeroTestsPolicy GetZeroTestsPolicy(ICommandLineOptions commandLineOptions)
        => commandLineOptions.TryGetOptionArgumentList(ZeroTestsPolicyOptionKey, out string[]? arguments)
            && arguments is { Length: 1 }
            && ZeroTestsPolicyStrictArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase)
                ? ZeroTestsPolicy.Strict
                : ZeroTestsPolicy.AllowSkipped;

    public static bool IsListTestsJsonOutput(ICommandLineOptions commandLineOptions)
        => commandLineOptions.TryGetOptionArgumentList(DiscoverTestsOptionKey, out string[]? arguments)
            && arguments is { Length: 1 }
            && DiscoverTestsJsonArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase);

    private static Task<ValidationResult> IsMinimumExpectedTestsOptionValidAsync(CommandLineOption option, string[] arguments)
        => option.Name == MinimumExpectedTestsOptionKey
            && (arguments.Length != 1 || !int.TryParse(arguments[0], out int value) || value <= 0)
            ? ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineMinimumExpectedTestsOptionSingleArgument)
            : ValidationResult.ValidTask;

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        if (!commandLineOptions.IsOptionSet(DiagnosticOptionKey))
        {
            if (commandLineOptions.IsOptionSet(DiagnosticOutputDirectoryOptionKey))
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineDiagnosticOptionIsMissing, DiagnosticOutputDirectoryOptionKey));
            }

            if (commandLineOptions.IsOptionSet(DiagnosticOutputFilePrefixOptionKey))
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineDiagnosticOptionIsMissing, DiagnosticOutputFilePrefixOptionKey));
            }
        }

        if (commandLineOptions.IsOptionSet(DiscoverTestsOptionKey)
            && commandLineOptions.IsOptionSet(MinimumExpectedTestsOptionKey))
        {
            return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineMinimumExpectedTestsIncompatibleDiscoverTests);
        }

        if (commandLineOptions.IsOptionSet(FilterUidOptionKey)
            && commandLineOptions.IsOptionSet(TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter))
        {
            return ValidationResult.InvalidTask(PlatformResources.OnlyOneFilterSupported);
        }

        // The '--server dotnettestcli' protocol path requires exactly one pre-launch transport to carry the
        // (transport-neutral) wire protocol: either the legacy named pipe ('--dotnet-test-pipe', the default,
        // implied whenever it alone is given) or an explicit WebSocket transport ('--dotnet-test-transport
        // websocket' plus its endpoint/token). Without one of these, the dotnet test connection silently never
        // activates and the application falls back to console mode, leading to confusing behavior. All of these
        // options are internal and are expected to be passed together by 'dotnet test'.
        if (commandLineOptions.TryGetOptionArgumentList(ServerOptionKey, out string[]? serverProtocolArgs)
            && serverProtocolArgs is { Length: 1 }
            && DotnetTestCliProtocolName.Equals(serverProtocolArgs[0], StringComparison.OrdinalIgnoreCase))
        {
            bool hasPipe = commandLineOptions.IsOptionSet(DotNetTestPipeOptionKey);
            bool hasTransportOption = commandLineOptions.TryGetOptionArgumentList(DotNetTestTransportOptionKey, out string[]? transportArgs)
                && transportArgs is { Length: 1 };
            bool isWebSocketTransport = hasTransportOption && DotNetTestTransportWebSocketArgument.Equals(transportArgs![0], StringComparison.OrdinalIgnoreCase);
            bool isPipeTransport = hasTransportOption && DotNetTestTransportPipeArgument.Equals(transportArgs![0], StringComparison.OrdinalIgnoreCase);
            bool hasEndpoint = commandLineOptions.IsOptionSet(DotNetTestWebSocketEndpointOptionKey);
            bool hasToken = commandLineOptions.IsOptionSet(DotNetTestWebSocketTokenOptionKey);

            // 1. Conflict: a pipe name together with anything WebSocket-shaped (explicit websocket transport, an
            // endpoint, or a token) - the two transports are mutually exclusive.
            if (hasPipe && (isWebSocketTransport || hasEndpoint || hasToken))
            {
                return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineDotnetTestTransportConflict);
            }

            // 2. WebSocket-only options given without selecting the WebSocket transport, and no pipe either: the
            // more specific and actionable message ("these options require --dotnet-test-transport websocket")
            // beats the generic "no transport selected" message from step 3 below.
            if (!hasPipe && !isWebSocketTransport && (hasEndpoint || hasToken))
            {
                return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineDotnetTestWebSocketOptionsRequireTransport);
            }

            // 3. Nothing selects a transport at all (covers: no options given, or --dotnet-test-transport pipe
            // given explicitly without the required --dotnet-test-pipe name).
            if (!hasPipe && !isWebSocketTransport)
            {
                return ValidationResult.InvalidTask(
                    isPipeTransport
                        ? string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineDotnetTestCliRequiresPipe, DotnetTestCliProtocolName, DotNetTestPipeOptionKey)
                        : PlatformResources.PlatformCommandLineDotnetTestCliRequiresTransport);
            }

            // 4. WebSocket transport selected (or implied) but incomplete.
            if (isWebSocketTransport && (!hasEndpoint || !hasToken))
            {
                return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineDotnetTestWebSocketRequiresEndpointAndToken);
            }

            // Reject impossible transport/runtime combinations as early as possible (before we ever try to
            // construct a NamedPipeClient, which would throw PlatformNotSupportedException deep inside the
            // connection bootstrap). System.IO.Pipes is not available on either single-threaded wasm runtime.
            if (hasPipe && OperatingSystem.IsBrowser())
            {
                return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLinePipeTransportNotSupportedOnBrowser);
            }

            if (hasPipe && OperatingSystem.IsWasi())
            {
                return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLinePipeTransportNotSupportedOnWasi);
            }

            // The WebSocket transport is implemented for browser-wasm (via JS interop) and for regular
            // (non-wasm) runtimes (via System.Net.WebSockets.ClientWebSocket). wasi-wasm has neither a
            // ClientWebSocket implementation nor an established JS-interop equivalent in this repo, so it is
            // not yet supported by any transport; fail fast with an actionable message instead of silently
            // falling back to console mode.
            if (isWebSocketTransport && OperatingSystem.IsWasi())
            {
                return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineWebSocketTransportNotSupportedOnWasi);
            }
        }

        if (commandLineOptions.IsOptionSet(DiagnosticFileLoggerSynchronousWriteOptionKey))
        {
            if (OperatingSystem.IsBrowser())
            {
                return ValidationResult.InvalidTask(PlatformResources.SyncFlushNotSupportedInBrowserErrorMessage);
            }
        }

        if (commandLineOptions.IsOptionSet(ExitOnProcessExitOptionKey))
        {
            if (OperatingSystem.IsBrowser())
            {
                return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineExitOnProcessExitNotSupportedInBrowser);
            }

            _ = commandLineOptions.TryGetOptionArgumentList(ExitOnProcessExitOptionKey, out string[]? pid);
            ApplicationStateGuard.Ensure(pid is not null);
            RoslynDebug.Assert(pid.Length == 1);
            int parentProcessPid = int.Parse(pid[0], CultureInfo.InvariantCulture);
            try
            {
                // We let the api to do the validity check before to go down the subscription path.
                // If we don't fail here but we fail below means that the parent process is not there anymore and we can take it as exited.
                _ = Process.GetProcessById(parentProcessPid);
            }
            catch (ArgumentException ex)
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineExitOnProcessExitInvalidDependentProcess, parentProcessPid, ex));
            }
        }

        // Validation succeeded
        return ValidationResult.ValidTask;
    }
}
