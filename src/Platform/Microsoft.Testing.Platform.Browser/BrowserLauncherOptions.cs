// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Browser;

internal sealed record BrowserLauncherOptions(
    string HostCommand,
    string HostArguments,
    string HostWorkingDirectory,
    string BrowserExecutable,
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
            throw new BrowserLauncherException(
                "The launcher command line must contain '--' before the Microsoft Testing Platform arguments.");
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < separatorIndex; i += 2)
        {
            if (i + 1 >= separatorIndex || !args[i].StartsWith("--", StringComparison.Ordinal))
            {
                throw new BrowserLauncherException($"Invalid launcher option at position {i + 1}.");
            }

            if (!options.TryAdd(args[i], args[i + 1]))
            {
                throw new BrowserLauncherException($"Launcher option '{args[i]}' was provided more than once.");
            }
        }

        string hostCommand = ReadEncodedOption(options, "--host-command-uri", required: true);
        string hostArguments = ReadEncodedOption(options, "--host-arguments-uri", required: false);
        string hostWorkingDirectory = ReadEncodedOption(options, "--host-working-directory-uri", required: true);
        string browserExecutable = Path.GetFullPath(
            ReadEncodedOption(options, "--browser-executable-uri", required: true));
        if (!File.Exists(browserExecutable))
        {
            throw new BrowserLauncherException(
                $"The configured browser executable does not exist: '{browserExecutable}'.");
        }

        TimeSpan startupTimeout = ParseTimeout(options, "--startup-timeout-seconds");
        TimeSpan completionTimeout = ParseTimeout(options, "--completion-timeout-seconds");
        if (options.Count != 0)
        {
            throw new BrowserLauncherException(
                $"Unknown launcher option '{options.Keys.First()}'.");
        }

        string[] expandedArguments = SdkResponseFileExpander.Expand(args[(separatorIndex + 1)..]);
        var bootstrap = DotnetTestHttpBootstrap.Parse(expandedArguments);

        return new BrowserLauncherOptions(
            hostCommand,
            hostArguments,
            Path.GetFullPath(hostWorkingDirectory),
            browserExecutable,
            startupTimeout,
            completionTimeout,
            expandedArguments,
            bootstrap);
    }

    private static string ReadEncodedOption(
        Dictionary<string, string> options,
        string name,
        bool required)
    {
        if (!options.Remove(name, out string? encodedValue))
        {
            return required
                ? throw new BrowserLauncherException($"Required launcher option '{name}' is missing.")
                : string.Empty;
        }

        string value = Uri.UnescapeDataString(encodedValue);
        return required && string.IsNullOrWhiteSpace(value)
            ? throw new BrowserLauncherException($"Required launcher option '{name}' is empty.")
            : value;
    }

    private static TimeSpan ParseTimeout(Dictionary<string, string> options, string name)
        => options.Remove(name, out string? value)
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds)
            && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : throw new BrowserLauncherException(
                    $"Launcher option '{name}' must be a positive integer.");
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
                    throw new BrowserLauncherException(
                        $"Microsoft Testing Platform option '{argument}' has no value.");
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

        Uri endpointUri = Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? parsedEndpoint)
            ? parsedEndpoint
            : throw InvalidBootstrap();

        return string.Equals(server, "dotnettestcli", StringComparison.OrdinalIgnoreCase)
            && string.Equals(transport, "http", StringComparison.OrdinalIgnoreCase)
            && endpointUri.Scheme is "http" or "https"
            && endpointUri.IsLoopback
            && !string.IsNullOrWhiteSpace(token)
            && !token.Any(char.IsWhiteSpace)
            && !token.Any(char.IsControl)
                ? new DotnetTestHttpBootstrap(endpointUri, token)
                : throw InvalidBootstrap();

        static BrowserLauncherException InvalidBootstrap()
            => new(
                "The SDK did not provide a valid loopback authenticated HTTP dotnettestcli bootstrap.");
    }
}

internal static class SdkResponseFileExpander
{
    public static string[] Expand(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0
            || !arguments[^1].StartsWith('@')
            || arguments[^1].Length == 1)
        {
            throw new BrowserLauncherException(
                "The browser experiment requires the SDK HTTP bootstrap response file as the final test application argument.");
        }

        for (int i = 0; i < arguments.Count - 1; i++)
        {
            if (arguments[i].StartsWith('@'))
            {
                throw new BrowserLauncherException(
                    "User response files are not supported by the browser experiment. Pass those arguments directly.");
            }
        }

        var expanded = new List<string>(arguments.Count + 8);
        expanded.AddRange(arguments.Take(arguments.Count - 1));
        string path = Path.GetFullPath(arguments[^1][1..]);
        try
        {
            ValidateResponseFilePermissions(path);
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true));

            while (reader.ReadLine() is { } line)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#')
                {
                    continue;
                }

                int separator = trimmed.IndexOfAny([' ', '\t']);
                if (separator < 0)
                {
                    expanded.Add(trimmed);
                    continue;
                }

                expanded.Add(trimmed[..separator]);
                string value = trimmed[(separator + 1)..].TrimStart();
                if (value.Length != 0)
                {
                    expanded.Add(value);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            throw new BrowserLauncherException(
                "Unable to read the SDK response file.",
                ex);
        }

        return [.. expanded];
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
                "The SDK response file is accessible by users other than its owner.");
        }
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
