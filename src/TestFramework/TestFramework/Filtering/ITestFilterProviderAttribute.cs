// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Shared by the built-in attribute shapes that register an <see cref="ITestFilter"/> for a test assembly.
/// The adapter preserves the shipped non-generic attribute lookup through its concrete type and uses this
/// contract for the generic shape.
/// </summary>
/// <remarks>
/// Deliberately internal. Making it public would turn "registers a test filter" into an open extension
/// point, and a user-defined provider attribute decides which filter it registers inside its own
/// constructor — something no build-time analyzer could validate. Keeping the contract internal means the
/// supported shapes stay exactly <see cref="TestFilterProviderAttribute"/> and
/// <c>TestFilterProviderAttribute&lt;TFilter&gt;</c>, both of which <c>MSTEST0081</c> can fully check.
/// </remarks>
internal interface ITestFilterProviderAttribute
{
    /// <summary>
    /// Gets the <see cref="ITestFilter"/> implementation registered by this attribute.
    /// </summary>
    /// <remarks>
    /// The <see cref="DynamicallyAccessedMembersAttribute"/> annotation must stay identical on this
    /// declaration and on every implementation: the adapter reads the filter type through this interface and
    /// passes it to a parameter that requires the same annotation, and trimming resolves annotations from
    /// the declaring member. A mismatch is reported as IL2093.
    /// </remarks>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    Type FilterType { get; }
}
