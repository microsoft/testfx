﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

public sealed class TestNodeUid(string value)
{
#pragma warning disable RS0030 // Do not use banned APIs
    public string Value { get; init; } = !TAString.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentNullException(nameof(value));

#pragma warning restore RS0030 // Do not use banned APIs
    public static implicit operator string(TestNodeUid testNode) => testNode.Value;

    public static implicit operator TestNodeUid(string value) => new(value);
}
