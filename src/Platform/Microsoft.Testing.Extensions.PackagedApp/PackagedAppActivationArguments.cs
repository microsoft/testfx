// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

using Microsoft.Testing.Extensions.PackagedApp.Resources;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// Serializes the platform-prepared argument array into the opaque string delivered to an AppContainer
/// app through launch activation, and restores the exact array in the activated host.
/// </summary>
internal static class PackagedAppActivationArguments
{
    private const string InlinePrefix = "mtp:v1:inline:";
    private const string FilePrefix = "mtp:v1:file:";

    // LaunchActivatedEventArgs receives the same app-defined argument string used by Windows launch
    // activation surfaces such as SecondaryTile.Arguments, whose documented limit is 2,048 characters.
    // Keep the direct payload within that proven envelope. Larger payloads use a one-shot encrypted file.
    private const int MaximumInlineLength = 2048;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly TimeSpan StalePayloadAge = TimeSpan.FromDays(1);

    public static PackagedAppActivationData Create(IReadOnlyList<string> arguments, string localStateDirectory)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrEmpty(localStateDirectory);

        byte[] serialized = Serialize(arguments);
        try
        {
            if (Directory.Exists(localStateDirectory))
            {
                TryDeleteStalePayloads(localStateDirectory, TimeProvider.System.GetUtcNow().UtcDateTime);
            }

            long inlineLength = InlinePrefix.Length + ((((long)serialized.Length + 2) / 3) * 4);
            if (inlineLength <= MaximumInlineLength)
            {
                return new PackagedAppActivationData(
                    InlinePrefix + Convert.ToBase64String(serialized),
                    payloadPath: null);
            }

            Directory.CreateDirectory(localStateDirectory);

            string token = Guid.NewGuid().ToString("N");
            string payloadPath = GetPayloadPath(localStateDirectory, token);
            byte[] key = RandomNumberGenerator.GetBytes(KeySize);
            try
            {
                byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
                byte[] ciphertext = new byte[serialized.Length];
                byte[] tag = new byte[TagSize];

                using (var aes = new AesGcm(key, TagSize))
                {
                    aes.Encrypt(nonce, serialized, ciphertext, tag, GetAssociatedData(token));
                }

                byte[] encryptedPayload = new byte[NonceSize + TagSize + ciphertext.Length];
                nonce.CopyTo(encryptedPayload, 0);
                tag.CopyTo(encryptedPayload, NonceSize);
                ciphertext.CopyTo(encryptedPayload, NonceSize + TagSize);
                try
                {
                    File.WriteAllBytes(payloadPath, encryptedPayload);

                    return new PackagedAppActivationData(
                        $"{FilePrefix}{token}:{Convert.ToBase64String(key)}",
                        payloadPath);
                }
                catch
                {
                    // File.WriteAllBytes can leave a partial file behind. Create has not returned its path
                    // to the launcher yet, so this is the only cleanup point that can remove it.
                    TryDeletePayload(payloadPath);
                    throw;
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(serialized);
        }
    }

    public static string[] Read(string activationArguments, string? localStateDirectory)
    {
        ArgumentNullException.ThrowIfNull(activationArguments);

        if (activationArguments.StartsWith(InlinePrefix, StringComparison.Ordinal))
        {
            byte[] serialized;
            try
            {
                serialized = Convert.FromBase64String(activationArguments[InlinePrefix.Length..]);
            }
            catch (FormatException ex)
            {
                throw new FormatException(ExtensionResources.ActivationArgumentsInvalidInlinePayload, ex);
            }

            try
            {
                return Deserialize(serialized);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(serialized);
            }
        }

        return activationArguments.StartsWith(FilePrefix, StringComparison.Ordinal)
            ? ReadEncryptedPayload(activationArguments[FilePrefix.Length..], localStateDirectory)
            : throw new FormatException(ExtensionResources.ActivationArgumentsMissingPayload);
    }

    public static void TryDeletePayload(string? payloadPath)
    {
        if (payloadPath is null)
        {
            return;
        }

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

    public static void TryDeleteStalePayloads(string localStateDirectory, DateTime utcNow)
    {
        string[] payloadPaths;
        try
        {
            payloadPaths = Directory.GetFiles(localStateDirectory, "mtp-activation-*.payload", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Best-effort enumeration of stale activation-argument payloads in '{localStateDirectory}' failed: {ex}");
            return;
        }

        DateTime staleBefore = utcNow - StalePayloadAge;
        foreach (string payloadPath in payloadPaths)
        {
            try
            {
                if (File.GetLastWriteTimeUtc(payloadPath) <= staleBefore)
                {
                    TryDeletePayload(payloadPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Best-effort inspection of activation-argument payload file '{payloadPath}' failed: {ex}");
            }
        }
    }

    private static string[] ReadEncryptedPayload(string reference, string? localStateDirectory)
    {
        int separator = reference.IndexOf(':');
        if (separator <= 0
            || !Guid.TryParseExact(reference[..separator], "N", out Guid tokenValue)
            || localStateDirectory is null)
        {
            throw new FormatException(ExtensionResources.ActivationArgumentsInvalidFileReference);
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(reference[(separator + 1)..]);
        }
        catch (FormatException ex)
        {
            throw new FormatException(ExtensionResources.ActivationArgumentsInvalidFileKey, ex);
        }

        if (key.Length != KeySize)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new FormatException(ExtensionResources.ActivationArgumentsInvalidFileKey);
        }

        try
        {
            string token = tokenValue.ToString("N");
            string payloadPath = GetPayloadPath(localStateDirectory, token);
            byte[] encryptedPayload;
            try
            {
                // Opening with no sharing atomically claims the payload: a concurrent consumer cannot open
                // the same path. DeleteOnClose keeps that claim through the read and removes the file even
                // when reading or later validation fails.
                using var payloadStream = new FileStream(
                    payloadPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None,
                    bufferSize: 4096,
                    FileOptions.DeleteOnClose);
                if (payloadStream.Length > int.MaxValue)
                {
                    throw new FormatException(ExtensionResources.ActivationArgumentsPayloadTooLarge);
                }

                encryptedPayload = new byte[(int)payloadStream.Length];
                payloadStream.ReadExactly(encryptedPayload);
            }
            catch (FormatException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new FormatException(ExtensionResources.ActivationArgumentsPayloadReadFailed, ex);
            }

            if (encryptedPayload.Length < NonceSize + TagSize)
            {
                throw new FormatException(ExtensionResources.ActivationArgumentsPayloadTruncated);
            }

            ReadOnlySpan<byte> nonce = encryptedPayload.AsSpan(0, NonceSize);
            ReadOnlySpan<byte> tag = encryptedPayload.AsSpan(NonceSize, TagSize);
            ReadOnlySpan<byte> ciphertext = encryptedPayload.AsSpan(NonceSize + TagSize);
            byte[] serialized = new byte[ciphertext.Length];
            try
            {
                using (var aes = new AesGcm(key, TagSize))
                {
                    aes.Decrypt(nonce, ciphertext, tag, serialized, GetAssociatedData(token));
                }

                return Deserialize(serialized);
            }
            catch (AuthenticationTagMismatchException ex)
            {
                throw new FormatException(ExtensionResources.ActivationArgumentsPayloadAuthenticationFailed, ex);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(serialized);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] Serialize(IReadOnlyList<string> arguments)
    {
        long byteCount = sizeof(int);
        foreach (string argument in arguments)
        {
            ArgumentNullException.ThrowIfNull(argument);
            byteCount += sizeof(int) + ((long)argument.Length * sizeof(char));
        }

        if (byteCount > int.MaxValue)
        {
            throw new ArgumentException(ExtensionResources.ActivationArgumentsPayloadTooLarge, nameof(arguments));
        }

        byte[] payload = new byte[(int)byteCount];
        Span<byte> destination = payload;
        WriteInt32(destination, arguments.Count);
        int offset = sizeof(int);
        foreach (string argument in arguments)
        {
            WriteInt32(destination[offset..], argument.Length);
            offset += sizeof(int);

            foreach (char value in argument)
            {
                destination[offset++] = (byte)value;
                destination[offset++] = (byte)(value >> 8);
            }
        }

        return payload;
    }

    private static string[] Deserialize(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < sizeof(int))
        {
            throw new FormatException(ExtensionResources.ActivationArgumentsPayloadTruncated);
        }

        int count = ReadInt32(payload);
        if (count < 0 || count > (payload.Length - sizeof(int)) / sizeof(int))
        {
            throw new FormatException(ExtensionResources.ActivationArgumentsInvalidArgumentCount);
        }

        string[] arguments = new string[count];
        int offset = sizeof(int);
        for (int argumentIndex = 0; argumentIndex < count; argumentIndex++)
        {
            if (payload.Length - offset < sizeof(int))
            {
                throw new FormatException(ExtensionResources.ActivationArgumentsPayloadTruncated);
            }

            int charCount = ReadInt32(payload[offset..]);
            offset += sizeof(int);
            if (charCount < 0 || charCount > (payload.Length - offset) / sizeof(char))
            {
                throw new FormatException(ExtensionResources.ActivationArgumentsInvalidArgumentLength);
            }

            char[] chars = new char[charCount];
            for (int i = 0; i < chars.Length; i++)
            {
                int sourceOffset = offset + (i * sizeof(char));
                chars[i] = (char)(payload[sourceOffset] | (payload[sourceOffset + 1] << 8));
            }

            arguments[argumentIndex] = new string(chars);
            offset += charCount * sizeof(char);
        }

        return offset == payload.Length
            ? arguments
            : throw new FormatException(ExtensionResources.ActivationArgumentsTrailingData);
    }

    private static byte[] GetAssociatedData(string token) => Encoding.ASCII.GetBytes($"{FilePrefix}{token}");

    private static string GetPayloadPath(string localStateDirectory, string token)
        => Path.Combine(localStateDirectory, $"mtp-activation-{token}.payload");

    private static int ReadInt32(ReadOnlySpan<byte> source)
        => source[0]
            | (source[1] << 8)
            | (source[2] << 16)
            | (source[3] << 24);

    private static void WriteInt32(Span<byte> destination, int value)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)(value >> 16);
        destination[3] = (byte)(value >> 24);
    }
}

internal sealed class PackagedAppActivationData
{
    public PackagedAppActivationData(string arguments, string? payloadPath)
    {
        Arguments = arguments;
        PayloadPath = payloadPath;
    }

    public string Arguments { get; }

    public string? PayloadPath { get; }
}
