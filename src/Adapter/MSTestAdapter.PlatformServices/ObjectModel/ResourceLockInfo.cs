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

        // Mode is publicly settable on the attribute, so a cast like (ResourceAccessMode)42 is valid C# and
        // reaches here. Normalize once, at the only entry point: 'Read' is the sole value that grants shared
        // access, so anything else - including undefined values - becomes exclusive. Without this, an
        // undefined value would be neither 'Read' nor 'ReadWrite' and would slip through the strongest-mode
        // merge and the encoder as if it were shared, which fails open.
        Mode = mode == ResourceAccessMode.Read ? ResourceAccessMode.Read : ResourceAccessMode.ReadWrite;
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
    /// one-character mode prefix (<c>R</c> for <see cref="ResourceAccessMode.Read"/>, <c>W</c> for every other
    /// value, including <see cref="ResourceAccessMode.ReadWrite"/>) followed by the resource key. Using a
    /// fixed-width prefix avoids any delimiter ambiguity with arbitrary resource strings.
    /// </summary>
    public static string Encode(ResourceLockInfo info)
        => (info.Mode == ResourceAccessMode.Read ? "R" : "W") + info.Resource;

    /// <summary>
    /// Decodes a lock previously produced by <see cref="Encode"/>. Only a recognized <c>R</c> or <c>W</c>
    /// prefix is consumed; any other input is treated as a bare resource key and decodes to the exclusive
    /// <see cref="ResourceAccessMode.ReadWrite"/> mode. Truncated, corrupted or future-version data therefore
    /// fails closed - it still names the same resource, and it runs serialized rather than racing.
    /// </summary>
    public static ResourceLockInfo Decode(string encoded)
    {
        if (encoded.Length > 0 && encoded[0] == 'R')
        {
            return new ResourceLockInfo(encoded.Substring(1), ResourceAccessMode.Read);
        }

        // Strip the prefix only when it is one we actually wrote. Stripping unconditionally would rewrite an
        // unrecognized payload into a *different* key, which would stop it conflicting with the intended one.
        string resource = encoded.Length > 0 && encoded[0] == 'W' ? encoded.Substring(1) : encoded;
        return new ResourceLockInfo(resource, ResourceAccessMode.ReadWrite);
    }
}
