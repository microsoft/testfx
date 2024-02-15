// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.TestHost;

/// <summary>
/// Represents client information.
/// </summary>
public sealed class ClientInfo
{
    internal ClientInfo(string id, string version)
    {
        Id = id;
        Version = version;
    }

    /// <summary>
    /// Gets the client ID.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the client version.
    /// </summary>
    public string Version { get; }
}
