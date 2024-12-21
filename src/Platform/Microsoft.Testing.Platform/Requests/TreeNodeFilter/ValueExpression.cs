// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

// An expression representing a single value.
[DebuggerDisplay("{Regex}")]
internal sealed class ValueExpression(string value) : FilterExpression
{
    public string Value { get; } = value;

    public Regex Regex { get; } = new Regex($"^{value}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
