// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Microsoft.Testing.Platform.Requests;

// An expression representing a single value.
[DebuggerDisplay("{Regex}")]
internal sealed class ValueExpression : FilterExpression
{
    public string Value { get; }

    public Regex Regex { get; }

    public ValueExpression(string value)
    {
        Value = value;
        Regex = new Regex($"^{value}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
