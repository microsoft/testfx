// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;

using Microsoft.Testing.Platform.Browser.Resources;

namespace Microsoft.Testing.Platform.Browser;

internal sealed record BrowserLauncherOptions(
    string HostCommand,
    string HostArguments,
    string HostWorkingDirectory,
    string BrowserExecutable,
    string? NodeExecutable,
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
                ExtensionResources.LauncherArgumentSeparatorMissing);
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < separatorIndex; i += 2)
        {
            if (i + 1 >= separatorIndex || !args[i].StartsWith("--", StringComparison.Ordinal))
            {
                throw new BrowserLauncherException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ExtensionResources.InvalidLauncherOption,
                        i + 1));
            }

            if (!options.TryAdd(args[i], args[i + 1]))
            {
                throw new BrowserLauncherException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ExtensionResources.DuplicateLauncherOption,
                        args[i]));
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
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.BrowserExecutableNotFound,
                    browserExecutable));
        }

        string nodeExecutableValue = ReadEncodedOption(options, "--node-executable-uri", required: false);
        string? nodeExecutable = string.IsNullOrWhiteSpace(nodeExecutableValue)
            ? null
            : Path.GetFullPath(nodeExecutableValue);
        if (nodeExecutable is not null && !File.Exists(nodeExecutable))
        {
            throw new BrowserLauncherException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.NodeExecutableNotFound,
                    nodeExecutable));
        }

        TimeSpan startupTimeout = ParseTimeout(options, "--startup-timeout-seconds");
        TimeSpan completionTimeout = ParseTimeout(options, "--completion-timeout-seconds");
        if (options.Count != 0)
        {
            throw new BrowserLauncherException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.UnknownLauncherOption,
                    options.Keys.First()));
        }

        string[] expandedArguments = SdkResponseFileExpander.Expand(args[(separatorIndex + 1)..]);
        var bootstrap = DotnetTestHttpBootstrap.Parse(expandedArguments);

        return new BrowserLauncherOptions(
            hostCommand,
            hostArguments,
            Path.GetFullPath(hostWorkingDirectory),
            browserExecutable,
            nodeExecutable,
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
                ? throw new BrowserLauncherException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ExtensionResources.RequiredLauncherOptionMissing,
                        name))
                : string.Empty;
        }

        string value = Uri.UnescapeDataString(encodedValue);
        return required && string.IsNullOrWhiteSpace(value)
            ? throw new BrowserLauncherException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.RequiredLauncherOptionEmpty,
                    name))
            : value;
    }

    private static TimeSpan ParseTimeout(Dictionary<string, string> options, string name)
        => options.Remove(name, out string? value)
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds)
            && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : throw new BrowserLauncherException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ExtensionResources.LauncherOptionMustBePositiveInteger,
                        name));
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
                        string.Format(
                            CultureInfo.CurrentCulture,
                            ExtensionResources.PlatformOptionMissingValue,
                            argument));
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
            => new(ExtensionResources.InvalidBootstrap);
    }
}

internal static class BrowserHostArguments
{
    public static string[] Parse(Uri hostUri)
    {
        if (string.IsNullOrEmpty(hostUri.Query))
        {
            return [];
        }

        var arguments = new List<string>();
        foreach (string component in hostUri.Query[1..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = component.IndexOf('=');
            string name = separator < 0 ? component : component[..separator];
            if (!string.Equals(WebUtility.UrlDecode(name), "arg", StringComparison.Ordinal))
            {
                continue;
            }

            string value = separator < 0 ? string.Empty : component[(separator + 1)..];
            arguments.Add(WebUtility.UrlDecode(value));
        }

        return [.. arguments];
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
                ExtensionResources.BootstrapResponseFileRequired);
        }

        for (int i = 0; i < arguments.Count - 1; i++)
        {
            if (arguments[i].StartsWith('@'))
            {
                throw new BrowserLauncherException(
                    ExtensionResources.UserResponseFilesNotSupported);
            }
        }

        var expanded = new List<string>(arguments.Count + 8);
        expanded.AddRange(arguments.Take(arguments.Count - 1));
        string path = Path.GetFullPath(arguments[^1][1..]);
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new BrowserLauncherException(
                    ExtensionResources.BootstrapResponseFileSymbolicLink);
            }

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);
            ValidateResponseFilePermissions(path);
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
            throw new BrowserLauncherException(ExtensionResources.BootstrapResponseFileReadFailed);
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
                ExtensionResources.BootstrapResponseFileAccessibleByOtherUsers);
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
