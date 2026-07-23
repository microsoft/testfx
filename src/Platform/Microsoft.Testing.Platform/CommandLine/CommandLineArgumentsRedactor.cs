// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.CommandLine;

internal static class CommandLineArgumentsRedactor
{
    private const string RedactedPlaceholder = "***REDACTED***";

    public static string Redact(string[] args)
    {
        if (args.Length == 0)
        {
            return string.Empty;
        }

        string[] redacted = new string[args.Length];
        bool redactNextValue = false;
        bool sanitizeNextEndpoint = false;
        bool sensitiveValueConsumed = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (TryParseOption(arg, out string? prefix, out string? name, out string? delimiter, out string? inlineValue))
            {
                bool isToken = PlatformCommandLineProvider.DotNetTestHttpTokenOptionKey.Equals(name, StringComparison.OrdinalIgnoreCase);
                bool isEndpoint = PlatformCommandLineProvider.DotNetTestHttpEndpointOptionKey.Equals(name, StringComparison.OrdinalIgnoreCase);
                if (isToken)
                {
                    redacted[i] = inlineValue is null
                        ? arg
                        : $"{prefix}{name}{delimiter}{RedactedPlaceholder}";
                    redactNextValue = true;
                    sanitizeNextEndpoint = false;
                    sensitiveValueConsumed = inlineValue is not null;
                }
                else if (isEndpoint)
                {
                    redacted[i] = inlineValue is null
                        ? arg
                        : $"{prefix}{name}{delimiter}{SanitizeEndpoint(inlineValue)}";
                    sanitizeNextEndpoint = true;
                    redactNextValue = false;
                    sensitiveValueConsumed = inlineValue is not null;
                }
                else if (redactNextValue && !sensitiveValueConsumed)
                {
                    redacted[i] = RedactedPlaceholder;
                    redactNextValue = false;
                    sanitizeNextEndpoint = false;
                    sensitiveValueConsumed = false;
                }
                else
                {
                    redacted[i] = arg;
                    redactNextValue = false;
                    sanitizeNextEndpoint = false;
                    sensitiveValueConsumed = false;
                }

                continue;
            }

            if (redactNextValue)
            {
                redacted[i] = RedactedPlaceholder;
                sensitiveValueConsumed = true;
            }
            else if (sanitizeNextEndpoint)
            {
                redacted[i] = SanitizeEndpoint(arg);
                sensitiveValueConsumed = true;
            }
            else
            {
                redacted[i] = arg;
            }
        }

        return string.Join(" ", redacted);
    }

    private static bool TryParseOption(
        string arg,
        [NotNullWhen(true)] out string? prefix,
        [NotNullWhen(true)] out string? name,
        out string? delimiter,
        out string? inlineValue)
    {
        prefix = null;
        name = null;
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
            name = withoutPrefix;
            return true;
        }

        name = withoutPrefix[..delimiterIndex];
        delimiter = withoutPrefix[delimiterIndex].ToString();
        inlineValue = withoutPrefix[(delimiterIndex + 1)..];
        return true;
    }

    private static string SanitizeEndpoint(string endpoint)
        => Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
            && uri.Scheme is "http" or "https"
            && RoslynString.IsNullOrEmpty(uri.UserInfo)
                ? uri.GetLeftPart(UriPartial.Authority)
                : RedactedPlaceholder;
}
