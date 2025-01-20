// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class AttributeHelpers
{
    public static bool IsIgnored(ICustomAttributeProvider type, out string? ignoreMessage)
    {
        IEnumerable<ConditionalTestBaseAttribute> attributes = ReflectHelper.Instance.GetDerivedAttributes<ConditionalTestBaseAttribute>(type, inherit: false);
        foreach (ConditionalTestBaseAttribute attribute in attributes)
        {
            if (attribute.ShouldIgnore)
            {
                ignoreMessage = attribute.ConditionalIgnoreMessage;
                return true;
            }
        }

        ignoreMessage = null;
        return false;
    }
}
