// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        internal virtual object[] GetCustomAttributes(MemberInfo attributeProvider, Type type)
        {
            return this.GetCustomAttributes(attributeProvider, type, true);
        }

        /// <summary>
        /// Gets all the custom attributes adorned on a member.
        /// </summary>
        /// <param name="memberInfo"> The member. </param>
        /// <param name="inherit"> True to inspect the ancestors of element; otherwise, false. </param>
        /// <returns> The list of attributes on the member. Empty list if none found. </returns>
        internal object[] GetCustomAttributes(MemberInfo memberInfo, bool inherit)
        {
            return this.GetCustomAttributes(memberInfo, type: null, inherit: inherit);
        }

        internal object[] GetCustomAttributes(MemberInfo memberInfo, Type type, bool inherit)
        {
            if (memberInfo == null)
            {
                return null;
            }

            if (type == null)
            {
                return memberInfo.GetCustomAttributes(inherit).ToArray();
            }
            else
            {
                return memberInfo.GetCustomAttributes(type, inherit).ToArray();
            }
        }
    }
}
