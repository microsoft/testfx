// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if CLASSIC_UWP

using System.Security.Cryptography;

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Restores Microsoft Testing Platform arguments and controller environment for a classic UWP test application.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "The classic UWP bootstrap directly hosts MTP.")]
public static class PackagedAppExtensions
{
    private const string InlinePrefix = "mtp:v1:inline:";
    private const string TestHostControllerPidOption = "--internal-testhostcontroller-pid";
    private const string RetryPipeNameOption = "--internal-retry-pipename";

    /// <summary>
    /// Restores the argument array delivered through <c>LaunchActivatedEventArgs.Arguments</c>.
    /// </summary>
    /// <param name="activationArguments">The opaque activation arguments supplied by Windows.</param>
    /// <returns>The exact Microsoft Testing Platform argument array.</returns>
    public static string[] GetTestApplicationArguments(string activationArguments)
    {
        if (activationArguments is null)
        {
            throw new ArgumentNullException(nameof(activationArguments));
        }

        if (!activationArguments.StartsWith(InlinePrefix, StringComparison.Ordinal))
        {
            throw new FormatException("The activation argument payload is not supported by this classic UWP host.");
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(activationArguments.Substring(InlinePrefix.Length));
        }
        catch (FormatException ex)
        {
            throw new FormatException("The activation argument payload is malformed.", ex);
        }

        string[] arguments = Deserialize(payload);
        ApplyConnectBackEnvironment(arguments);
        return arguments;
    }

    /// <summary>
    /// Runs the native Microsoft Testing Platform host from a classic UWP launch activation.
    /// </summary>
    /// <param name="activationArguments">The opaque activation arguments supplied by Windows.</param>
    /// <returns>The Microsoft Testing Platform exit code.</returns>
    public static async Task<int> RunTestsAsync(string activationArguments)
    {
        string[] arguments = GetTestApplicationArguments(activationArguments);
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(arguments).ConfigureAwait(false);
        Microsoft.Testing.Platform.MSBuild.TestingPlatformBuilderHook.AddExtensions(builder, arguments);
        Telemetry.TestingPlatformBuilderHook.AddExtensions(builder, arguments);
        Microsoft.VisualStudio.TestTools.UnitTesting.TestingPlatformBuilderHook.AddExtensions(builder, arguments);
        using ITestApplication application = await builder.BuildAsync().ConfigureAwait(false);
        return await application.RunAsync().ConfigureAwait(false);
    }

    private static void ApplyConnectBackEnvironment(string[] arguments)
    {
        string? handshakeId = GetOptionValue(arguments, TestHostControllerPidOption);
        if (handshakeId is null)
        {
            string? retryPipeName = GetOptionValue(arguments, RetryPipeNameOption);
            if (retryPipeName is null)
            {
                return;
            }

            using var sha256 = SHA256.Create();
            handshakeId = "retry-" + ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(retryPipeName)));
        }

        string handshakePath = Path.Combine(
            Windows.Storage.ApplicationData.Current.LocalFolder.Path,
            "mtp-testhostcontroller-" + handshakeId + ".handshake");
        if (!File.Exists(handshakePath))
        {
            return;
        }

        string[] lines = File.ReadAllLines(handshakePath, Encoding.UTF8);
        try
        {
            File.Delete(handshakePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        foreach (string line in lines)
        {
            int separator = line.IndexOf('=');
            if (separator <= 0 || separator == line.Length - 1)
            {
                continue;
            }

            string key = line.Substring(0, separator);
            char marker = line[separator + 1];
            if (marker == 'N')
            {
                Environment.SetEnvironmentVariable(key, null);
            }
            else if (marker == 'V')
            {
                try
                {
                    string encoded = line.Substring(separator + 2);
                    Environment.SetEnvironmentVariable(
                        key,
                        Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
                }
                catch (FormatException)
                {
                }
            }
        }
    }

    private static string[] Deserialize(byte[] payload)
    {
        if (payload.Length < sizeof(int))
        {
            throw new FormatException("The activation argument payload is truncated.");
        }

        int count = ReadInt32(payload, 0);
        if (count < 0 || count > (payload.Length - sizeof(int)) / sizeof(int))
        {
            throw new FormatException("The activation argument count is invalid.");
        }

        string[] arguments = new string[count];
        int offset = sizeof(int);
        for (int argumentIndex = 0; argumentIndex < count; argumentIndex++)
        {
            if (payload.Length - offset < sizeof(int))
            {
                throw new FormatException("The activation argument payload is truncated.");
            }

            int charCount = ReadInt32(payload, offset);
            offset += sizeof(int);
            if (charCount < 0 || charCount > (payload.Length - offset) / sizeof(char))
            {
                throw new FormatException("An activation argument length is invalid.");
            }

            char[] chars = new char[charCount];
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)(payload[offset++] | (payload[offset++] << 8));
            }

            arguments[argumentIndex] = new string(chars);
        }

        return offset == payload.Length
            ? arguments
            : throw new FormatException("The activation argument payload contains trailing data.");
    }

    private static string? GetOptionValue(string[] arguments, string option)
    {
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (string.Equals(arguments[i], option, StringComparison.Ordinal))
            {
                return arguments[i + 1];
            }
        }

        return null;
    }

    private static int ReadInt32(byte[] source, int offset)
        => source[offset]
            | (source[offset + 1] << 8)
            | (source[offset + 2] << 16)
            | (source[offset + 3] << 24);

    private static string ToHexString(byte[] bytes)
    {
        const string Hex = "0123456789ABCDEF";
        char[] result = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            result[i * 2] = Hex[bytes[i] >> 4];
            result[(i * 2) + 1] = Hex[bytes[i] & 0x0F];
        }

        return new string(result);
    }
}

#endif
