// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
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

        internal object[] GetCustomAttributes(MemberInfo memberInfo, Type type, bool inherit)
        {
            if (memberInfo == null)
            {
                return null;
            }

            bool shouldGetAllAttributes = type == null;

            if (shouldGetAllAttributes)
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
