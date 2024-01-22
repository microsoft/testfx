// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Hosts;

internal interface IServerTestHost
{
    bool IsInitialized { get; }

    Task SendTelemetryEventUpdateAsync(TelemetryEventArgs args);

    Task PushDataAsync(IData data);
}
