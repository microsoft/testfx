// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework.SourceGeneration.Helpers;

internal readonly struct DisposableAction : IDisposable
{
    public Action Action { get; }

    public DisposableAction(Action action) => Action = action;

    public void Dispose() => Action();
}
