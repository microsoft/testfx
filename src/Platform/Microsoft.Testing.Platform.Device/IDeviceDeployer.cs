// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Device;

/// <summary>
/// Handles deployment of applications to devices.
/// </summary>
public interface IDeviceDeployer
{
    /// <summary>
    /// Deploys an application to the specified device.
    /// </summary>
    /// <param name="device">Target device.</param>
    /// <param name="options">Deployment options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deployment result.</returns>
    Task<DeploymentResult> DeployAsync(DeviceInfo device, DeploymentOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Uninstalls an application from the specified device.
    /// </summary>
    /// <param name="device">Target device.</param>
    /// <param name="appId">Application identifier (package name or bundle ID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if uninstall was successful.</returns>
    Task<bool> UninstallAsync(DeviceInfo device, string appId, CancellationToken cancellationToken);
}

/// <summary>
/// Options for application deployment.
/// </summary>
/// <param name="AppPath">Path to the application package (APK, IPA, etc.).</param>
/// <param name="AppId">Application identifier.</param>
/// <param name="Timeout">Deployment timeout.</param>
public record DeploymentOptions(
    string AppPath,
    string AppId,
    TimeSpan Timeout = default);

/// <summary>
/// Result of a deployment operation.
/// </summary>
/// <param name="Success">Whether deployment was successful.</param>
/// <param name="Message">Status or error message.</param>
public record DeploymentResult(
    bool Success,
    string Message);
