// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class OutputDeviceWriter
{
    private readonly IOutputDevice _outputDevice;
    private readonly IOutputDeviceDataProducer _outputDeviceDataProducer;

    public OutputDeviceWriter(IOutputDevice outputDevice, IOutputDeviceDataProducer outputDeviceDataProducer)
    {
        _outputDevice = outputDevice;
        _outputDeviceDataProducer = outputDeviceDataProducer;
    }

    /// <summary>
    /// Displays the output data asynchronously, using the stored producer.
    /// </summary>
    /// <param name="data">The output data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DisplayAsync(IOutputDeviceData data, CancellationToken cancellationToken)
        => await _outputDevice.DisplayAsync(_outputDeviceDataProducer, data, cancellationToken).ConfigureAwait(false);
}
