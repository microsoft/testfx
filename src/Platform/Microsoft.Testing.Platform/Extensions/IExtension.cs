// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions;

/// <summary>
/// The interface that all extensions must implement. Extensions are a special kind of
/// services that have some identity.
/// </summary>
public interface IExtension
{
    /// <summary>
    /// Gets the unique identifier for the extension.
    /// </summary>
    /// <remarks>
    /// The value must be a <b>stable</b> string constant that survives class renames. It is SHA-256-hashed
    /// for telemetry, displayed verbatim in <c>--info</c> output and error messages, embedded in artifact
    /// metadata, and used for feature detection, so changing it silently breaks telemetry continuity and any
    /// consumer relying on the previous value.
    /// <para>
    /// Do <b>not</b> derive the value from <c>nameof(ImplementingClass)</c>: an IDE "Rename Symbol" refactor
    /// would then change the UID without any visible diff at the call site. Use an explicit string literal or
    /// <c>const</c> instead, even if its initial value happens to match the class name.
    /// </para>
    /// </remarks>
    string Uid { get; }

    /// <summary>
    /// Gets the version of the extension (ideally semantic version).
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets the display name of the extension.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the description of the extension.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Controls whether the extension is enabled or not. This is useful for extensions
    /// that are always registered but only enabled when certain conditions are met.
    /// For example, an extension that would want to be run only when its associated
    /// command line option is provided by the user.
    /// </summary>
    Task<bool> IsEnabledAsync();
}
