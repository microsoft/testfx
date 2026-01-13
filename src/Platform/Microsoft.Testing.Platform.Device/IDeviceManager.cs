// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Device;

/// <summary>
/// Manages device discovery and selection.
/// </summary>
public interface IDeviceManager
{
    /// <summary>
    /// Discovers available devices matching the specified filter.
    /// </summary>
    /// <param name="filter">Filter criteria for device discovery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of discovered devices.</returns>
    Task<IReadOnlyList<DeviceInfo>> DiscoverDevicesAsync(DeviceFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// Selects a device by ID or prompts user for selection if ID is null.
    /// </summary>
    /// <param name="deviceId">Optional device ID. If null, user will be prompted to select.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Selected device information, or null if no device was selected.</returns>
    Task<DeviceInfo?> SelectDeviceAsync(string? deviceId, CancellationToken cancellationToken);
}
