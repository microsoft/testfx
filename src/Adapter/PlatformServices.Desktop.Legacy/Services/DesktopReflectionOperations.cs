// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

    /// <summary>
    /// This service is responsible for platform specific reflection operations.
    /// </summary>
    /// <remarks>
    /// The test platform triggers discovery of test assets built for all architectures including ARM on desktop. In such cases we would need to load
    /// these sources in a reflection only context. Since Reflection-Only context currently is primarily prevalent in .Net Framework only, this service is required
    /// so that some operations like fetching attributes in a reflection only context can be performed.
    /// </remarks>
    public class ReflectionOperations : IReflectionOperations
    {
        private ReflectionUtility reflectionUtility;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReflectionOperations"/> class.
        /// </summary>
        public ReflectionOperations()
        {
            this.reflectionUtility = new ReflectionUtility();
        }

        /// <summary>
        /// Gets all the custom attributes adorned on a member.
        /// </summary>
        /// <param name="memberInfo"> The member. </param>
        /// <param name="inherit"> True to inspect the ancestors of element; otherwise, false. </param>
        /// <returns> The list of attributes on the member. Empty list if none found. </returns>
        public object[] GetCustomAttributes(MemberInfo memberInfo, bool inherit)
        {
            return this.reflectionUtility.GetCustomAttributes(memberInfo, inherit);
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
            return this.reflectionUtility.GetCustomAttributes(memberInfo, type, inherit);
        }

        /// <summary>
        /// Gets all the custom attributes of a given type on an assembly.
        /// </summary>
        /// <param name="assembly"> The assembly. </param>
        /// <param name="type"> The attribute type. </param>
        /// <returns> The list of attributes of the given type on the member. Empty list if none found. </returns>
        public object[] GetCustomAttributes(Assembly assembly, Type type)
        {
            return this.reflectionUtility.GetCustomAttributes(assembly, type);
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
