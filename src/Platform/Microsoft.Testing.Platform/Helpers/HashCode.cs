// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP
namespace System;

// https://stackoverflow.com/questions/263400/what-is-the-best-algorithm-for-overriding-gethashcode/263416#263416
internal struct HashCode
{
    private int _hash;

    public HashCode() => _hash = 17;

    public void Add(string value)
    {
        // Overflow is fine, just wrap
        unchecked
        {
            _hash = (_hash * 23) + (value?.GetHashCode() ?? 0);
        }
    }

    public void Add(bool value)
    {
        // Overflow is fine, just wrap
        unchecked
        {
            _hash = (_hash * 23) + value.GetHashCode();
        }
    }

    public void Add(int value)
    {
        // Overflow is fine, just wrap
        unchecked
        {
            _hash = (_hash * 23) + value.GetHashCode();
        }
    }

    public readonly int ToHashCode()
        => _hash;
}
#endif
