// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC;

[Embedded]
internal interface IServer :
#if NETCOREAPP
    IAsyncDisposable,
#endif
    IDisposable
{
    PipeNameDescription PipeName { get; }

    Task WaitConnectionAsync(CancellationToken cancellationToken);
}
