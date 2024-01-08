// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#if NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// This service is responsible for platform specific reflection operations.
/// </summary>
public class ReflectionOperations : IReflectionOperations
{
    private readonly ConcurrentDictionary<MemberInfo, object[]?> _cache = new();
    private readonly ConcurrentDictionary<MemberInfo, object[]?> _cacheWithInherit = new();

    private readonly ConcurrentDictionary<MemberInfoTypeKey, object[]?> _cacheWithType = new();
    private readonly ConcurrentDictionary<MemberInfoTypeKey, object[]?> _cacheWithTypeAndInherit = new();

    private readonly ConcurrentDictionary<AssemblyTypeKey, object[]?> _cacheAssemblyAndType = new();

    /// <summary>
    /// Gets all the custom attributes adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member. </param>
    /// <param name="inherit"> True to inspect the ancestors of element; otherwise, false. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo, bool inherit)
    {
        if (inherit)
        {
            if (_cacheWithInherit.TryGetValue(memberInfo, out object[]? cachedAttributes))
            {
                return cachedAttributes!;
            }
        }
        else
        {
            if (_cache.TryGetValue(memberInfo, out object[]? cachedAttributes))
            {
                return cachedAttributes!;
            }
        }

#if NETFRAMEWORK
        object[]? attributes = ReflectionUtility.GetCustomAttributes(memberInfo, inherit);
#else
        object[]? attributes = memberInfo.GetCustomAttributes(inherit);
#endif

        if (inherit)
        {
            _cacheWithInherit.TryAdd(memberInfo, attributes);
        }
        else
        {
            _cache.TryAdd(memberInfo, attributes);
        }

        return attributes;
    }

    /// <summary>
    /// Gets all the custom attributes of a given type adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member info. </param>
    /// <param name="type"> The attribute type. </param>
    /// <param name="inherit"> True to inspect the ancestors of element; otherwise, false. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo, Type type, bool inherit)
    {
        var key = new MemberInfoTypeKey(memberInfo, type);
        if (inherit)
        {
            if (_cacheWithTypeAndInherit.TryGetValue(key, out object[]? cachedAttributes))
            {
                return cachedAttributes!;
            }
        }
        else
        {
            if (_cacheWithType.TryGetValue(key, out object[]? cachedAttributes))
            {
                return cachedAttributes!;
            }
        }

#if NETFRAMEWORK
        object[]? attributes = ReflectionUtility.GetCustomAttributes(memberInfo, type, inherit);
#else
        object[]? attributes = memberInfo.GetCustomAttributes(type, inherit);
#endif

        if (inherit)
        {
            _cacheWithTypeAndInherit.TryAdd(key, attributes);
        }
        else
        {
            _cacheWithType.TryAdd(key, attributes);
        }

        return attributes;
    }

    /// <summary>
    /// Gets all the custom attributes of a given type on an assembly.
    /// </summary>
    /// <param name="assembly"> The assembly. </param>
    /// <param name="type"> The attribute type. </param>
    /// <returns> The list of attributes of the given type on the member. Empty list if none found. </returns>
    public object[] GetCustomAttributes(Assembly assembly, Type type)
    {
        var key = new AssemblyTypeKey(assembly, type);
        if (_cacheAssemblyAndType.TryGetValue(key, out object[]? cachedAttributes))
        {
            return cachedAttributes!;
        }

#if NETFRAMEWORK
        object[]? attributes = ReflectionUtility.GetCustomAttributes(assembly, type);
#else
        object[]? attributes = assembly.GetCustomAttributes(type).ToArray();
#endif

        _cacheAssemblyAndType.TryAdd(key, attributes);

        return attributes;
    }

    private record struct AssemblyTypeKey(Assembly Assembly, Type Type);

    private record struct MemberInfoTypeKey(MemberInfo Assembly, Type Type);
}
