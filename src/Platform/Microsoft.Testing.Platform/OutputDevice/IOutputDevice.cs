// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Represents an output device.
/// </summary>
public interface IOutputDevice
{
    /// <summary>
    /// Displays the output data asynchronously.
    /// </summary>
    /// <param name="producer">The data producer.</param>
    /// <param name="data">The output data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data);
}
