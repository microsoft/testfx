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
