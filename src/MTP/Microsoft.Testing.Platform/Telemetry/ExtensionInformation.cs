// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed class ExtensionInformation(string id, string version, bool enabled)
{
    public string Id { get; } = id;

    public string Version { get; } = version;

    public bool Enabled { get; } = enabled;
}
