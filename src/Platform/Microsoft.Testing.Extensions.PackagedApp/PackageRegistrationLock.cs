// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

using Microsoft.Testing.Extensions.PackagedApp.Resources;

namespace Microsoft.Testing.Extensions.PackagedApp;

[SupportedOSPlatform("windows")]
internal static class PackageRegistrationLock
{
    private const int SharingViolationHResult = unchecked((int)0x80070020);

    public static Task<T> WithManifestAsync<T>(
        string manifestPath,
        Func<AppxManifestInfo, Task<T>> action,
        CancellationToken cancellationToken)
        => WithManifestAsync(manifestPath, AcquireAsync, action, cancellationToken);

    internal static async Task<T> WithManifestAsync<T>(
        string manifestPath,
        Func<string, CancellationToken, Task<FileStream>> acquireRegistrationLock,
        Func<AppxManifestInfo, Task<T>> action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Windows must read the same manifest that selected the family lock and activation AUMID.
        // Read sharing permits deployment to read it, but prevents a rebuild from changing or replacing it.
        using FileStream manifest = File.OpenRead(manifestPath);
        var manifestInfo = AppxManifestInfo.ReadFromManifest(manifest);
        using FileStream registrationLock = await acquireRegistrationLock(manifestInfo.PackageFamilyName, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await action(manifestInfo).ConfigureAwait(false);
    }

    public static Task<FileStream> AcquireAsync(string packageFamilyName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (localApplicationData.Length == 0)
        {
            throw new InvalidOperationException(ExtensionResources.PackagedAppRegistrationLockDirectoryUnavailable);
        }

        // Keep the lock outside package-owned storage: registration may replace that storage and
        // must establish its AppContainer ACL before the controller writes anything there.
        string lockDirectory = Path.Combine(localApplicationData, "Microsoft.Testing.Platform", "PackagedApp", "RegistrationLocks");
        return AcquireAsync(packageFamilyName, lockDirectory, cancellationToken);
    }

    internal static async Task<FileStream> AcquireAsync(string packageFamilyName, string lockDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(lockDirectory);
        string lockName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(packageFamilyName.ToUpperInvariant())));
        string lockPath = Path.Combine(lockDirectory, lockName + ".lock");

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // A file lease is not thread-affine and the OS releases it if the controller exits.
                // Leave the file in place so every contender continues to lock the same file.
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex) when (ex.HResult == SharingViolationHResult)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
