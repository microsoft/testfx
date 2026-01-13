// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Device;

/// <summary>
/// Represents information about a device.
/// </summary>
/// <param name="Id">Unique identifier for the device (UDID, serial number, or emulator name).</param>
/// <param name="Name">Human-readable name of the device.</param>
/// <param name="Type">Type of device (emulator, simulator, or physical device).</param>
/// <param name="Platform">Platform type (Android, iOS, Windows, etc.).</param>
/// <param name="State">Current state of the device.</param>
public record DeviceInfo(
    string Id,
    string Name,
    DeviceType Type,
    PlatformType Platform,
    DeviceState State);

/// <summary>
/// Type of device.
/// </summary>
public enum DeviceType
{
    /// <summary>
    /// Android emulator.
    /// </summary>
    Emulator,

    /// <summary>
    /// iOS simulator.
    /// </summary>
    Simulator,

    /// <summary>
    /// Physical device connected via USB or network.
    /// </summary>
    Physical,
}

/// <summary>
/// Platform type for the device.
/// </summary>
public enum PlatformType
{
    /// <summary>
    /// Android platform.
    /// </summary>
    Android,

    /// <summary>
    /// iOS platform.
    /// </summary>
    iOS,

    /// <summary>
    /// macOS Catalyst.
    /// </summary>
    MacCatalyst,

    /// <summary>
    /// Windows platform.
    /// </summary>
    Windows,
}

/// <summary>
/// Current state of a device.
/// </summary>
public enum DeviceState
{
    /// <summary>
    /// Device is online and ready to use.
    /// </summary>
    Online,

    /// <summary>
    /// Device is offline or disconnected.
    /// </summary>
    Offline,

    /// <summary>
    /// Device is currently booting up.
    /// </summary>
    Booting,

    /// <summary>
    /// Device state is unknown.
    /// </summary>
    Unknown,
}

/// <summary>
/// Filter criteria for device discovery.
/// </summary>
/// <param name="Platform">Optional platform filter.</param>
/// <param name="DeviceType">Optional device type filter.</param>
/// <param name="State">Optional state filter.</param>
public record DeviceFilter(
    PlatformType? Platform = null,
    DeviceType? DeviceType = null,
    DeviceState? State = null);
