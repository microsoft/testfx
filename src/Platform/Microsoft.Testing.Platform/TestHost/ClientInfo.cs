// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.TestHost;

public sealed class ClientInfo
{
    internal ClientInfo(string id, string version)
    {
        Id = id;
        Version = version;
    }

    public string Id { get; }

    public string Version { get; }
}
