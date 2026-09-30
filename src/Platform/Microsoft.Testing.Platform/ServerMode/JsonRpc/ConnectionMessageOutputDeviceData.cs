// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.ServerMode;

// Keep connection diagnostics distinct while they wait for the initialize handshake.
internal sealed class ConnectionMessageOutputDeviceData(string text) : TextOutputDeviceData(text);
