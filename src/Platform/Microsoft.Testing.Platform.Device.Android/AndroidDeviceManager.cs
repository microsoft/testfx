// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Device.Android;

/// <summary>
/// Manages Android device discovery and selection.
/// </summary>
public sealed class AndroidDeviceManager : IDeviceManager
{
    private readonly AdbClient _adbClient;

    public AndroidDeviceManager()
    {
        _adbClient = new AdbClient();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DeviceInfo>> DiscoverDevicesAsync(DeviceFilter filter, CancellationToken cancellationToken)
    {
        AdbResult result = await _adbClient.ExecuteAsync("devices -l", cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to discover devices: {result.Error}");
        }

        var devices = new List<DeviceInfo>();
        string[] lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Skip first line ("List of devices attached")
        foreach (string line in lines.Skip(1))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            DeviceInfo? device = ParseDeviceLine(trimmed);
            if (device is not null &&
                (filter.Platform is null || filter.Platform == PlatformType.Android) &&
                (filter.DeviceType is null || filter.DeviceType == device.Type) &&
                (filter.State is null || filter.State == device.State))
            {
                devices.Add(device);
            }
        }

        return devices;
    }

    /// <inheritdoc/>
    public async Task<DeviceInfo?> SelectDeviceAsync(string? deviceId, CancellationToken cancellationToken)
    {
        var devices = await DiscoverDevicesAsync(new DeviceFilter(Platform: PlatformType.Android), cancellationToken);

        if (devices.Count == 0)
        {
            return null;
        }

        if (deviceId is not null)
        {
            // Find device by ID
            return devices.FirstOrDefault(d => d.Id == deviceId);
        }

        if (devices.Count == 1)
        {
            // If only one device, select it automatically
            return devices[0];
        }

        // Interactive selection - for now, just return first online device
        return devices.FirstOrDefault(d => d.State == DeviceState.Online);
    }

    private static DeviceInfo? ParseDeviceLine(string line)
    {
        // Format: "emulator-5554 device product:sdk_gphone64_arm64 model:sdk_gphone64_arm64"
        // or: "ABCD1234 device product:coral model:Pixel_4_XL"
        string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        string id = parts[0];
        string stateStr = parts[1];

        DeviceState state = stateStr switch
        {
            "device" => DeviceState.Online,
            "offline" => DeviceState.Offline,
            "bootloader" or "recovery" => DeviceState.Booting,
            _ => DeviceState.Unknown,
        };

        // Determine if emulator or physical device
        DeviceType deviceType = id.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase)
            ? DeviceType.Emulator
            : DeviceType.Physical;

        // Extract model name if available
        string name = id;
        foreach (string part in parts.Skip(2))
        {
            if (part.StartsWith("model:", StringComparison.OrdinalIgnoreCase))
            {
                name = part.Substring("model:".Length).Replace('_', ' ');
                break;
            }
        }

        return new DeviceInfo(id, name, deviceType, PlatformType.Android, state);
    }
}
