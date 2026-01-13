// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Device.Android;

/// <summary>
/// Handles deployment of Android applications (APKs) to devices.
/// </summary>
public sealed class AndroidDeviceDeployer : IDeviceDeployer
{
    private readonly AdbClient _adbClient;

    public AndroidDeviceDeployer()
    {
        _adbClient = new AdbClient();
    }

    /// <inheritdoc/>
    public async Task<DeploymentResult> DeployAsync(DeviceInfo device, DeploymentOptions options, CancellationToken cancellationToken)
    {
        if (!File.Exists(options.AppPath))
        {
            return new DeploymentResult(false, $"APK file not found: {options.AppPath}");
        }

        // Install APK to device
        string args = $"-s {device.Id} install -r \"{options.AppPath}\"";
        AdbResult result = await _adbClient.ExecuteAsync(args, cancellationToken);

        if (!result.Success || !result.Output.Contains("Success", StringComparison.OrdinalIgnoreCase))
        {
            return new DeploymentResult(false, $"Failed to install APK: {result.Error}\n{result.Output}");
        }

        return new DeploymentResult(true, "APK installed successfully");
    }

    /// <inheritdoc/>
    public async Task<bool> UninstallAsync(DeviceInfo device, string appId, CancellationToken cancellationToken)
    {
        string args = $"-s {device.Id} uninstall {appId}";
        AdbResult result = await _adbClient.ExecuteAsync(args, cancellationToken);
        return result.Success;
    }
}
