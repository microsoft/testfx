// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// A single declared resource lock (from a <c>[ResourceLock]</c> attribute) carried on a
/// <see cref="UnitTestElement"/>: the opaque resource key and the access mode requested for it.
/// </summary>
#if NETFRAMEWORK
[Serializable]
#endif
internal sealed class ResourceLockInfo
{
    public ResourceLockInfo(string resource, ResourceAccessMode mode)
    {
        Resource = resource;
        Mode = mode;
    }

    /// <summary>
    /// Gets the opaque resource key. Two locks refer to the same resource when this value is ordinally equal.
    /// </summary>
    public string Resource { get; }

    /// <summary>
    /// Gets the requested access mode for the resource.
    /// </summary>
    public ResourceAccessMode Mode { get; }

    /// <summary>
    /// Encodes a lock as a single string for transport across the VSTest <c>TestProperty</c> boundary: a
    /// one-character mode prefix (<c>W</c> for <see cref="ResourceAccessMode.ReadWrite"/>, <c>R</c> for
    /// <see cref="ResourceAccessMode.Read"/>) followed by the resource key. Using a fixed-width prefix avoids any
    /// delimiter ambiguity with arbitrary resource strings.
    /// </summary>
    public static string Encode(ResourceLockInfo info)
        => (info.Mode == ResourceAccessMode.ReadWrite ? "W" : "R") + info.Resource;

    /// <summary>
    /// Decodes a lock previously produced by <see cref="Encode"/>. Any input that is not an explicit
    /// <c>R</c> (<see cref="ResourceAccessMode.Read"/>) prefix decodes to the exclusive
    /// <see cref="ResourceAccessMode.ReadWrite"/> mode, so truncated, corrupted or
    /// future-version-prefixed data fails closed (running serialized) rather than open (racing).
    /// </summary>
    public static ResourceLockInfo Decode(string encoded)
    {
        ResourceAccessMode mode = encoded.Length > 0 && encoded[0] == 'R'
            ? ResourceAccessMode.Read
            : ResourceAccessMode.ReadWrite;
        string resource = encoded.Length > 0 ? encoded.Substring(1) : string.Empty;
        return new ResourceLockInfo(resource, mode);
    }
}
