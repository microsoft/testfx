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
    /// value, including <see cref="ResourceAccessMode.ReadWrite"/>) followed by the resource key.
    /// </summary>
    /// <remarks>
    /// The prefix is fixed-width rather than delimited so that a well-formed payload always splits at exactly
    /// one position, whatever the key contains - a delimited marker such as <c>R:</c> would still have to
    /// decide what to do with a key that itself contains, or starts with, the delimiter. This encoding is not
    /// self-describing: a string that was never produced by this method but happens to begin with <c>R</c> or
    /// <c>W</c> is indistinguishable from a real payload. That is accepted because <see cref="Encode"/> and
    /// <see cref="Decode"/> are a matched pair over a private adapter property, so <see cref="Decode"/> only
    /// ever sees this method's output; keys beginning with the marker characters round-trip correctly, which
    /// is covered by tests. Making arbitrary strings unambiguous would need length-prefixing, which is not
    /// warranted for an internal, in-process property.
    /// </remarks>
    public static string Encode(ResourceLockInfo info)
        => (info.Mode == ResourceAccessMode.Read ? "R" : "W") + info.Resource;

    /// <summary>
    /// Decodes a lock previously produced by <see cref="Encode"/>. A valid payload is a recognized <c>R</c> or
    /// <c>W</c> prefix followed by at least one key character; anything else is treated as a bare resource key
    /// and decodes to the exclusive <see cref="ResourceAccessMode.ReadWrite"/> mode. Truncated, corrupted or
    /// future-version data therefore fails closed - it still names the same resource, and it runs serialized
    /// rather than racing.
    /// </summary>
    public static ResourceLockInfo Decode(string encoded)
    {
        // Require a key character before consuming the prefix. [ResourceLock] rejects empty keys, so Encode can
        // never emit a bare prefix; treating "R" as a prefix would decode truncated data into an *empty shared*
        // lock, which both fails open and invents a key the attribute forbids.
        if (encoded.Length > 1 && encoded[0] == 'R')
        {
            return new ResourceLockInfo(encoded.Substring(1), ResourceAccessMode.Read);
        }

        // Strip the prefix only when it is one we actually wrote. Stripping unconditionally would rewrite an
        // unrecognized payload into a *different* key, which would stop it conflicting with the intended one.
        string resource = encoded.Length > 1 && encoded[0] == 'W' ? encoded.Substring(1) : encoded;
        return new ResourceLockInfo(resource, ResourceAccessMode.ReadWrite);
    }
}
