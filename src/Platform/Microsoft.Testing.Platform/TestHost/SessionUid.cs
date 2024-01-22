// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.TestHost;

public readonly struct SessionUid(string value)
{
    public string Value { get; } = value;

    public override string ToString() => $"SessionUid {{ Value = {Value} }}";
}
