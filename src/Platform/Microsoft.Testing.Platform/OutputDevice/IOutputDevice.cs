// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;

namespace Microsoft.Testing.Platform.OutputDevice;

public interface IOutputDevice
{
    Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data);
}
