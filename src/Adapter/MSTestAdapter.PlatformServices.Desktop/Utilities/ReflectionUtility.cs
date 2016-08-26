// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Utility for reflection API's
    /// </summary>
    internal class ReflectionUtility
    {
        /// <summary>
        /// Gets the custom attributes of the provided type on a memberInfo
        /// </summary>
        /// <param name="attributeProvider"> The member to reflect on. </param>
        /// <param name="type"> The attribute type. </param>
        /// <returns> The vale of the custom attibute. </returns>
        internal virtual object[] GetCustomAttributes(MemberInfo attributeProvider, Type type)
        {
            return this.GetCustomAttributes(attributeProvider, type, true);
        }

        /// <summary>
        /// Get custom attributes on a member for both normal and reflection only load.
        /// </summary>
        /// <param name="memberInfo">Member for which attributes needs to be retrieved.</param>
        /// <param name="type">Type of attribute to retrieve.</param>
        /// <param name="inherit">If inherited type of attribute.</param>
        /// <returns>All attributes of give type on member.</returns>
        private object[] GetCustomAttributes(MemberInfo memberInfo, Type type, bool inherit)
        {
            if (memberInfo == null)
            {
                return null;
            }

            return memberInfo.GetCustomAttributes(type, inherit).ToArray();
        }
    }
}
