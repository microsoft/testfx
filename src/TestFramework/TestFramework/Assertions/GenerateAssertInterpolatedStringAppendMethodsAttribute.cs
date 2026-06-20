// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Marks an <c>Assert.*InterpolatedStringHandler</c> struct so the source generator emits its
/// repetitive <c>AppendLiteral</c>/<c>AppendFormatted</c> overload set. The marked struct only needs to
/// declare its state (the <c>StringBuilder? _builder</c> field), constructor, and <c>ComputeAssertion</c>
/// logic; the generated partial supplies the interpolated-string-handler append methods that forward to
/// <see cref="AssertInterpolatedStringHandlerAppender"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
internal sealed class GenerateAssertInterpolatedStringAppendMethodsAttribute : Attribute
{
    /// <summary>
    /// Gets or sets a value indicating whether the generated <c>AppendLiteral</c> overload accepts a
    /// nullable <c>string?</c> parameter (instead of the default non-nullable <c>string</c>).
    /// </summary>
    public bool NullableLiteralParameter { get; set; }
}
