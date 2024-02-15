// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Capabilities;

/// <summary>
/// Represents the capabilities provided by an extension.
/// </summary>
/// <typeparam name="TCapability">The type of capability.</typeparam>
public interface ICapabilities<TCapability>
    where TCapability : ICapability
{
    /// <summary>
    /// Gets the collection of capabilities.
    /// </summary>
    IReadOnlyCollection<TCapability> Capabilities { get; }
}
