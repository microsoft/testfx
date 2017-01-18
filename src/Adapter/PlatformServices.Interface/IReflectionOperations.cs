// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface
{
    using System;
    using System.Reflection;

    /// <summary>
    /// This service is responsible for platform specific reflection operations.
    /// </summary>
    public interface IReflectionOperations
    {
        /// <summary>
        /// Gets all the custom attributes adorned on a member.
        /// </summary>
        /// <param name="memberInfo"> The member. </param>
        /// <param name="inherit"> True to inspect the ancestors of element; otherwise, false. </param>
        /// <returns> The list of attributes on the member. Empty list if none found. </returns>
        object[] GetCustomAttributes(MemberInfo memberInfo, bool inherit);

        /// <summary>
        /// Gets all the custom attributes of a given type adorned on a member.
        /// </summary>
        /// <param name="memberInfo"> The member info. </param>
        /// <param name="type"> The attribute type. </param>
        /// <param name="inherit"> True to inspect the ancestors of element; otherwise, false. </param>
        /// <returns> The list of attributes on the member. Empty list if none found. </returns>
        object[] GetCustomAttributes(MemberInfo memberInfo, Type type, bool inherit);

        /// <summary>
        /// Gets all the custom attributes of a given type on an assembly.
        /// </summary>
        /// <param name="assembly"> The assembly. </param>
        /// <param name="type"> The attribute type. </param>
        /// <returns> The list of attributes of the given type on the member. Empty list if none found. </returns>
        object[] GetCustomAttributes(Assembly assembly, Type type);
    }
}
