// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Browser;

internal sealed record BrowserLauncherOptions(
    string HostCommand,
    IReadOnlyList<string> HostArguments,
    string HostWorkingDirectory,
    string UrlPath,
    string? BrowserExecutable,
    IReadOnlyList<string> BrowserArguments,
    TimeSpan StartupTimeout,
    TimeSpan CompletionTimeout,
    IReadOnlyList<string> TestApplicationArguments,
    DotnetTestHttpBootstrap Bootstrap)
{
    public static BrowserLauncherOptions Parse(string[] args)
    {
        int separatorIndex = Array.IndexOf(args, "--");
        if (separatorIndex < 0)
        {
            throw new BrowserLauncherException("The launcher command line must contain '--' before the Microsoft Testing Platform arguments.");
        }

        string hostCommand;
        string hostArguments;
        string hostWorkingDirectory;
        string urlPath;
        string? browserExecutable;
        string browserArguments;
        TimeSpan startupTimeout;
        TimeSpan completionTimeout;

        if (separatorIndex == 2 && args[0] == "--config")
        {
            string[] configuration;
            try
            {
                configuration = File.ReadAllLines(args[1]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new BrowserLauncherException("Unable to read the browser launcher configuration file.", ex);
            }

            if (configuration.Length != 8)
            {
                throw new BrowserLauncherException("The browser launcher configuration file is invalid.");
            }

            hostCommand = ReadEncodedConfigurationValue(configuration, 0, "host-command-uri");
            hostArguments = ReadEncodedConfigurationValue(configuration, 1, "host-arguments-uri");
            hostWorkingDirectory = ReadEncodedConfigurationValue(configuration, 2, "host-working-directory-uri");
            urlPath = ReadEncodedConfigurationValue(configuration, 3, "url-path-uri");
            browserExecutable = ReadEncodedConfigurationValue(configuration, 4, "browser-executable-uri");
            browserArguments = ReadEncodedConfigurationValue(configuration, 5, "browser-arguments-uri");
            startupTimeout = ParseTimeout(
                ReadConfigurationValue(configuration, 6, "startup-timeout-seconds"),
                "startup timeout");
            completionTimeout = ParseTimeout(
                ReadConfigurationValue(configuration, 7, "completion-timeout-seconds"),
                "completion timeout");
        }
        else
        {
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < separatorIndex; i += 2)
            {
                if (i + 1 >= separatorIndex || !args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new BrowserLauncherException($"Invalid launcher option at position {i + 1}.");
                }

                options.Add(args[i], args[i + 1]);
            }

            hostCommand = DecodeRequired(options, "--host-command-base64");
            hostArguments = DecodeRequired(options, "--host-arguments-base64");
            hostWorkingDirectory = DecodeRequired(options, "--host-working-directory-base64");
            urlPath = DecodeRequired(options, "--url-path-base64");
            browserExecutable = DecodeOptional(options, "--browser-executable-base64");
            browserArguments = DecodeOptional(options, "--browser-arguments-base64") ?? string.Empty;

            startupTimeout = ParseTimeout(options, "--startup-timeout-seconds");
            completionTimeout = ParseTimeout(options, "--completion-timeout-seconds");
        }

        string[] expandedArguments = ResponseFileArgumentExpander.Expand(args[(separatorIndex + 1)..]);
        var bootstrap = DotnetTestHttpBootstrap.Parse(expandedArguments);
        string[] parsedBrowserArguments = CommandLineTokenizer.Split(browserArguments);
        ValidateBrowserArguments(parsedBrowserArguments);

        return new BrowserLauncherOptions(
            hostCommand,
            CommandLineTokenizer.Split(hostArguments),
            Path.GetFullPath(hostWorkingDirectory),
            NormalizeUrlPath(urlPath),
            browserExecutable,
            parsedBrowserArguments,
            startupTimeout,
            completionTimeout,
            expandedArguments,
            bootstrap);
    }

    private static void ValidateBrowserArguments(IReadOnlyList<string> arguments)
    {
        string[] forbiddenPrefixes =
        [
            "--remote-debugging-address",
            "--remote-debugging-pipe",
            "--remote-debugging-port",
            "--remote-allow-origins",
            "--profile-directory",
            "--user-data-dir",
        ];

        foreach (string argument in arguments)
        {
            string switchName = argument.TrimStart('-', '/');
            int valueSeparator = switchName.IndexOf('=');
            if (valueSeparator >= 0)
            {
                switchName = switchName[..valueSeparator];
            }

            if (forbiddenPrefixes.Any(prefix =>
                switchName.Equals(prefix[2..], StringComparison.OrdinalIgnoreCase)))
            {
                throw new BrowserLauncherException(
                    $"Browser argument '{argument}' is controlled by Microsoft.Testing.Platform.Browser and cannot be overridden.");
            }
        }
    }

    private static string DecodeRequired(Dictionary<string, string> options, string name)
        => DecodeOptional(options, name) is { Length: > 0 } value
            ? value
            : throw new BrowserLauncherException($"Required launcher option '{name}' is missing or empty.");

    private static string? DecodeOptional(Dictionary<string, string> options, string name)
    {
        if (!options.Remove(name, out string? encodedValue))
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encodedValue));
        }
        catch (FormatException ex)
        {
            throw new BrowserLauncherException($"Launcher option '{name}' is not valid Base64.", ex);
        }
    }

    private static TimeSpan ParseTimeout(Dictionary<string, string> options, string name)
        => options.Remove(name, out string? value)
            ? ParseTimeout(value, name)
            : throw new BrowserLauncherException($"Launcher option '{name}' must be a positive integer.");

    private static TimeSpan ParseTimeout(string value, string name)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds)
            && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : throw new BrowserLauncherException($"Launcher option '{name}' must be a positive integer.");

    private static string NormalizeUrlPath(string path)
        => path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;

    internal static Uri ResolveBrowserUri(Uri hostUri, string urlPath)
    {
        var browserUri = new Uri(hostUri, NormalizeUrlPath(urlPath));
        return string.Equals(
            browserUri.GetLeftPart(UriPartial.Authority),
            hostUri.GetLeftPart(UriPartial.Authority),
            StringComparison.Ordinal)
                ? browserUri
                : throw new BrowserLauncherException(
                    "The configured browser URL path resolves outside the browser host origin.");
    }

    private static string ReadConfigurationValue(string[] configuration, int index, string name)
    {
        string prefix = name + "=";
        return configuration[index].StartsWith(prefix, StringComparison.Ordinal)
            ? configuration[index][prefix.Length..]
            : throw new BrowserLauncherException("The browser launcher configuration file is invalid.");
    }

    private static string ReadEncodedConfigurationValue(string[] configuration, int index, string name)
        => Uri.UnescapeDataString(ReadConfigurationValue(configuration, index, name));
}

internal sealed record DotnetTestHttpBootstrap(Uri Endpoint, string Token)
{
    public static DotnetTestHttpBootstrap Parse(IReadOnlyList<string> arguments)
    {
        string? server = null;
        string? transport = null;
        string? endpoint = null;
        string? token = null;

        for (int i = 0; i < arguments.Count; i++)
        {
            string argument = arguments[i];
            if (argument is "--server" or "--dotnet-test-transport" or "--dotnet-test-http-endpoint" or "--dotnet-test-http-token")
            {
                if (++i >= arguments.Count)
                {
                    throw new BrowserLauncherException($"Microsoft Testing Platform option '{argument}' has no value.");
                }

                string value = arguments[i];
                switch (argument)
                {
                    case "--server":
                        server = value;
                        break;
                    case "--dotnet-test-transport":
                        transport = value;
                        break;
                    case "--dotnet-test-http-endpoint":
                        endpoint = value;
                        break;
                    case "--dotnet-test-http-token":
                        token = value;
                        break;
                }
            }
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? endpointUri))
        {
            throw new BrowserLauncherException(
                "The SDK did not provide a valid loopback authenticated HTTP dotnettestcli bootstrap.");
        }

        bool isValid = string.Equals(server, "dotnettestcli", StringComparison.OrdinalIgnoreCase)
            && string.Equals(transport, "http", StringComparison.OrdinalIgnoreCase)
            && endpointUri.Scheme is "http" or "https"
            && endpointUri.IsLoopback
            && !string.IsNullOrWhiteSpace(token)
            && !token.Any(char.IsWhiteSpace)
            && !token.Any(char.IsControl);

        return isValid
            ? new DotnetTestHttpBootstrap(endpointUri, token!)
            : throw new BrowserLauncherException(
                "The SDK did not provide a valid loopback authenticated HTTP dotnettestcli bootstrap.");
    }
}

internal static class ResponseFileArgumentExpander
{
    public static string[] Expand(IReadOnlyList<string> arguments)
    {
        var expanded = new List<string>();
        var activeFiles = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (string argument in arguments)
        {
            ExpandArgument(argument, expanded, activeFiles);
        }

        return [.. expanded];
    }

    private static void ExpandArgument(string argument, List<string> expanded, HashSet<string> activeFiles)
    {
        if (!argument.StartsWith('@'))
        {
            expanded.Add(argument);
            return;
        }

        string path = Path.GetFullPath(argument[1..]);
        if (!activeFiles.Add(path))
        {
            throw new BrowserLauncherException("Recursive response files are not supported.");
        }

        try
        {
            ValidateResponseFilePermissions(path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));

            while (reader.ReadLine() is { } line)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#')
                {
                    continue;
                }

                foreach (string nestedArgument in CommandLineTokenizer.Split(trimmed))
                {
                    ExpandArgument(nestedArgument, expanded, activeFiles);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            throw new BrowserLauncherException($"Unable to read the SDK response file '{Path.GetFileName(path)}'.", ex);
        }
        finally
        {
            activeFiles.Remove(path);
        }
    }

    private static void ValidateResponseFilePermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        UnixFileMode mode = File.GetUnixFileMode(path);
        const UnixFileMode disallowed =
            UnixFileMode.GroupRead
            | UnixFileMode.GroupWrite
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead
            | UnixFileMode.OtherWrite
            | UnixFileMode.OtherExecute;
        if ((mode & disallowed) != 0)
        {
            throw new BrowserLauncherException(
                $"The SDK response file '{Path.GetFileName(path)}' is accessible by users other than its owner.");
        }
    }
}

internal static class CommandLineTokenizer
{
    public static string[] Split(string commandLine)
    {
        var arguments = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        bool tokenStarted = false;
        int backslashCount = 0;

        void FlushBackslashes()
        {
            if (backslashCount > 0)
            {
                current.Append('\\', backslashCount);
                backslashCount = 0;
                tokenStarted = true;
            }
        }

        for (int i = 0; i < commandLine.Length; i++)
        {
            char character = commandLine[i];
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                tokenStarted = true;
                current.Append('\\', backslashCount / 2);
                if (inQuotes && backslashCount % 2 == 0
                    && i + 1 < commandLine.Length
                    && commandLine[i + 1] == '"')
                {
                    current.Append('"');
                    backslashCount = 0;
                    i++;
                    continue;
                }

                if (backslashCount % 2 == 0)
                {
                    inQuotes = !inQuotes;
                }
                else
                {
                    current.Append('"');
                }

                backslashCount = 0;
                continue;
            }

            FlushBackslashes();
            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (tokenStarted)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                    tokenStarted = false;
                }

                continue;
            }

            tokenStarted = true;
            current.Append(character);
        }

        FlushBackslashes();
        if (inQuotes)
        {
            throw new BrowserLauncherException("A launcher command line contains an unclosed quote.");
        }

        if (tokenStarted)
        {
            arguments.Add(current.ToString());
        }

        return [.. arguments];
    }
}

internal sealed class BrowserLauncherException : Exception
{
    public BrowserLauncherException(string message)
        : base(message)
    {
    }

    public BrowserLauncherException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
