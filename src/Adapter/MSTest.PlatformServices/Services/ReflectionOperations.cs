// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MSTest.PlatformServices.Interface;
#if NETFRAMEWORK
using MSTest.PlatformServices.Utilities;
#endif

namespace MSTest.PlatformServices;

/// <summary>
/// This service is responsible for platform specific reflection operations.
/// </summary>
internal class ReflectionOperations : IReflectionOperations
{
    /// <summary>
    /// Gets all the custom attributes adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo)
#if NETFRAMEWORK
         => [.. ReflectionUtility.GetCustomAttributes(memberInfo)];
#else
    {
        object[] attributes = memberInfo.GetCustomAttributes(typeof(Attribute), inherit: true);

        // Ensures that when the return of this method is used here:
        // https://github.com/microsoft/testfx/blob/e101a9d48773cc935c7b536d25d378d9a3211fee/src/Adapter/MSTest.TestAdapter/Helpers/ReflectHelper.cs#L461
        // then we are already Attribute[] to avoid LINQ Cast and extra array allocation.
        // This assert is solely for performance. Nothing "functional" will go wrong if the assert failed.
        Debug.Assert(attributes is Attribute[], $"Expected Attribute[], found '{attributes.GetType()}'.");
        return attributes;
    }
#endif

    /// <summary>
    /// Gets all the custom attributes of a given type adorned on a member.
    /// </summary>
    /// <param name="memberInfo"> The member info. </param>
    /// <param name="type"> The attribute type. </param>
    /// <returns> The list of attributes on the member. Empty list if none found. </returns>
    [return: NotNullIfNotNull(nameof(memberInfo))]
    public object[]? GetCustomAttributes(MemberInfo memberInfo, Type type) =>
#if NETFRAMEWORK
        [.. ReflectionUtility.GetCustomAttributesCore(memberInfo, type)];
#else
        memberInfo.GetCustomAttributes(type, inherit: true);
#endif

    /// <summary>
    /// Gets all the custom attributes of a given type on an assembly.
    /// </summary>
    /// <param name="assembly"> The assembly. </param>
    /// <param name="type"> The attribute type. </param>
    /// <returns> The list of attributes of the given type on the member. Empty list if none found. </returns>
    public object[] GetCustomAttributes(Assembly assembly, Type type) =>
#if NETFRAMEWORK
        ReflectionUtility.GetCustomAttributes(assembly, type).ToArray();
#else
        assembly.GetCustomAttributes(type, inherit: true);
#endif
}
