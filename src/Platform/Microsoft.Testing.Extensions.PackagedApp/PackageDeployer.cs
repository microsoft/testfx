// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if PACKAGEDAPP_WINRT

using Microsoft.Testing.Extensions.PackagedApp.Interop;
using Microsoft.Testing.Extensions.PackagedApp.Resources;

using Windows.Management.Deployment;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// Registers a packaged (MSIX) loose layout with the OS and activates it by Application User Model ID,
/// using only public, redistributable Windows APIs — the <see cref="PackageManager"/> WinRT class for
/// registration and <see cref="ApplicationActivationManager"/> for activation. This is the equivalent
/// of the Visual-Studio-internal deployment components VSTest relies on, implemented without them.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
internal static class PackageDeployer
{
    /// <summary>
    /// Registers the loose layout described by <paramref name="manifestPath"/> in place (no copy) and
    /// prepares its package-owned storage before any activation handoffs are written.
    /// </summary>
    /// <param name="manifestPath">The full path to the layout's <c>AppxManifest.xml</c>.</param>
    /// <param name="cancellationToken">A token to observe while registering.</param>
    public static async Task RegisterAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var packageManager = new PackageManager();

        // DeveloperMode registers the unsigned build-output layout in place. It requires Developer Mode
        // (or sideloading) to be enabled on the machine, exactly like 'Add-AppxPackage -Register'.
        var options = new RegisterPackageOptions
        {
            DeveloperMode = true,
        };

        DeploymentResult result;
        try
        {
            result = await packageManager
                .RegisterPackageByUriAsync(new Uri(manifestPath), options)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected; let it propagate unwrapped.
            throw;
        }
        catch (Exception ex)
        {
            // The WinRT operation can fault (rather than completing with a DeploymentResult), for example
            // when Developer Mode is disabled or the layout is invalid. Surface it with the actionable
            // registration message instead of a raw COM/WinRT exception.
            throw new InvalidOperationException(
                string.Format(CultureInfo.CurrentCulture, ExtensionResources.PackagedAppRegistrationFailed, manifestPath, ex.Message),
                ex);
        }

        // Registration reports success through IsRegistered; ExtendedErrorCode can also carry a non-fatal
        // (informational) HRESULT, so IsRegistered is the authoritative signal.
        if (!result.IsRegistered)
        {
            throw new InvalidOperationException(
                string.Format(CultureInfo.CurrentCulture, ExtensionResources.PackagedAppRegistrationFailed, manifestPath, result.ErrorText),
                result.ExtendedErrorCode);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>Activates a registered packaged app and returns its process id.</summary>
    public static uint Activate(string appUserModelId, string? activationArguments)
        => ApplicationActivationManager.ActivateApplication(appUserModelId, activationArguments);
}

#endif
