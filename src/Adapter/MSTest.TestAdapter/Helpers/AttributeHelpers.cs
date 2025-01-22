// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class AttributeHelpers
{
    public static bool IsIgnored(ICustomAttributeProvider type, out string? ignoreMessage)
    {
        IEnumerable<ConditionBaseAttribute> attributes = ReflectHelper.Instance.GetDerivedAttributes<ConditionBaseAttribute>(type, inherit: false);
        IEnumerable<IGrouping<string, ConditionBaseAttribute>> groups = attributes.GroupBy(attr => attr.GroupName);
        foreach (IGrouping<string, ConditionBaseAttribute>? group in groups)
        {
            bool atLeastOneInGroupIsSatisfied = false;
            string? firstNonSatisfiedMatch = null;
            foreach (ConditionBaseAttribute attribute in group)
            {
                bool shouldRun = attribute.Mode == Mode.Include ? attribute.ShouldRun : !attribute.ShouldRun;
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
