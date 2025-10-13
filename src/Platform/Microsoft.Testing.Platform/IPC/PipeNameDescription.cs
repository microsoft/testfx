// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC;

[Embedded]
internal sealed class PipeNameDescription(string name) : IDisposable
{
    public string Name { get; } = name;

    // This *was* available via IVT.
    // Avoid removing it as it can be seen as a binary breaking change when users use newer version of core MTP but older version of one of the extensions.
    public void Dispose()
    {
    }
}
