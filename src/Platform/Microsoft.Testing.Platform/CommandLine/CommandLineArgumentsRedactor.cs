// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.CommandLine;

internal static class CommandLineArgumentsRedactor
{
    private const string RedactedPlaceholder = "***REDACTED***";

    public static string Redact(string[] args)
        => string.Join(" ", RedactArguments(args));

    public static string RedactArgument(string[] args, int index)
        => RedactArguments(args)[index];

    private static string[] RedactArguments(string[] args)
    {
        if (args.Length == 0)
        {
            return [];
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
                bool isToken = IsTokenOption(name);
                bool isEndpoint = IsEndpointOption(name);
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
                else if (sanitizeNextEndpoint && !sensitiveValueConsumed)
                {
                    redacted[i] = SanitizeEndpoint(arg);
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

        return redacted;
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

        int optionStart = 0;
        while (optionStart < arg.Length && char.IsWhiteSpace(arg[optionStart]))
        {
            optionStart++;
        }

        int prefixLength = 0;
        while (optionStart + prefixLength < arg.Length && arg[optionStart + prefixLength] == '-')
        {
            prefixLength++;
        }

        bool hasLeadingWhitespace = optionStart > 0;
        bool isValidPrefix = !hasLeadingWhitespace && prefixLength is 1 or 2;
        bool isMalformedPrefix = hasLeadingWhitespace || prefixLength >= 3;
        if ((!isValidPrefix && !isMalformedPrefix) || optionStart + prefixLength == arg.Length)
        {
            return false;
        }

        prefix = arg[..(optionStart + prefixLength)];
        string withoutPrefix = arg[(optionStart + prefixLength)..];
        int delimiterIndex = withoutPrefix.IndexOfAny(['=', ':']);
        if (delimiterIndex == -1)
        {
            name = withoutPrefix;
        }
        else
        {
            name = withoutPrefix[..delimiterIndex];
            delimiter = withoutPrefix[delimiterIndex].ToString();
            inlineValue = withoutPrefix[(delimiterIndex + 1)..];
        }

        if (!isMalformedPrefix || IsTokenOption(name) || IsEndpointOption(name))
        {
            return true;
        }

        prefix = null;
        name = null;
        delimiter = null;
        inlineValue = null;
        return false;
    }

    private static bool IsTokenOption(string name)
        => PlatformCommandLineProvider.DotNetTestHttpTokenOptionKey.Equals(name, StringComparison.OrdinalIgnoreCase);

    private static bool IsEndpointOption(string name)
        => PlatformCommandLineProvider.DotNetTestHttpEndpointOptionKey.Equals(name, StringComparison.OrdinalIgnoreCase);

    private static string SanitizeEndpoint(string endpoint)
        => Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
            && uri.Scheme is "http" or "https"
            && RoslynString.IsNullOrEmpty(uri.UserInfo)
                ? uri.GetLeftPart(UriPartial.Authority)
                : RedactedPlaceholder;
}
