// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
namespace System.Runtime.InteropServices;

[Microsoft.CodeAnalysis.Embedded]
internal readonly struct OSPlatform : IEquatable<OSPlatform>
{
    public static OSPlatform Windows { get; } = new OSPlatform("WINDOWS");

    internal string Name { get; }

    private OSPlatform(string osPlatform)
        => Name = osPlatform;

    public bool Equals(OSPlatform other)
        => Equals(other.Name);

    private bool Equals(string? other)
        => string.Equals(Name, other, StringComparison.OrdinalIgnoreCase);

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is OSPlatform osPlatform && Equals(osPlatform);

    public override int GetHashCode()
        => Name == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    public override string ToString()
        => Name ?? string.Empty;

    public static bool operator ==(OSPlatform left, OSPlatform right)
        => left.Equals(right);

    public static bool operator !=(OSPlatform left, OSPlatform right)
        => !(left == right);
}
#endif
