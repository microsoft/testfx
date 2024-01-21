// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.TestHost;

public readonly struct SessionUid(string uid)
{
    public string Uid { get; } = uid;

    public override string ToString() => Uid;
}
