// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using System;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#if NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
#endif

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

/// <summary>
/// This service is responsible for platform specific reflection operations.
/// </summary>
public class ReflectionOperations : IReflectionOperations
{
#if NETFRAMEWORK
    private readonly ReflectionUtility _reflectionUtility;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReflectionOperations"/> class.
    /// </summary>
    public ReflectionOperations()
    {
        _reflectionUtility = new ReflectionUtility();
    }
#endif

    /// <summary>
    /// Gets all the custom attributes adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member. </param>
    /// <param name="inherit"> True to inspect the ancestors of element; otherwise, false. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    public object[] GetCustomAttributes(MemberInfo memberInfo, bool inherit)
    {
#if NETFRAMEWORK
        return _reflectionUtility.GetCustomAttributes(memberInfo, inherit);
#else
        return memberInfo.GetCustomAttributes(inherit).ToArray<object>();
#endif
    }

    /// <summary>
    /// Gets all the custom attributes of a given type adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member info. </param>
    /// <param name="type"> The attribute type. </param>
    /// <param name="inherit"> True to inspect the ancestors of element; otherwise, false. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    public object[] GetCustomAttributes(MemberInfo memberInfo, Type type, bool inherit)
    {
#if NETFRAMEWORK
        return _reflectionUtility.GetCustomAttributes(memberInfo, type, inherit);
#else
        return memberInfo.GetCustomAttributes(type, inherit).ToArray<object>();
#endif
    }

    /// <summary>
    /// Gets all the custom attributes of a given type on an assembly.
    /// </summary>
    /// <param name="assembly"> The assembly. </param>
    /// <param name="type"> The attribute type. </param>
    /// <returns> The list of attributes of the given type on the member. Empty list if none found. </returns>
    public object[] GetCustomAttributes(Assembly assembly, Type type)
    {
#if NETFRAMEWORK
        return _reflectionUtility.GetCustomAttributes(assembly, type);
#else
        return assembly.GetCustomAttributes(type).ToArray<object>();
#endif
    }
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
