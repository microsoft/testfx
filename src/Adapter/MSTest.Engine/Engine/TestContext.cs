// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

internal readonly struct TestContext(CancellationToken cancellationToken)
{
    public CancellationToken CancellationToken { get; } = cancellationToken;
}
