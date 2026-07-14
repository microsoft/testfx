// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// <b>Infrastructure.</b> Abstraction over the system clock used by Microsoft.Testing.Platform
/// and its first-party extensions.
/// </summary>
/// <remarks>
/// <b>This type is not intended to be used directly from application code.</b> It is public only so
/// that first-party platform extensions can consume it across the assembly boundary without an
/// <c>InternalsVisibleTo</c> grant (its instances are provided by the platform's service provider,
/// so it cannot be source-embedded). Its shape is an implementation detail; do not depend on it
/// from your own code.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[Experimental("TPINTERNAL")]
public interface IClock
{
    /// <summary>
    /// Gets the current date and time in Coordinated Universal Time (UTC).
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
