// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    /// <summary>
    /// Gets all the custom attributes adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member. </param>
    /// <param name="inherit"> True to inspect the ancestors of element; otherwise, false. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    public object[] GetCustomAttributes(MemberInfo memberInfo, bool inherit) =>
#if NETFRAMEWORK
        ReflectionUtility.GetCustomAttributes(memberInfo, inherit).ToArray();
#else
        memberInfo.GetCustomAttributes(inherit);
#endif

    /// <summary>
    /// Gets all the custom attributes of a given type adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member info. </param>
    /// <param name="type"> The attribute type. </param>
    /// <param name="inherit"> True to inspect the ancestors of element; otherwise, false. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    public object[] GetCustomAttributes(MemberInfo memberInfo, Type type, bool inherit) =>
#if NETFRAMEWORK
        ReflectionUtility.GetCustomAttributes(memberInfo, type, inherit).ToArray();
#else
        memberInfo.GetCustomAttributes(type, inherit);
#endif

    /// <summary>
    /// Gets all the custom attributes of a given type on an assembly.
    /// </summary>
    /// <param name="assembly"> The assembly. </param>
    /// <param name="type"> The attribute type. </param>
    /// <returns> The list of attributes of the given type on the member. Empty list if none found. </returns>
    public object[] GetCustomAttributes(Assembly assembly, Type type) =>
#if NETFRAMEWORK
        ReflectionUtility.GetCustomAttributes(assembly, type).ToArray<object>();
#else
        assembly.GetCustomAttributes(type).ToArray<object>();
#endif
}
