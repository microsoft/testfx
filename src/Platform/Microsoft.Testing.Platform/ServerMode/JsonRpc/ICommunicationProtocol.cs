// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

internal interface ICommunicationProtocol
{
    public string Name { get; }

    public string Version { get; }

    public string Description { get; }
}
