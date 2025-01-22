// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class AttributeHelpers
{
    public static bool IsIgnored(ICustomAttributeProvider type, out string? ignoreMessage)
    {
        IEnumerable<ConditionalTestBaseAttribute> attributes = ReflectHelper.Instance.GetDerivedAttributes<ConditionalTestBaseAttribute>(type, inherit: false);
        IEnumerable<IGrouping<string, ConditionalTestBaseAttribute>> groups = attributes.GroupBy(attr => attr.GroupName);
        foreach (IGrouping<string, ConditionalTestBaseAttribute>? group in groups)
        {
            bool atLeastOneInGroupIsSatisfied = false;
            string? firstNonSatisfiedMatch = null;
            foreach (ConditionalTestBaseAttribute attribute in group)
            {
                if (attribute.ShouldRun)
                {
                    atLeastOneInGroupIsSatisfied = true;
                    break;
                }

                firstNonSatisfiedMatch ??= attribute.ConditionalIgnoreMessage;
            }

            if (!atLeastOneInGroupIsSatisfied)
            {
                ignoreMessage = firstNonSatisfiedMatch;
                return true;
            }
        }

        ignoreMessage = null;
        return false;
    }
}
