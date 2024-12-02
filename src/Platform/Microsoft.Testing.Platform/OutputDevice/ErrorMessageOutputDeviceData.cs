// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

public sealed class ErrorMessageOutputDeviceData(string message) : IOutputDeviceData
{
    public string Message { get; } = message;
}
