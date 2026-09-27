// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if CLASSIC_UWP

using System.Security.Cryptography;

using Microsoft.Testing.Platform.Builder;

using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.Storage.Streams;

using RetryBuilderHook = Microsoft.Testing.Extensions.Retry.TestingPlatformBuilderHook;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Restores Microsoft Testing Platform arguments and controller environment for a classic UWP test application.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "The classic UWP bootstrap directly hosts MTP.")]
public static class PackagedAppExtensions
{
    private const string EnabledExtensionsEnvironmentVariable = "MSTEST_APPMODEL_CONTROLLER_EXTENSIONS";
    private const string InlinePrefix = "mtp:v1:inline:";
    private const string FilePrefix = "mtp:v1:file:";
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
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

        if (activationArguments.StartsWith(FilePrefix, StringComparison.Ordinal))
        {
            string[] fileArguments = ReadEncryptedPayload(activationArguments.Substring(FilePrefix.Length));
            ApplyConnectBackEnvironment(fileArguments);
            return fileArguments;
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

    private static string[] ReadEncryptedPayload(string reference)
    {
        int separator = reference.IndexOf(':');
        if (separator <= 0 || !Guid.TryParseExact(reference.Substring(0, separator), "N", out Guid tokenValue))
        {
            throw new FormatException("The activation argument file reference is invalid.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(reference.Substring(separator + 1));
        }
        catch (FormatException ex)
        {
            throw new FormatException("The activation argument file key is invalid.", ex);
        }

        if (key.Length != KeySize)
        {
            Array.Clear(key, 0, key.Length);
            throw new FormatException("The activation argument file key is invalid.");
        }

        string token = tokenValue.ToString("N");
        string payloadPath = Path.Combine(
            ApplicationData.Current.LocalFolder.Path,
            $"mtp-activation-{token}.payload");
        byte[] encryptedPayload;
        try
        {
            using var stream = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.None);
            if (stream.Length > int.MaxValue)
            {
                throw new FormatException("The activation argument payload is too large.");
            }

            encryptedPayload = new byte[(int)stream.Length];
            int offset = 0;
            while (offset < encryptedPayload.Length)
            {
                int read = stream.Read(encryptedPayload, offset, encryptedPayload.Length - offset);
                if (read == 0)
                {
                    throw new EndOfStreamException("The activation argument payload is truncated.");
                }

                offset += read;
            }
        }
        catch (FormatException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new FormatException("The activation argument payload could not be read.", ex);
        }
        finally
        {
            try
            {
                if (File.Exists(payloadPath))
                {
                    File.Delete(payloadPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Best-effort delete of activation-argument payload file '{payloadPath}' failed: {ex}");
            }
        }

        try
        {
            if (encryptedPayload.Length < NonceSize + TagSize)
            {
                throw new FormatException("The activation argument payload is truncated.");
            }

            byte[] nonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];
            byte[] ciphertext = new byte[encryptedPayload.Length - NonceSize - TagSize];
            System.Buffer.BlockCopy(encryptedPayload, 0, nonce, 0, nonce.Length);
            System.Buffer.BlockCopy(encryptedPayload, NonceSize, tag, 0, tag.Length);
            System.Buffer.BlockCopy(encryptedPayload, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

            var algorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesGcm);
            IBuffer keyBuffer = CryptographicBuffer.CreateFromByteArray(key);
            CryptographicKey cryptographicKey = algorithm.CreateSymmetricKey(keyBuffer);
            IBuffer plaintextBuffer;
            try
            {
                plaintextBuffer = CryptographicEngine.DecryptAndAuthenticate(
                    cryptographicKey,
                    CryptographicBuffer.CreateFromByteArray(ciphertext),
                    CryptographicBuffer.CreateFromByteArray(nonce),
                    CryptographicBuffer.CreateFromByteArray(tag),
                    CryptographicBuffer.CreateFromByteArray(Encoding.ASCII.GetBytes(FilePrefix + token)));
            }
            catch (Exception ex)
            {
                throw new FormatException("The activation argument payload failed authentication.", ex);
            }

            CryptographicBuffer.CopyToByteArray(plaintextBuffer, out byte[] plaintext);
            try
            {
                return Deserialize(plaintext);
            }
            finally
            {
                Array.Clear(plaintext, 0, plaintext.Length);
            }
        }
        finally
        {
            Array.Clear(key, 0, key.Length);
            Array.Clear(encryptedPayload, 0, encryptedPayload.Length);
        }
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
        TrxReport.TestingPlatformBuilderHook.AddExtensions(builder, arguments);
        if (GetEnabledExtensions().Contains("retry"))
        {
            RetryBuilderHook.AddExtensions(builder, arguments);
        }

        using ITestApplication application = await builder.BuildAsync().ConfigureAwait(false);
        return await application.RunAsync().ConfigureAwait(false);
    }

    private static HashSet<string> GetEnabledExtensions()
        => new(
            (Environment.GetEnvironmentVariable(EnabledExtensionsEnvironmentVariable) ?? string.Empty)
                .Split([';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(extension => extension.Trim()),
            StringComparer.OrdinalIgnoreCase);

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
