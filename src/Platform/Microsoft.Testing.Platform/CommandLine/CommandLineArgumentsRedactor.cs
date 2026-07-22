// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.CommandLine;

/// <summary>
/// Formats raw command-line arguments for diagnostics (e.g. the "Command line arguments" line written to the
/// <c>--diagnostic</c> log) while masking the value(s) of options that carry secrets, so enabling verbose
/// diagnostics never writes a secret to disk. WebSocket endpoint values are also reduced to scheme, host,
/// port, and path so user-info, query parameters, and fragments cannot leak credentials. See
/// <c>docs/mstest-runner-protocol/004-protocol-dotnet-test-pipe.md</c> §15.4: the
/// <c>--dotnet-test-websocket-token</c> value is a per-run authentication secret and must never be logged.
/// </summary>
internal static class CommandLineArgumentsRedactor
{
    private const string RedactedPlaceholder = "***REDACTED***";

    // Option names (without leading dashes) whose argument value(s) must never appear in diagnostics output.
    // Extend this set - not the call site - whenever a new option is introduced that carries a secret.
    private static readonly string[] SensitiveOptionNames =
    [
        PlatformCommandLineProvider.DotNetTestWebSocketTokenOptionKey,
    ];

    /// <summary>
    /// Formats <paramref name="args"/> as a single space-separated string suitable for diagnostics, replacing
    /// the value(s) of any option in <see cref="SensitiveOptionNames"/> with a fixed placeholder. Every other
    /// argument - including the sensitive option's own name/prefix/delimiter - is preserved exactly except for
    /// the sanitized WebSocket endpoint, so the output remains useful for troubleshooting.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="CommandLineParser"/>'s own option-detection rule (a token starting with a single '-'
    /// followed by a non-'-' character, or '--' followed by a non-'-' character, optionally with an inline
    /// '='/':' delimited value) so this stays in sync with how the real parser identifies option boundaries.
    /// Handles a missing value (the sensitive option is the last argument - nothing follows to redact, no
    /// out-of-range access) and a repeated option (each occurrence's value(s) are independently redacted).
    /// </remarks>
    public static string Redact(string[] args)
    {
        if (args.Length == 0)
        {
            return string.Empty;
        }

        string[] redacted = new string[args.Length];
        bool redactingValuesForCurrentOption = false;
        bool redactedValueForCurrentOption = false;
        bool sanitizingEndpointValue = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (TryParseOption(arg, out string? prefix, out string? optionName, out string? delimiter, out string? inlineValue))
            {
                sanitizingEndpointValue = false;
                bool isSensitive = IsSensitiveOptionName(optionName);
                if (redactingValuesForCurrentOption
                    && !redactedValueForCurrentOption
                    && !isSensitive
                    && !IsWebSocketEndpointOptionName(optionName))
                {
                    // An opaque secret may itself look like an option (for example "-secret"). Diagnostic
                    // logging happens before arity validation, so mask this token rather than leaking it.
                    redacted[i] = RedactedPlaceholder;
                    redactingValuesForCurrentOption = false;
                    redactedValueForCurrentOption = true;
                    continue;
                }

                if (isSensitive && inlineValue is not null)
                {
                    // Inline form, e.g. '--dotnet-test-websocket-token=<secret>': redact just the value,
                    // preserving the original prefix/name/delimiter exactly.
                    redacted[i] = $"{prefix}{optionName}{delimiter}{RedactedPlaceholder}";
                    redactingValuesForCurrentOption = true;
                    redactedValueForCurrentOption = true;
                }
                else if (IsWebSocketEndpointOptionName(optionName))
                {
                    redacted[i] = inlineValue is null
                        ? arg
                        : $"{prefix}{optionName}{delimiter}{SanitizeWebSocketEndpoint(inlineValue)}";
                    redactingValuesForCurrentOption = false;
                    redactedValueForCurrentOption = false;
                    sanitizingEndpointValue = true;
                }
                else
                {
                    // Bare option name (no inline value): keep it as-is. If it is sensitive, its value(s) are
                    // one or more separate following tokens - redact those below until the next option.
                    redacted[i] = arg;
                    redactingValuesForCurrentOption = isSensitive;
                    redactedValueForCurrentOption = false;
                }

                continue;
            }

            if (sanitizingEndpointValue)
            {
                redacted[i] = SanitizeWebSocketEndpoint(arg);
                continue;
            }

            // Not an option-looking token: it's an argument value for whichever option preceded it.
            redacted[i] = redactingValuesForCurrentOption ? RedactedPlaceholder : arg;
            redactedValueForCurrentOption |= redactingValuesForCurrentOption;
        }

        return string.Join(" ", redacted);
    }

    private static bool TryParseOption(string arg, [NotNullWhen(true)] out string? prefix, [NotNullWhen(true)] out string? optionName, out string? delimiter, out string? inlineValue)
    {
        prefix = null;
        optionName = null;
        delimiter = null;
        inlineValue = null;

        bool isDoubleDash = arg.Length > 2 && arg[0] == '-' && arg[1] == '-' && arg[2] != '-';
        bool isSingleDash = !isDoubleDash && arg.Length > 1 && arg[0] == '-' && arg[1] != '-';
        if (!isDoubleDash && !isSingleDash)
        {
            return false;
        }

        prefix = isDoubleDash ? "--" : "-";
        string withoutPrefix = arg[prefix.Length..];

        int delimiterIndex = withoutPrefix.IndexOfAny(['=', ':']);
        if (delimiterIndex == -1)
        {
            optionName = withoutPrefix;
            return true;
        }

        optionName = withoutPrefix[..delimiterIndex];
        delimiter = withoutPrefix[delimiterIndex].ToString();
        inlineValue = withoutPrefix[(delimiterIndex + 1)..];
        return true;
    }

    private static bool IsSensitiveOptionName(string optionName)
        => SensitiveOptionNames.Any(sensitive => sensitive.Equals(optionName, StringComparison.OrdinalIgnoreCase));

    private static bool IsWebSocketEndpointOptionName(string optionName)
        => PlatformCommandLineProvider.DotNetTestWebSocketEndpointOptionKey.Equals(optionName, StringComparison.OrdinalIgnoreCase);

    private static string SanitizeWebSocketEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
            || uri.Scheme is not ("ws" or "wss"))
        {
            return RedactedPlaceholder;
        }

        UriBuilder builder = new(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty,
        };

        return builder.Uri.AbsoluteUri;
    }
}
